using Microsoft.Extensions.Configuration;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Vocab.Roots;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Infrastructure.Vocab;

public sealed class VocabRootRunEstimateResult
{
    public AgentRunPrepareOutcome Outcome { get; init; }
    public string? Error { get; init; }
    public VocabRootRunEstimateResponse? Estimate { get; init; }
}

/// <summary>
/// Estimate and start of a vocab root agent run (roadmap 0030.4). The run is an <c>AgentRuns</c> row of mode <c>vocab-root</c>
/// (BibleBookId = course id, From/ToRefId = rank range, IncludeConfirmed = option "Vorbefüllung prüfen"), so status, cancel, resume, caps and
/// totals are the ones of the agent runs; <see cref="VocabRootRunExecutor"/> does the work.
/// </summary>
public sealed class VocabRootRunService
{
    public const int MaxLimit = 2000;

    /// <summary>Entries selected when the request has no limit: the whole from..to range up to <see cref="MaxLimit"/>.</summary>
    public static int EffectiveLimit(int? limit) => limit ?? MaxLimit;

    private readonly VocabFormRepository _forms;
    private readonly VocabRootRunRepository _roots;
    private readonly IAgentRunRepository _runs;
    private readonly IAgentStoreRepository _store;
    private readonly IAgentPromptProvider _prompts;
    private readonly AgentProfiles _profiles;

    public VocabRootRunService(VocabFormRepository forms, VocabRootRunRepository roots, IAgentRunRepository runs, IAgentStoreRepository store, IAgentPromptProvider prompts, IConfiguration configuration)
    {
        _forms = forms;
        _roots = roots;
        _runs = runs;
        _store = store;
        _prompts = prompts;
        _profiles = AgentProfiles.From(configuration);
    }

    public async Task<VocabRootRunEstimateResult> EstimateAsync(VocabRootRunRangeRequest request, long? maxTokens = null, decimal? maxCost = null, CancellationToken token = default)
    {
        var plan = await PlanAsync(request, token);

        if (plan.Error is not null)
        {
            return new VocabRootRunEstimateResult { Outcome = plan.Outcome, Error = plan.Error };
        }

        return new VocabRootRunEstimateResult { Outcome = AgentRunPrepareOutcome.Ok, Estimate = plan.ToEstimate(maxTokens, maxCost) };
    }

    public async Task<AgentRunPrepareResult> CreateRunAsync(StartVocabRootRunRequest request, int userId, CancellationToken token = default)
    {
        var plan = await PlanAsync(request, token);

        if (plan.Error is not null)
        {
            return new AgentRunPrepareResult { Outcome = plan.Outcome, Error = plan.Error };
        }

        if (!plan.Profile!.IsUsable)
        {
            return new AgentRunPrepareResult { Outcome = AgentRunPrepareOutcome.BadRequest, Error = plan.Profile.UnusableReason };
        }

        if (request.MaxTokens is <= 0 || request.MaxCostUsd is <= 0)
        {
            return new AgentRunPrepareResult { Outcome = AgentRunPrepareOutcome.BadRequest, Error = "maxTokens and maxCostUsd must be positive." };
        }

        var maxTokens = request.MaxTokens ?? plan.Profile.EffectiveMaxTokens(plan.Model!);
        var maxCost = request.MaxCostUsd ?? plan.Profile.EffectiveMaxCost(plan.Model!);
        var estimate = plan.ToEstimate(maxTokens, maxCost);
        var aiUserId = await _runs.GetOrCreateAiUserAsync(plan.Model!, token);

        var runId = await _store.CreateRunAsync(new AgentRunInsert
        {
            CreatedByUserId = userId,
            AiUserId = aiUserId,
            Profile = plan.Profile.Name,
            Model = plan.Model!,
            PromptVersion = plan.Prompt!.Version,
            BibleBookId = plan.Course!.Id,
            FromRefId = plan.Candidates![0].Rank,
            ToRefId = plan.Candidates[^1].Rank,
            Mode = AgentModes.VocabRoot,
            Status = AgentRunStatuses.Queued,
            EstimatedTokens = estimate.EstimatedInputTokens + estimate.EstimatedOutputTokens,
            EstimatedCostUsd = estimate.EstimatedCostUsd,
            MaxTokens = maxTokens,
            MaxCostUsd = maxCost,
            BatchSize = plan.BatchSize,
            IncludeConfirmed = request.CheckPrefill,
        }, token);

        return new AgentRunPrepareResult { Outcome = AgentRunPrepareOutcome.Ok, RunId = runId };
    }

