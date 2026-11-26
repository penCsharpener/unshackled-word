using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Application.Features.AgentMapping;

/// <summary>
/// The run loop of the agent worker (roadmap 0016.6). One instance per run: builds the packages of the run's range, sends them to the model
/// (the first one alone so the provider can cache the prefix, then up to <see cref="AgentProfile.MaxParallel"/> at a time), submits every answer
/// through <see cref="IAgentProposalService"/> and resubmits only the rejected verses (at most <see cref="MaxResubmissions"/> times, with the
/// validator messages). Nothing here writes live data; proposals are the only output.
/// </summary>
public sealed class AgentRunExecutor
{
    public const int MaxResubmissions = 2;

    /// <summary>Consecutive packages that fail for network or provider reasons before the run gives up (provider down, quota empty).</summary>
    public const int MaxConsecutiveTransientFailures = 3;

    /// <summary>Mapping packages as before; LXX packages without the empty mapping lists (roadmap 0028.7).</summary>
    private static readonly JsonSerializerOptions PackageJson = AgentPackageJson.Options;

    private readonly IAgentPackageBuilder _builder;
    private readonly IAgentProposalService _proposals;
    private readonly IAgentRunRepository _runs;
    private readonly IAgentStoreRepository _store;
    private readonly IAgentModelClientFactory _clients;
    private readonly IAgentPromptProvider _prompts;
    private readonly AgentProfiles _profiles;
    private readonly ILogger<AgentRunExecutor> _logger;

    public AgentRunExecutor(
        IAgentPackageBuilder builder,
        IAgentProposalService proposals,
        IAgentRunRepository runs,
        IAgentStoreRepository store,
        IAgentModelClientFactory clients,
        IAgentPromptProvider prompts,
        IConfiguration configuration,
        ILogger<AgentRunExecutor> logger)
    {
        _builder = builder;
        _proposals = proposals;
        _runs = runs;
        _store = store;
        _clients = clients;
        _prompts = prompts;
        _profiles = AgentProfiles.From(configuration);
        _logger = logger;
    }

    /// <summary>Runs (or continues) the run to the end; never throws except for shutdown (<paramref name="stop"/>), which leaves the run <c>failed</c> and resumable.</summary>
    public async Task RunAsync(int runId, CancellationToken stop = default)
    {
        var run = await _runs.GetRunAsync(runId, CancellationToken.None);
        if (run is null)
        {
            return;
        }

        using var runSpan = AgentTelemetry.Source.StartActivity("agent.run");
        runSpan?.SetTag("run.id", runId);
        runSpan?.SetTag("profile", run.Profile);
        runSpan?.SetTag("model", run.Model);

        var state = new RunState(run);
        try
        {
            await ExecuteAsync(run, state, stop);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            runSpan?.SetStatus(ActivityStatusCode.Error, "interrupted");
            await _runs.SetRunStatusUnlessCancelledAsync(runId, AgentRunStatuses.Failed, "Interrupted by shutdown; resume continues with the verses without a proposal.", CancellationToken.None);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Agent run {RunId} failed", runId);
            runSpan?.SetStatus(ActivityStatusCode.Error, e.GetType().Name);
            await _runs.SetRunStatusUnlessCancelledAsync(runId, AgentRunStatuses.Failed, $"{e.GetType().Name}: {e.Message}", CancellationToken.None);
        }
    }

