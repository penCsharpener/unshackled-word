using Microsoft.Extensions.Configuration;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Application.Features.AgentMapping;

public enum AgentRunPrepareOutcome
{
    Ok,
    BadRequest,
    NotFound,

    /// <summary>The run is active, or has a status that cannot be resumed.</summary>
    Conflict,
}

public sealed class AgentRunPrepareResult
{
    public AgentRunPrepareOutcome Outcome { get; init; }
    public string? Error { get; init; }
    public int RunId { get; init; }
}

public sealed class AgentRunEstimateResult
{
    public AgentRunPrepareOutcome Outcome { get; init; }
    public string? Error { get; init; }
    public AgentRunEstimateResponse? Estimate { get; init; }
}

public interface IAgentRunService
{
    IReadOnlyList<AgentProfile> Profiles { get; }

    Task<AgentRunEstimateResult> EstimateAsync(AgentRunRangeRequest request, CancellationToken token = default);

    /// <summary>Validates, creates the run (<c>queued</c>) and the AI user of the model; the caller starts <see cref="AgentRunExecutor"/> for the returned id.</summary>
    Task<AgentRunPrepareResult> CreateRunAsync(StartAgentRunRequest request, int userId, CancellationToken token = default);

    /// <summary>Checks that a run can continue, stores a raised cap and sets it <c>queued</c>; the caller starts the executor.</summary>
    Task<AgentRunPrepareResult> PrepareResumeAsync(ResumeAgentRunRequest request, bool activeInProcess, CancellationToken token = default);

    /// <summary>Sets a run <c>cancelled</c>; a running worker notices before its next package and stops after the package in flight.</summary>
    Task<AgentRunPrepareResult> CancelAsync(int runId, CancellationToken token = default);
}

/// <summary>Estimate, start, resume and cancel of agent runs (the work itself is <see cref="AgentRunExecutor"/>).</summary>
public sealed class AgentRunService : IAgentRunService
{
    private const int MaxVerses = 3000;

    private readonly IAgentPackageBuilder _builder;
    private readonly IAgentRunRepository _runs;
    private readonly IAgentStoreRepository _store;
    private readonly IAgentPromptProvider _prompts;
    private readonly AgentProfiles _profiles;

    public AgentRunService(IAgentPackageBuilder builder, IAgentRunRepository runs, IAgentStoreRepository store, IAgentPromptProvider prompts, IConfiguration configuration)
    {
        _builder = builder;
        _runs = runs;
        _store = store;
        _prompts = prompts;
        _profiles = AgentProfiles.From(configuration);
    }

    public IReadOnlyList<AgentProfile> Profiles => _profiles.Items;

    public async Task<AgentRunEstimateResult> EstimateAsync(AgentRunRangeRequest request, CancellationToken token = default)
    {
        var plan = await PlanAsync(request, token);
        if (plan.Error is not null)
        {
            return new AgentRunEstimateResult { Outcome = plan.Outcome, Error = plan.Error };
        }

        var maxTokens = (request as StartAgentRunRequest)?.MaxTokens ?? plan.Profile!.EffectiveMaxTokens(plan.Model!);
        var maxCost = (request as StartAgentRunRequest)?.MaxCostUsd ?? plan.Profile!.EffectiveMaxCost(plan.Model!);

        return new AgentRunEstimateResult { Outcome = AgentRunPrepareOutcome.Ok, Estimate = plan.ToEstimate(maxTokens, maxCost) };
    }