    private async Task<Plan> PlanAsync(VocabRootRunRangeRequest request, CancellationToken token)
    {
        if (request.FromRank < 1 || request.ToRank is { } to && to < request.FromRank)
        {
            return Plan.Fail(AgentRunPrepareOutcome.BadRequest, "fromRank must be at least 1 and toRank not below it.");
        }

        if (request.Limit is < 1 or > MaxLimit)
        {
            return Plan.Fail(AgentRunPrepareOutcome.BadRequest, $"limit must be 1..{MaxLimit}.");
        }

        if (request.BatchSize is < 1 or > VocabRootRunExecutor.MaxBatchSize)
        {
            return Plan.Fail(AgentRunPrepareOutcome.BadRequest, $"batchSize must be 1..{VocabRootRunExecutor.MaxBatchSize}.");
        }

        var course = await _forms.GetCourseAsync(request.Course, null, token);

        if (course is null)
        {
            return Plan.Fail(AgentRunPrepareOutcome.NotFound, $"Course '{request.Course}' does not exist.");
        }

        if (_profiles.Find(request.Profile) is not { } profile)
        {
            return Plan.Fail(AgentRunPrepareOutcome.BadRequest, $"Profile '{request.Profile}' is not configured (AgentMapping:Profiles).");
        }

        var model = string.IsNullOrWhiteSpace(request.Model) ? profile.Model : request.Model.Trim();
        AgentPrompt prompt;

        try
        {
            prompt = _prompts.Get(VocabRootPackage.LanguageOf(course.Track[0]));
        }
        catch (FileNotFoundException e)
        {
            return Plan.Fail(AgentRunPrepareOutcome.BadRequest, e.Message);
        }

        var candidates = await _roots.SelectCandidatesAsync(course.Track, request.FromRank, request.ToRank, EffectiveLimit(request.Limit), request.CheckPrefill, token);

        if (candidates.Count == 0)
        {
            return Plan.Fail(AgentRunPrepareOutcome.NotFound, request.CheckPrefill
                ? "No entry of the range needs a root (every row is agent or manual, or the range is empty)."
                : "No entry of the range needs a root (every main key has a row, or the range is empty).");
        }

        return new Plan { Profile = profile, Model = model, Prompt = prompt, Course = course, Candidates = candidates, BatchSize = request.BatchSize, CutAtMaxLimit = request.Limit is null && candidates.Count == MaxLimit };
    }

    private sealed class Plan
    {
        public AgentRunPrepareOutcome Outcome { get; init; }
        public string? Error { get; init; }
        public AgentProfile? Profile { get; init; }
        public string? Model { get; init; }
        public AgentPrompt? Prompt { get; init; }
        public VocabFormCourse? Course { get; init; }
        public IReadOnlyList<VocabRootCandidate>? Candidates { get; init; }

        public int? BatchSize { get; init; }
        public bool CutAtMaxLimit { get; init; }

        public static Plan Fail(AgentRunPrepareOutcome outcome, string error) => new() { Outcome = outcome, Error = error };

        public VocabRootRunEstimateResponse ToEstimate(long? maxTokens, decimal? maxCost)
        {
            var prefixTokens = VocabRootRunExecutor.EstimateTokens(Prompt!.StaticPrefix);
            var batchSize = VocabRootRunExecutor.EffectiveBatchSize(BatchSize);
            var batches = (Candidates!.Count + batchSize - 1) / batchSize;
            long input = 0, cached = 0, output = 0;

            for (var b = 0; b < batches; b++)
            {
                var batch = Candidates.Skip(b * batchSize).Take(batchSize).ToList();
                input += prefixTokens + VocabRootRunExecutor.EstimatePackageTokens(batch);
                cached += b == 0 ? 0 : prefixTokens;
                output += VocabRootRunExecutor.EstimateOutputTokens(batch.Count);
            }

            input = (long)Math.Ceiling(input * AgentRunBudget.RetryMargin);
            cached = (long)Math.Ceiling(cached * AgentRunBudget.RetryMargin);
            output = (long)Math.Ceiling(output * AgentRunBudget.RetryMargin);

            var response = new VocabRootRunEstimateResponse
            {
                Course = Course!.Code,
                Profile = Profile!.Name,
                Model = Model!,
                PromptVersion = Prompt.Version,
                Entries = Candidates.Count,
                Batches = batches,
                BatchSize = batchSize,
                FromRank = Candidates[0].Rank,
                ToRank = Candidates[^1].Rank,
                EstimatedInputTokens = input,
                EstimatedOutputTokens = output,
                EstimatedCostUsd = Math.Round(Profile.Cost(Model!, input, cached, output), 4),
            };

            if (CutAtMaxLimit)
            {
                response.Warnings.Add($"The range has more than {MaxLimit} entries: this run ends at rank {response.ToRank}; start another run from rank {response.ToRank + 1} for the rest.");
            }

            if ((maxTokens is { } t && input + output > t) || (maxCost is { } c && response.EstimatedCostUsd > c))
            {
                response.Warnings.Add("The estimate is above a cap: the run stops with budget-exceeded before the batch that would pass it; raise the cap and resume to continue.");
            }

            if (!Profile.IsUsable)
            {
                response.Warnings.Add(Profile.UnusableReason!);
            }

            var settings = Profile.ModelSettings(Model!);

            if (settings.InputPricePerMillion == 0 && settings.OutputPricePerMillion == 0)
            {
                response.Warnings.Add($"No prices configured for model '{Model}' (AgentMapping:Profiles:{Profile.Name}:Models); the cost shows as 0.");
            }

            return response;
        }
    }
}