    private async Task ExecuteAsync(AgentRunRecord run, RunState state, CancellationToken stop)
    {
        if (_profiles.Find(run.Profile) is not { } profile || !profile.IsUsable)
        {
            await _runs.SetRunStatusUnlessCancelledAsync(run.Id, AgentRunStatuses.Failed, $"Profile '{run.Profile}' is unknown or has no API key.", CancellationToken.None);
            return;
        }

        var mode = AgentModes.TryParse(run.Mode, out var parsed) ? parsed : AgentMode.Redo;
        var language = AgentPromptProvider.LanguageFor(run.BibleBookId, mode);
        var prompt = _prompts.GetVersion(language, run.PromptVersion);
        var prefixTokens = _builder.EstimateTokens(prompt.StaticPrefix);
        var client = _clients.Create(profile);
        var maxVerses = profile.EffectiveMaxVerses(run.Model);

        if (!await _runs.SetRunStatusUnlessCancelledAsync(run.Id, AgentRunStatuses.Running, null, CancellationToken.None))
        {
            return;
        }

        var skip = await _runs.GetProposedRefIdsAsync(run.Id, CancellationToken.None);
        var built = await _builder.BuildAsync(new AgentBuildRequest
        {
            Range = RangeOf(run.FromRefId, run.ToRefId),
            Mode = mode,
            MaxVersesPerPackage = maxVerses,
            UserId = run.CreatedByUserId,
            ExistingRunId = run.Id,
            SkipRefIds = skip,
            PromptVersion = run.PromptVersion,
        }, stop);

        var queue = new LinkedList<PackageWork>((built?.Packages ?? []).Select(p => new PackageWork(p)));
        var skippedNote = SkippedNote(mode, built?.Stats.SkippedVerses ?? 0);
        if (queue.Count == 0)
        {
            await _runs.SetRunStatusUnlessCancelledAsync(run.Id, AgentRunStatuses.Completed, string.Join(' ', new[] { "Nothing to do: every verse of the range has a proposal or is fully fixed.", skippedNote }.Where(x => !string.IsNullOrEmpty(x))), CancellationToken.None);
            return;
        }

        var inflight = new Dictionary<Task<PackageOutcome>, PackageWork>();
        var stopReason = (Status: AgentRunStatuses.Completed, Note: (string?)null);
        var warmedUp = false;
        var shrinkTo = maxVerses;
        var failedPackages = 0;
        var okPackages = 0;
        var consecutiveTransient = 0;

        while (true)
        {
            // Start as many packages as allowed: one until the first call came back (prefix cache warm-up), then the profile's parallelism.
            while (stopReason.Status == AgentRunStatuses.Completed && queue.Count > 0 && inflight.Count < (warmedUp ? profile.MaxParallel : 1))
            {
                stop.ThrowIfCancellationRequested();

                if (await _runs.GetRunStatusAsync(run.Id, CancellationToken.None) == AgentRunStatuses.Cancelled)
                {
                    stopReason = (AgentRunStatuses.Cancelled, null);
                    break;
                }

                var work = queue.First!.Value;
                queue.RemoveFirst();

                if (shrinkTo is { } limit && work.Verses > limit && work.Verses > 1)
                {
                    var parts = await SplitAsync(run, work, mode, Math.Max(1, limit), stop);
                    for (var i = parts.Count - 1; i >= 0; i--)
                    {
                        queue.AddFirst(parts[i]);
                    }

                    continue;
                }

                var estimateTokens = AgentRunBudget.PackageTotalTokens(work.Tokens, prefixTokens);
                var estimateCost = AgentRunBudget.PackageCost(work.Tokens, prefixTokens, warmedUp, profile, run.Model);
                var exceeded = state.WouldExceed(estimateTokens, estimateCost, inflight.Values.Sum(w => w.ReservedTokens), inflight.Values.Sum(w => w.ReservedCost));
                if (exceeded is not null)
                {
                    stopReason = (AgentRunStatuses.BudgetExceeded, $"Stopped before package {work.FromRefId}-{work.ToRefId}: {exceeded}. Raise the cap and resume to continue.");
                    queue.AddFirst(work);
                    break;
                }

                work.ReservedTokens = estimateTokens;
                work.ReservedCost = estimateCost;
                // Task.Run: the activities ProcessAsync starts must not leak into this loop's context.
                inflight[Task.Run(() => ProcessAsync(run, profile, client, prompt, work, state, prefixTokens, stop), CancellationToken.None)] = work;
            }

            if (inflight.Count == 0)
            {
                break;
            }

            var finished = await Task.WhenAny(inflight.Keys);
            var done = inflight[finished];
            inflight.Remove(finished);
            var outcome = await finished;

            switch (outcome.Kind)
            {
                case PackageOutcomeKind.Done:
                    warmedUp = true;
                    okPackages++;
                    consecutiveTransient = 0;
                    break;

                case PackageOutcomeKind.SchemaFailure:
                    warmedUp = true;
                    consecutiveTransient = 0;
                    if (done.Verses > 1)
                    {
                        // The answer was cut off or not JSON: ask for fewer verses at a time, for this package and the rest of the run.
                        shrinkTo = Math.Max(1, done.Verses / 2);
                        var parts = await SplitAsync(run, done, mode, shrinkTo.Value, stop);
                        for (var i = parts.Count - 1; i >= 0; i--)
                        {
                            queue.AddFirst(parts[i]);
                        }

                        _logger.LogWarning("Agent run {RunId}: package of {Verses} verses failed the schema, continuing with at most {Max} verses per package", run.Id, done.Verses, shrinkTo);
                    }
                    else
                    {
                        await _store.SetPackageStatusAsync(done.Id, AgentPackageStatuses.Failed, false, CancellationToken.None);
                        failedPackages++;
                        state.Notes.Add($"{done.FromRefId}: {outcome.Message}");
                    }

                    break;

                case PackageOutcomeKind.Transient:
                    failedPackages++;
                    consecutiveTransient++;
                    await _store.SetPackageStatusAsync(done.Id, AgentPackageStatuses.Failed, false, CancellationToken.None);
                    state.Notes.Add($"{done.FromRefId}: {outcome.Message}");
                    if (consecutiveTransient >= MaxConsecutiveTransientFailures && stopReason.Status == AgentRunStatuses.Completed)
                    {
                        stopReason = (AgentRunStatuses.Failed, $"{consecutiveTransient} packages in a row failed: {outcome.Message}");
                    }

                    break;

                case PackageOutcomeKind.Fatal:
                    stopReason = (AgentRunStatuses.Failed, outcome.Message);
                    break;

                case PackageOutcomeKind.BudgetExceeded:
                    if (stopReason.Status == AgentRunStatuses.Completed)
                    {
                        stopReason = (AgentRunStatuses.BudgetExceeded, outcome.Message);
                    }

                    break;
            }
        }

        // Packages that were built but never sent (stop, cancel): a resume builds new ones for the verses without a proposal.
        foreach (var unsent in queue)
        {
            await _store.SetPackageStatusAsync(unsent.Id, AgentPackageStatuses.Expired, false, CancellationToken.None);
        }

        var summary = string.Join(' ', new[] { state.Summary(failedPackages, okPackages), skippedNote }.Where(x => !string.IsNullOrEmpty(x)));
        switch (stopReason.Status)
        {
            case AgentRunStatuses.Cancelled:
                // The cancel request already stored the status; keep it.
                break;
            case AgentRunStatuses.Completed when okPackages == 0 && failedPackages > 0:
                await _runs.SetRunStatusUnlessCancelledAsync(run.Id, AgentRunStatuses.Failed, $"No package succeeded. {summary}", CancellationToken.None);
                break;
            default:
                await _runs.SetRunStatusUnlessCancelledAsync(run.Id, stopReason.Status, string.Join(' ', new[] { stopReason.Note, summary }.Where(x => !string.IsNullOrEmpty(x))), CancellationToken.None);
                break;
        }
    }