    public async Task<AgentRunPrepareResult> CreateRunAsync(StartAgentRunRequest request, int userId, CancellationToken token = default)
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
            BibleBookId = request.Book,
            FromRefId = plan.FromRefId,
            ToRefId = plan.ToRefId,
            Mode = plan.Mode.ToDb(),
            Status = AgentRunStatuses.Queued,
            EstimatedTokens = estimate.EstimatedTotalTokens,
            EstimatedCostUsd = estimate.EstimatedCostUsd,
            MaxTokens = maxTokens,
            MaxCostUsd = maxCost,
        }, token);

        return new AgentRunPrepareResult { Outcome = AgentRunPrepareOutcome.Ok, RunId = runId };
    }

    public async Task<AgentRunPrepareResult> PrepareResumeAsync(ResumeAgentRunRequest request, bool activeInProcess, CancellationToken token = default)
    {
        var run = await _runs.GetRunAsync(request.RunId, token);
        if (run is null)
        {
            return new AgentRunPrepareResult { Outcome = AgentRunPrepareOutcome.NotFound, Error = $"Run {request.RunId} does not exist." };
        }

        if (activeInProcess || !AgentRunStatuses.CanResume(run.Status))
        {
            return new AgentRunPrepareResult { Outcome = AgentRunPrepareOutcome.Conflict, Error = $"Run {run.Id} is {(activeInProcess ? "still running" : run.Status)}; only failed, cancelled or budget-exceeded runs can be resumed." };
        }

        if (request.MaxTokens is <= 0 || request.MaxCostUsd is <= 0)
        {
            return new AgentRunPrepareResult { Outcome = AgentRunPrepareOutcome.BadRequest, Error = "maxTokens and maxCostUsd must be positive." };
        }

        var spentTokens = run.InputTokens + run.OutputTokens;
        if (request.MaxTokens is { } newTokens && newTokens <= spentTokens)
        {
            return new AgentRunPrepareResult { Outcome = AgentRunPrepareOutcome.BadRequest, Error = $"maxTokens {newTokens} must exceed the {spentTokens} tokens already spent." };
        }

        if (request.MaxCostUsd is { } newCost && newCost <= run.CostUsd)
        {
            return new AgentRunPrepareResult { Outcome = AgentRunPrepareOutcome.BadRequest, Error = $"maxCostUsd {newCost} must exceed the {run.CostUsd} USD already spent." };
        }

        if (_profiles.Find(run.Profile) is not { IsUsable: true })
        {
            return new AgentRunPrepareResult { Outcome = AgentRunPrepareOutcome.BadRequest, Error = $"Profile '{run.Profile}' is unknown or has no API key." };
        }

        if (request.MaxTokens is not null || request.MaxCostUsd is not null)
        {
            await _runs.SetCapsAsync(run.Id, request.MaxTokens ?? run.MaxTokens, request.MaxCostUsd ?? run.MaxCostUsd, token);
        }

        await _runs.SetRunStatusAsync(run.Id, AgentRunStatuses.Queued, "Resumed.", token);

        return new AgentRunPrepareResult { Outcome = AgentRunPrepareOutcome.Ok, RunId = run.Id };
    }

    public async Task<AgentRunPrepareResult> CancelAsync(int runId, CancellationToken token = default)
    {
        var run = await _runs.GetRunAsync(runId, token);
        if (run is null)
        {
            return new AgentRunPrepareResult { Outcome = AgentRunPrepareOutcome.NotFound, Error = $"Run {runId} does not exist." };
        }

        if (!AgentRunStatuses.IsActive(run.Status))
        {
            return new AgentRunPrepareResult { Outcome = AgentRunPrepareOutcome.Conflict, Error = $"Run {runId} is already {run.Status}.", RunId = runId };
        }

        await _runs.SetRunStatusAsync(runId, AgentRunStatuses.Cancelled, "Cancelled by the maintainer; the package in flight finishes first.", token);

        return new AgentRunPrepareResult { Outcome = AgentRunPrepareOutcome.Ok, RunId = runId };
    }

    private async Task<Plan> PlanAsync(AgentRunRangeRequest request, CancellationToken token)
    {
        if (request.Book is < 1 or > 66 || request.FromChapter < 1 || request.FromVerse < 1)
        {
            return Plan.Fail(AgentRunPrepareOutcome.BadRequest, "book must be 1..66 and fromChapter/fromVerse at least 1.");
        }

        var toChapter = request.ToChapter ?? request.FromChapter;
        var toVerse = request.ToVerse ?? (request.ToChapter is null ? request.FromVerse : 999);
        if (toChapter < request.FromChapter || (toChapter == request.FromChapter && toVerse < request.FromVerse))
        {
            return Plan.Fail(AgentRunPrepareOutcome.BadRequest, "The range ends before it starts.");
        }

        if (!AgentModes.TryParse(request.Mode, out var mode))
        {
            return Plan.Fail(AgentRunPrepareOutcome.BadRequest, "mode must be redo, fill-open or lemma.");
        }

        if (_profiles.Find(request.Profile) is not { } profile)
        {
            return Plan.Fail(AgentRunPrepareOutcome.BadRequest, $"Profile '{request.Profile}' is not configured (AgentMapping:Profiles).");
        }

        var model = string.IsNullOrWhiteSpace(request.Model) ? profile.Model : request.Model.Trim();
        var language = AgentPromptProvider.LanguageFor(request.Book, mode);
        AgentPrompt prompt;
        try
        {
            prompt = _prompts.Get(language);
        }
        catch (FileNotFoundException e)
        {
            return Plan.Fail(AgentRunPrepareOutcome.BadRequest, e.Message);
        }

        var range = new AgentRange { BibleBookId = request.Book, FromChapter = request.FromChapter, FromVerse = request.FromVerse, ToChapter = toChapter, ToVerse = toVerse };
        var built = await _builder.BuildAsync(new AgentBuildRequest
        {
            Range = range,
            Mode = mode,
            MaxVersesPerPackage = profile.EffectiveMaxVerses(model),
            DryRun = true,
            PromptVersion = prompt.Version,
        }, token);

        if (built is null)
        {
            return Plan.Fail(AgentRunPrepareOutcome.NotFound, "The range has no German words.");
        }

        if (built.Stats.Verses > MaxVerses)
        {
            return Plan.Fail(AgentRunPrepareOutcome.BadRequest, $"At most {MaxVerses} verses per run; split the range.");
        }

        return new Plan
        {
            Profile = profile,
            Model = model,
            Mode = mode,
            Prompt = prompt,
            PrefixTokens = _builder.EstimateTokens(prompt.StaticPrefix),
            Built = built,
            FromRefId = AgentRange.RefId(request.Book, request.FromChapter, request.FromVerse),
            ToRefId = AgentRange.RefId(request.Book, toChapter, toVerse),
        };
    }

    private sealed class Plan
    {
        public AgentRunPrepareOutcome Outcome { get; init; }
        public string? Error { get; init; }
        public AgentProfile? Profile { get; init; }
        public string? Model { get; init; }
        public AgentMode Mode { get; init; }
        public AgentPrompt? Prompt { get; init; }
        public int PrefixTokens { get; init; }
        public GetAgentPackageResponse? Built { get; init; }
        public int FromRefId { get; init; }
        public int ToRefId { get; init; }

        public static Plan Fail(AgentRunPrepareOutcome outcome, string error) => new() { Outcome = outcome, Error = error };

        public AgentRunEstimateResponse ToEstimate(long? maxTokens, decimal? maxCost)
        {
            var budget = AgentRunBudget.Estimate(Built!.Packages.Select(p => p.EstimatedTokens).ToList(), PrefixTokens, Profile!, Model!);
            var response = new AgentRunEstimateResponse
            {
                Profile = Profile!.Name,
                Model = Model!,
                PromptVersion = Prompt!.Version,
                Mode = Mode.ToDb(),
                Packages = Built.Packages.Count,
                Verses = Built.Packages.Sum(p => p.Package.Verses.Count),
                SkippedVerses = Built.Stats.SkippedVerses,
                PrefixTokens = PrefixTokens,
                EstimatedInputTokens = budget.InputTokens,
                EstimatedCachedInputTokens = budget.CachedInputTokens,
                EstimatedOutputTokens = budget.OutputTokens,
                EstimatedTotalTokens = budget.TotalTokens,
                EstimatedCostUsd = Math.Round(budget.CostUsd, 4),
                MaxTokens = maxTokens,
                MaxCostUsd = maxCost,
                Stats = Built.Stats,
            };

            response.ExceedsCap = (maxTokens is { } t && response.EstimatedTotalTokens > t) || (maxCost is { } c && response.EstimatedCostUsd > c);
            if (response.ExceedsCap)
            {
                response.Warnings.Add("The estimate is above a cap: the run stops with budget-exceeded before the package that would pass it; raise the cap and resume to continue.");
            }

            if (!Profile.IsUsable)
            {
                response.Warnings.Add(Profile.UnusableReason!);
            }

            if (Profile.ModelSettings(Model!).InputPricePerMillion == 0 && Profile.ModelSettings(Model!).OutputPricePerMillion == 0)
            {
                response.Warnings.Add($"No prices configured for model '{Model}' (AgentMapping:Profiles:{Profile.Name}:Models); the cost shows as 0.");
            }

            return response;
        }
    }
}

