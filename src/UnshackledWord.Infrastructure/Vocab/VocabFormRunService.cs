using Microsoft.Extensions.Configuration;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Vocab.Forms;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Infrastructure.Vocab;

public sealed class VocabFormRunEstimateResult
{
    public AgentRunPrepareOutcome Outcome { get; init; }
    public string? Error { get; init; }
    public VocabFormRunEstimateResponse? Estimate { get; init; }
}

/// <summary>
/// Estimate and start of a dictionary-form agent run (roadmap 0022.13). The run is an <c>AgentRuns</c> row of mode <c>vocab-form</c>
/// (BibleBookId = course id, From/ToRefId = rank range), so status, cancel, resume, caps and totals are the ones of the agent runs;
/// <see cref="VocabFormRunExecutor"/> does the work.
/// </summary>
public sealed class VocabFormRunService
{
    public const int MaxLimit = 2000;
    public const int DefaultLimit = 200;

    private readonly VocabFormRepository _forms;
    private readonly IAgentRunRepository _runs;
    private readonly IAgentStoreRepository _store;
    private readonly IAgentPromptProvider _prompts;
    private readonly AgentProfiles _profiles;

    public VocabFormRunService(VocabFormRepository forms, IAgentRunRepository runs, IAgentStoreRepository store, IAgentPromptProvider prompts, IConfiguration configuration)
    {
        _forms = forms;
        _runs = runs;
        _store = store;
        _prompts = prompts;
        _profiles = AgentProfiles.From(configuration);
    }

    public async Task<VocabFormRunEstimateResult> EstimateAsync(VocabFormRunRangeRequest request, long? maxTokens = null, decimal? maxCost = null, CancellationToken token = default)
    {
        var plan = await PlanAsync(request, token);

        if (plan.Error is not null)
        {
            return new VocabFormRunEstimateResult { Outcome = plan.Outcome, Error = plan.Error };
        }

        return new VocabFormRunEstimateResult { Outcome = AgentRunPrepareOutcome.Ok, Estimate = plan.ToEstimate(maxTokens, maxCost) };
    }

    public async Task<AgentRunPrepareResult> CreateRunAsync(StartVocabFormRunRequest request, int userId, CancellationToken token = default)
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
            Mode = AgentModes.VocabForm,
            Status = AgentRunStatuses.Queued,
            EstimatedTokens = estimate.EstimatedInputTokens + estimate.EstimatedOutputTokens,
            EstimatedCostUsd = estimate.EstimatedCostUsd,
            MaxTokens = maxTokens,
            MaxCostUsd = maxCost,
        }, token);

        return new AgentRunPrepareResult { Outcome = AgentRunPrepareOutcome.Ok, RunId = runId };
    }

    private async Task<Plan> PlanAsync(VocabFormRunRangeRequest request, CancellationToken token)
    {
        if (request.FromRank < 1 || request.ToRank is { } to && to < request.FromRank)
        {
            return Plan.Fail(AgentRunPrepareOutcome.BadRequest, "fromRank must be at least 1 and toRank not below it.");
        }

        if (request.Limit is < 1 or > MaxLimit)
        {
            return Plan.Fail(AgentRunPrepareOutcome.BadRequest, $"limit must be 1..{MaxLimit}.");
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
            prompt = _prompts.Get(VocabFormPackage.LanguageOf(course.Track[0]));
        }
        catch (FileNotFoundException e)
        {
            return Plan.Fail(AgentRunPrepareOutcome.BadRequest, e.Message);
        }

        var candidates = await _forms.SelectCandidatesAsync(course.Track, request.FromRank, request.ToRank, request.Limit, token);

        if (candidates.Count == 0)
        {
            return Plan.Fail(AgentRunPrepareOutcome.NotFound, "No entry of the range needs a form (every form is agent or manual, or the range is empty).");
        }

        return new Plan { Profile = profile, Model = model, Prompt = prompt, Course = course, Candidates = candidates, FromRank = request.FromRank };
    }

    private sealed class Plan
    {
        public AgentRunPrepareOutcome Outcome { get; init; }
        public string? Error { get; init; }
        public AgentProfile? Profile { get; init; }
        public string? Model { get; init; }
        public AgentPrompt? Prompt { get; init; }
        public VocabFormCourse? Course { get; init; }
        public IReadOnlyList<VocabFormCandidate>? Candidates { get; init; }
        public int FromRank { get; init; }

        public static Plan Fail(AgentRunPrepareOutcome outcome, string error) => new() { Outcome = outcome, Error = error };

        public VocabFormRunEstimateResponse ToEstimate(long? maxTokens, decimal? maxCost)
        {
            var prefixTokens = VocabFormRunExecutor.EstimateTokens(Prompt!.StaticPrefix);
            var batches = (Candidates!.Count + VocabFormRunExecutor.BatchSize - 1) / VocabFormRunExecutor.BatchSize;
            long input = 0, cached = 0, output = 0;

            for (var b = 0; b < batches; b++)
            {
                var batch = Candidates.Skip(b * VocabFormRunExecutor.BatchSize).Take(VocabFormRunExecutor.BatchSize).ToList();
                input += prefixTokens + VocabFormRunExecutor.EstimatePackageTokens(batch);
                cached += b == 0 ? 0 : prefixTokens;
                output += VocabFormRunExecutor.EstimateOutputTokens(batch.Count);
            }

            input = (long)Math.Ceiling(input * AgentRunBudget.RetryMargin);
            cached = (long)Math.Ceiling(cached * AgentRunBudget.RetryMargin);
            output = (long)Math.Ceiling(output * AgentRunBudget.RetryMargin);

            var response = new VocabFormRunEstimateResponse
            {
                Course = Course!.Code,
                Profile = Profile!.Name,
                Model = Model!,
                PromptVersion = Prompt.Version,
                Entries = Candidates.Count,
                Batches = batches,
                FromRank = Candidates[0].Rank,
                ToRank = Candidates[^1].Rank,
                EstimatedInputTokens = input,
                EstimatedOutputTokens = output,
                EstimatedCostUsd = Math.Round(Profile.Cost(Model!, input, cached, output), 4),
            };

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