    /// <summary>Lemma mode: how many verses had no work (06-ai-runs.md) and cost nothing; null in the mapping modes or when none.</summary>
    public static string? SkippedNote(AgentMode mode, int skipped) =>
        mode == AgentMode.Lemma && skipped > 0 ? $"{skipped} verse(s) without work skipped." : null;

    /// <summary>One package: call, submit, resubmit rejected verses; usage is stored after every call.</summary>
    private async Task<PackageOutcome> ProcessAsync(AgentRunRecord run, AgentProfile profile, IAgentModelClient client, AgentPrompt prompt, PackageWork work, RunState state, int prefixTokens, CancellationToken stop)
    {
        using var packageSpan = AgentTelemetry.Source.StartActivity("agent.package");
        packageSpan?.SetTag("run.id", run.Id);
        packageSpan?.SetTag("package.id", work.Id.ToString());
        packageSpan?.SetTag("profile", run.Profile);
        packageSpan?.SetTag("model", run.Model);
        long inputSum = 0, cachedSum = 0, outputSum = 0;
        try
        {
            var turns = new List<AgentModelTurn>();
            var packageJson = JsonSerializer.Serialize(work.Envelope.Package, PackageJson);
            var versesById = work.Envelope.Package.Verses.ToDictionary(v => v.V, v => v.Ref);
            var stored = new HashSet<string>();

            for (var attempt = 0; attempt <= MaxResubmissions; attempt++)
            {
                using var attemptSpan = AgentTelemetry.Source.StartActivity("agent.attempt");
                attemptSpan?.SetTag("run.id", run.Id);
                attemptSpan?.SetTag("package.id", work.Id.ToString());
                attemptSpan?.SetTag("attempt", attempt + 1);
                attemptSpan?.SetTag("profile", run.Profile);
                attemptSpan?.SetTag("model", run.Model);
                AgentModelResult result;
                try
                {
                    result = await client.CompleteAsync(new AgentModelRequest
                    {
                        Model = run.Model,
                        StaticPrefix = prompt.StaticPrefix,
                        PackageJson = packageJson,
                        JsonSchema = AgentAnswerSchema.ForLanguage(prompt.Language),
                        MaxOutputTokens = profile.MaxOutputTokens,
                        Turns = turns.ToList(),
                    }, stop);
                }
                catch (AgentModelException e)
                {
                    await RecordAsync(run, profile, work, state, e.InputTokens, e.CachedInputTokens, e.OutputTokens);
                    TagUsage(attemptSpan, e.InputTokens, e.CachedInputTokens, e.OutputTokens);
                    (inputSum, cachedSum, outputSum) = (inputSum + e.InputTokens, cachedSum + e.CachedInputTokens, outputSum + e.OutputTokens);
                    attemptSpan?.SetStatus(ActivityStatusCode.Error, e.Kind.ToString());

                    return e.Kind switch
                    {
                        AgentModelErrorKind.SchemaFailure => new PackageOutcome(attempt == 0 ? PackageOutcomeKind.SchemaFailure : PackageOutcomeKind.Done, e.Message),
                        AgentModelErrorKind.Transient => new PackageOutcome(PackageOutcomeKind.Transient, e.Message),
                        _ => new PackageOutcome(PackageOutcomeKind.Fatal, $"{e.Kind}: {e.Message}"),
                    };
                }

                await RecordAsync(run, profile, work, state, result.InputTokens, result.CachedInputTokens, result.OutputTokens);
                TagUsage(attemptSpan, result.InputTokens, result.CachedInputTokens, result.OutputTokens);
                (inputSum, cachedSum, outputSum) = (inputSum + result.InputTokens, cachedSum + result.CachedInputTokens, outputSum + result.OutputTokens);
                // Per verse (roadmap 0028.7): the call's tokens over the verses of the package (a resubmission still sends the whole package).
                var verseCount = Math.Max(1, work.Verses);
                attemptSpan?.SetTag("verses", verseCount);
                attemptSpan?.SetTag("tokens.per_verse", (result.InputTokens + result.OutputTokens) / verseCount);
                _logger.LogInformation("Agent run {RunId} package {Package} attempt {Attempt}: {Input} input ({Cached} cached), {Output} output tokens, finish '{Finish}'; {Verses} verse(s), {InputPerVerse} input and {OutputPerVerse} output tokens per verse",
                    run.Id, work.Id, attempt + 1, result.InputTokens, result.CachedInputTokens, result.OutputTokens, result.FinishReason,
                    verseCount, result.InputTokens / verseCount, result.OutputTokens / verseCount);

                var answer = WithPackageId(result.Json, work.Id);
                var submit = await _proposals.SubmitAsync(work.Id.ToString(), answer, CancellationToken.None);

                string[] problems;
                if (submit.Outcome == AgentSubmitOutcome.Ok)
                {
                    var response = submit.Response!;
                    stored.UnionWith(response.Verses.Where(v => v.Status != AgentVerseStatus.Rejected).Select(v => v.V));
                    problems = [.. response.Verses.Where(v => v.Status == AgentVerseStatus.Rejected).SelectMany(v => v.Messages.DefaultIfEmpty($"{v.V}: rejected")),
                        .. response.NotAnswered.Where(v => !stored.Contains(v)).Select(v => $"{v} ({versesById.GetValueOrDefault(v, "?")}): no answer for this verse.")];
                }
                else if (submit.Outcome == AgentSubmitOutcome.Invalid)
                {
                    problems = [.. submit.Errors];
                }
                else
                {
                    // Not found, expired or stale: the package cannot be used any more; the text changed or took too long.
                    await _store.SetPackageStatusAsync(work.Id, submit.Outcome == AgentSubmitOutcome.Stale ? AgentPackageStatuses.Stale : AgentPackageStatuses.Expired, false, CancellationToken.None);

                    return new PackageOutcome(PackageOutcomeKind.Transient, $"package {submit.Outcome}: {string.Join(' ', submit.Errors)}");
                }

                if (problems.Length == 0)
                {
                    return new PackageOutcome(PackageOutcomeKind.Done, null);
                }

                if (attempt == MaxResubmissions)
                {
                    state.Notes.Add($"{work.FromRefId}: {problems.Length} problem(s) left after {MaxResubmissions} resubmissions");
                    return new PackageOutcome(PackageOutcomeKind.Done, null);
                }

                var retryTokens = AgentRunBudget.PackageTotalTokens(work.Tokens, prefixTokens) + result.OutputTokens;
                var exceeded = state.WouldExceed(retryTokens, AgentRunBudget.PackageCost(work.Tokens, prefixTokens, true, profile, run.Model), 0, 0);
                if (exceeded is not null)
                {
                    return new PackageOutcome(PackageOutcomeKind.BudgetExceeded, $"Stopped before resubmitting rejected verses of package {work.FromRefId}-{work.ToRefId}: {exceeded}. Raise the cap and resume to continue.");
                }

                turns.Add(new AgentModelTurn(AgentModelTurn.Assistant, result.Json));
                turns.Add(new AgentModelTurn(AgentModelTurn.User, RetryMessage(problems)));
            }

            return new PackageOutcome(PackageOutcomeKind.Done, null);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Agent run {RunId}: package {Package} failed", run.Id, work.Id);
            packageSpan?.SetStatus(ActivityStatusCode.Error, e.GetType().Name);

            return new PackageOutcome(PackageOutcomeKind.Transient, $"{e.GetType().Name}: {e.Message}");
        }
        finally
        {
            TagUsage(packageSpan, inputSum, cachedSum, outputSum);
        }
    }