/// <summary>Token and cost arithmetic of 07-providers-and-caching.md ("Budget"): the estimate before a run and the per-package figure of the hard cap.</summary>
public static class AgentRunBudget
{
    /// <summary>Output tokens per input token of a package (Mk 1: about 17k input and 5k output); refined from finished runs.</summary>
    public const double OutputPerInputFactor = 0.3;

    /// <summary>Margin on top of the estimate for resubmissions.</summary>
    public const double RetryMargin = 1.3;

    public static long PackageOutputTokens(long packageTokens) => (long)Math.Ceiling(packageTokens * OutputPerInputFactor);

    /// <summary>Tokens of one package call: the prefix, the package and the expected answer (no margin).</summary>
    public static long PackageTotalTokens(long packageTokens, long prefixTokens) => prefixTokens + packageTokens + PackageOutputTokens(packageTokens);

    public static decimal PackageCost(long packageTokens, long prefixTokens, bool prefixCached, AgentProfile profile, string model) =>
        profile.Cost(model, prefixTokens + packageTokens, prefixCached ? prefixTokens : 0, PackageOutputTokens(packageTokens));

    public static AgentRunBudgetEstimate Estimate(IReadOnlyList<int> packageTokens, long prefixTokens, AgentProfile profile, string model)
    {
        long input = 0, cached = 0, output = 0;
        for (var i = 0; i < packageTokens.Count; i++)
        {
            input += prefixTokens + packageTokens[i];
            cached += i == 0 ? 0 : prefixTokens;
            output += PackageOutputTokens(packageTokens[i]);
        }

        input = (long)Math.Ceiling(input * RetryMargin);
        cached = (long)Math.Ceiling(cached * RetryMargin);
        output = (long)Math.Ceiling(output * RetryMargin);

        return new AgentRunBudgetEstimate(input, cached, output, input + output, profile.Cost(model, input, cached, output));
    }
}

public sealed record AgentRunBudgetEstimate(long InputTokens, long CachedInputTokens, long OutputTokens, long TotalTokens, decimal CostUsd);