    private static void TagUsage(Activity? span, long input, long cached, long output)
    {
        span?.SetTag("tokens.input", input);
        span?.SetTag("tokens.cached", cached);
        span?.SetTag("tokens.output", output);
    }

    private async Task RecordAsync(AgentRunRecord run, AgentProfile profile, PackageWork work, RunState state, long input, long cached, long output)
    {
        if (input == 0 && output == 0)
        {
            return;
        }

        var cost = profile.Cost(run.Model, input, cached, output);
        state.Add(input + output, cost);
        await _runs.AddUsageAsync(run.Id, work.Id, input, cached, output, cost, CancellationToken.None);
    }

    /// <summary>Rebuilds the verses of a package as smaller packages of the same run (the old package is marked <c>expired</c>).</summary>
    private async Task<List<PackageWork>> SplitAsync(AgentRunRecord run, PackageWork work, AgentMode mode, int maxVerses, CancellationToken token)
    {
        await _store.SetPackageStatusAsync(work.Id, AgentPackageStatuses.Expired, false, CancellationToken.None);
        var skip = await _runs.GetProposedRefIdsAsync(run.Id, CancellationToken.None);
        var built = await _builder.BuildAsync(new AgentBuildRequest
        {
            Range = RangeOf(work.FromRefId, work.ToRefId),
            Mode = mode,
            MaxVersesPerPackage = maxVerses,
            UserId = run.CreatedByUserId,
            ExistingRunId = run.Id,
            SkipRefIds = skip,
            PromptVersion = run.PromptVersion,
        }, token);

        return (built?.Packages ?? []).Select(p => new PackageWork(p)).ToList();
    }

    private static AgentRange RangeOf(int fromRef, int toRef) => new()
    {
        BibleBookId = fromRef / 1_000_000,
        FromChapter = fromRef / 1000 % 1000,
        FromVerse = fromRef % 1000,
        ToChapter = toRef / 1000 % 1000,
        ToVerse = toRef % 1000,
    };

    /// <summary>Sets <c>packageId</c> to the package's own id: a model may mangle a GUID, the binding is the worker's business.</summary>
    private static JsonElement WithPackageId(string json, Guid packageId)
    {
        var node = JsonNode.Parse(json);
        if (node is JsonObject obj)
        {
            obj["packageId"] = packageId.ToString();
        }

        return JsonSerializer.SerializeToElement(node);
    }

    public static string RetryMessage(IEnumerable<string> problems)
    {
        var text = new StringBuilder();
        text.AppendLine("The server rejected the following. Answer again with ONLY the verses named below (same JSON shape, same packageId, same local ids); every other verse is already stored.");
        foreach (var problem in problems)
        {
            text.Append("- ").AppendLine(problem);
        }

        return text.ToString().TrimEnd();
    }

    private enum PackageOutcomeKind
    {
        Done,
        SchemaFailure,
        Transient,
        Fatal,
        BudgetExceeded,
    }

    private sealed record PackageOutcome(PackageOutcomeKind Kind, string? Message);

    private sealed class PackageWork
    {
        public PackageWork(AgentPackageEnvelope envelope)
        {
            Envelope = envelope;
            Id = Guid.Parse(envelope.Package.PackageId);
        }

        public AgentPackageEnvelope Envelope { get; }
        public Guid Id { get; }
        public int Verses => Envelope.Package.Verses.Count;
        public int Tokens => Envelope.EstimatedTokens;
        public int FromRefId => Envelope.FromRefId;
        public int ToRefId => Envelope.ToRefId;
        public long ReservedTokens { get; set; }
        public decimal ReservedCost { get; set; }
    }

    /// <summary>Tokens and cost spent so far (the run's stored totals plus this process's calls) against the caps.</summary>
    private sealed class RunState
    {
        private readonly object _gate = new();
        private readonly long? _maxTokens;
        private readonly decimal? _maxCost;
        private long _tokens;
        private decimal _cost;

        public RunState(AgentRunRecord run)
        {
            _maxTokens = run.MaxTokens;
            _maxCost = run.MaxCostUsd;
            _tokens = run.InputTokens + run.OutputTokens;
            _cost = run.CostUsd;
        }

        public List<string> Notes { get; } = [];

        public void Add(long tokens, decimal cost)
        {
            lock (_gate)
            {
                _tokens += tokens;
                _cost += cost;
            }
        }

        /// <summary>A message when spent + reserved by packages in flight + this estimate would pass a cap, otherwise null.</summary>
        public string? WouldExceed(long estimateTokens, decimal estimateCost, long reservedTokens, decimal reservedCost)
        {
            lock (_gate)
            {
                // Reserved amounts of packages in flight are partly already in the totals; counting them again errs on the side of the cap.
                if (_maxTokens is { } t && _tokens + reservedTokens + estimateTokens > t)
                {
                    return $"{_tokens} tokens spent + about {reservedTokens + estimateTokens} for the next call(s) would pass MaxTokens {t}";
                }

                if (_maxCost is { } c && _cost + reservedCost + estimateCost > c)
                {
                    return $"{_cost:0.0000} USD spent + about {reservedCost + estimateCost:0.0000} for the next call(s) would pass MaxCostUsd {c}";
                }

                return null;
            }
        }

        public string Summary(int failedPackages, int okPackages)
        {
            var parts = new List<string>();
            if (failedPackages > 0)
            {
                parts.Add($"{failedPackages} package(s) failed ({okPackages} ok).");
            }

            parts.AddRange(Notes.Take(5));

            return string.Join(' ', parts);
        }
    }
}
