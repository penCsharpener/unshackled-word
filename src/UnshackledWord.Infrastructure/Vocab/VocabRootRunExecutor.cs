using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Vocab.Forms;
using UnshackledWord.Application.Features.Vocab.Roots;

namespace UnshackledWord.Infrastructure.Vocab;

/// <summary>
/// The run loop of mode <c>vocab-root</c> (roadmap 0030.4). Asks the model for the root and the stripped prepositions of the entries of the run's
/// rank range in batches of <see cref="BatchSize"/>, checks every answer with <see cref="VocabRootAnswerValidator"/> (rejected rows are asked once
/// more with the problem listed, flagged rows are only reported) and writes the accepted ones through <see cref="VocabRootRunWriter"/> with origin
/// <c>agent</c> and the run id. Manual rows are never touched; prefill rows only with the option "Vorbefüllung prüfen" (stored as the run's
/// <c>IncludeConfirmed</c>), and every replacement is listed in the run report. Resume asks again only for what still has no row.
/// </summary>
public sealed class VocabRootRunExecutor
{
    public const int BatchSize = 25;
    public const int MaxConsecutiveFailures = 3;

    private readonly VocabFormRepository _forms;
    private readonly VocabRootRunRepository _roots;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IAgentRunRepository _runs;
    private readonly IAgentModelClientFactory _clients;
    private readonly IAgentPromptProvider _prompts;
    private readonly AgentProfiles _profiles;
    private readonly ILogger<VocabRootRunExecutor> _logger;

    public VocabRootRunExecutor(
        VocabFormRepository forms,
        VocabRootRunRepository roots,
        IDbConnectionFactory connectionFactory,
        IAgentRunRepository runs,
        IAgentModelClientFactory clients,
        IAgentPromptProvider prompts,
        IConfiguration configuration,
        ILogger<VocabRootRunExecutor> logger)
    {
        _forms = forms;
        _roots = roots;
        _connectionFactory = connectionFactory;
        _runs = runs;
        _clients = clients;
        _prompts = prompts;
        _profiles = AgentProfiles.From(configuration);
        _logger = logger;
    }

    /// <summary>Rough token count of a text (4 characters per token); the same rule for the estimate and the cap check.</summary>
    public static long EstimateTokens(string text) => (text.Length + 3) / 4;

    public static long EstimatePackageTokens(IReadOnlyList<VocabRootCandidate> batch) =>
        batch.Sum(e => EstimateTokens(e.Lemma) + 24 + (e.Gloss?.Length ?? 0) / 4 + (VocabFormPackage.CleanLexicon(e.Lexicon)?.Length ?? 0) / 3);

    public static long EstimateOutputTokens(int entries) => entries * 45L;

    /// <summary>Runs (or continues) the run to the end; never throws except for shutdown (<paramref name="stop"/>), which leaves the run <c>failed</c> and resumable.</summary>
    public async Task RunAsync(int runId, CancellationToken stop = default)
    {
        var run = await _runs.GetRunAsync(runId, CancellationToken.None);

        if (run is null)
        {
            return;
        }

        try
        {
            await ExecuteAsync(run, stop);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            await _runs.SetRunStatusUnlessCancelledAsync(runId, AgentRunStatuses.Failed, "Interrupted by shutdown; resume continues with the entries without a root.", CancellationToken.None);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Vocab root run {RunId} failed", runId);
            await _runs.SetRunStatusUnlessCancelledAsync(runId, AgentRunStatuses.Failed, $"{e.GetType().Name}: {e.Message}", CancellationToken.None);
        }
    }

    private async Task ExecuteAsync(AgentRunRecord run, CancellationToken stop)
    {
        if (_profiles.Find(run.Profile) is not { IsUsable: true } profile)
        {
            await _runs.SetRunStatusUnlessCancelledAsync(run.Id, AgentRunStatuses.Failed, $"Profile '{run.Profile}' is unknown or has no API key.", CancellationToken.None);

            return;
        }

        var course = await _forms.GetCourseAsync(null, run.BibleBookId, stop);

        if (course is null)
        {
            await _runs.SetRunStatusUnlessCancelledAsync(run.Id, AgentRunStatuses.Failed, $"Vocab course {run.BibleBookId} does not exist.", CancellationToken.None);

            return;
        }

        var track = course.Track[0];
        var checkPrefill = run.IncludeConfirmed;
        var prompt = _prompts.GetVersion(VocabRootPackage.LanguageOf(track), run.PromptVersion);
        var client = _clients.Create(profile);

        if (!await _runs.SetRunStatusUnlessCancelledAsync(run.Id, AgentRunStatuses.Running, null, CancellationToken.None))
        {
            return;
        }

        var context = await _roots.LoadContextAsync(track, stop);
        var candidates = await _roots.SelectCandidatesAsync(course.Track, run.FromRefId, run.ToRefId, int.MaxValue, checkPrefill, stop);
        var prefixTokens = EstimateTokens(prompt.StaticPrefix);
        var counts = new Counts();
        var tokens = run.InputTokens + run.OutputTokens;
        var cost = run.CostUsd;
        var consecutiveFailures = 0;
        var status = AgentRunStatuses.Completed;
        string? stopNote = null;

        foreach (var batch in candidates.Chunk(BatchSize))
        {
            stop.ThrowIfCancellationRequested();

            if (await _runs.GetRunStatusAsync(run.Id, CancellationToken.None) == AgentRunStatuses.Cancelled)
            {
                status = AgentRunStatuses.Cancelled;

                break;
            }

            var batchTokens = prefixTokens + EstimatePackageTokens(batch) + EstimateOutputTokens(batch.Length);
            var batchCost = profile.Cost(run.Model, prefixTokens + EstimatePackageTokens(batch), 0, EstimateOutputTokens(batch.Length));

            if ((run.MaxTokens is { } maxTokens && tokens + batchTokens > maxTokens) || (run.MaxCostUsd is { } maxCost && cost + batchCost > maxCost))
            {
                status = AgentRunStatuses.BudgetExceeded;
                stopNote = $"Stopped before the batch from rank {batch[0].Rank}: the next call would exceed a cap. Raise the cap and resume to continue.";

                break;
            }

            try
            {
                var used = await AskAsync(run, profile, client, prompt, track, batch, context, checkPrefill, counts, stop);
                tokens += used.Tokens;
                cost += used.Cost;
                consecutiveFailures = 0;
            }
            catch (AgentModelException e)
            {
                var used = (Tokens: e.InputTokens + e.OutputTokens, Cost: profile.Cost(run.Model, e.InputTokens, e.CachedInputTokens, e.OutputTokens));
                await _runs.AddRunUsageAsync(run.Id, e.InputTokens, e.CachedInputTokens, e.OutputTokens, used.Cost, CancellationToken.None);
                tokens += used.Tokens;
                cost += used.Cost;
                counts.Failed += batch.Length;
                consecutiveFailures++;
                _logger.LogWarning(e, "Vocab root run {RunId}: batch from rank {Rank} failed ({Kind})", run.Id, batch[0].Rank, e.Kind);

                if (e.Kind is AgentModelErrorKind.Auth or AgentModelErrorKind.BadRequest || consecutiveFailures >= MaxConsecutiveFailures)
                {
                    status = AgentRunStatuses.Failed;
                    stopNote = $"Stopped at rank {batch[0].Rank}: {e.Kind}: {e.Message}";

                    break;
                }
            }
        }

        await _runs.SetRunStatusUnlessCancelledAsync(run.Id, status, $"{counts.Summary()}{(stopNote is null ? string.Empty : " " + stopNote)}", CancellationToken.None);
    }

    /// <summary>
    /// One batch. Rows the validator rejects are asked once more in a second call (only those entries, with the problem); rows the model leaves out
    /// count as unsure and stay as they are. Returns the tokens and cost of all calls.
    /// </summary>
    private async Task<(long Tokens, decimal Cost)> AskAsync(AgentRunRecord run, AgentProfile profile, IAgentModelClient client, AgentPrompt prompt, char track, VocabRootCandidate[] batch, VocabRootValidationContext context, bool checkPrefill, Counts counts, CancellationToken stop)
    {
        var pending = batch.Select(c => c.ToInput()).ToList();
        var originals = pending.ToDictionary(p => p.EntryId);
        long tokens = 0;
        decimal cost = 0;

        for (var attempt = 0; attempt <= 1 && pending.Count > 0; attempt++)
        {
            var result = await client.CompleteAsync(new AgentModelRequest
            {
                Model = run.Model,
                StaticPrefix = prompt.StaticPrefix,
                PackageJson = VocabRootPackage.Build(track, pending),
                JsonSchema = AgentAnswerSchema.VocabRootJson,
                MaxOutputTokens = profile.MaxOutputTokens,
            }, stop);

            var callCost = profile.Cost(run.Model, result.InputTokens, result.CachedInputTokens, result.OutputTokens);
            await _runs.AddRunUsageAsync(run.Id, result.InputTokens, result.CachedInputTokens, result.OutputTokens, callCost, CancellationToken.None);
            tokens += result.InputTokens + result.OutputTokens;
            cost += callCost;

            List<VocabRootAnswerRow> rows;

            try
            {
                rows = VocabRootPackage.ParseAnswer(result.Json, pending);
            }
            catch (JsonException)
            {
                rows = [];
            }

            var answered = rows.Select(r => r.EntryId).ToHashSet();
            var rejected = new Dictionary<int, string>();

            if (attempt == 0)
            {
                counts.Unsure += pending.Count(p => !answered.Contains(p.EntryId));
            }

            foreach (var row in rows)
            {
                var entry = originals[row.EntryId];
                var verdict = VocabRootAnswerValidator.Check(track, entry, row, context, checkPrefill);

                switch (verdict.Kind)
                {
                    case VocabRootVerdictKind.Dropped:
                        counts.Dropped++;

                        break;
                    case VocabRootVerdictKind.Rejected:
                        rejected[row.EntryId] = verdict.Reason!;

                        break;
                    case VocabRootVerdictKind.Flagged:
                        counts.Flagged++;
                        await _roots.SetItemAsync(run.Id, entry.EntryId, entry.MainKey, "flagged", verdict.Reason!, Describe(row), CancellationToken.None);

                        break;
                    default:
                        await WriteAsync(run, track, entry, row, verdict.Plan!, counts);

                        break;
                }
            }

            if (attempt == 1 || rejected.Count == 0)
            {
                foreach (var (entryId, reason) in rejected)
                {
                    var entry = originals[entryId];
                    counts.Rejected++;
                    await _roots.SetItemAsync(run.Id, entryId, entry.MainKey, "rejected", reason, Describe(rows.First(r => r.EntryId == entryId)), CancellationToken.None);
                }

                break;
            }

            pending = pending.Where(p => rejected.ContainsKey(p.EntryId)).Select(p => originals[p.EntryId] with { Problem = rejected[p.EntryId] }).ToList();
        }

        return (tokens, cost);
    }

    private async Task WriteAsync(AgentRunRecord run, char track, VocabRootInput entry, VocabRootAnswerRow row, VocabRootPlan plan, Counts counts)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(CancellationToken.None);
        using var transaction = connection.BeginTransaction();
        var outcome = await VocabRootRunWriter.ApplyAsync(connection, transaction, track, entry, plan, run.Id, run.AiUserId);
        transaction.Commit();

        switch (outcome.Status)
        {
            case VocabRootWriteStatus.Written:
                counts.Written++;

                break;
            case VocabRootWriteStatus.Confirmed:
                counts.Confirmed++;

                break;
            case VocabRootWriteStatus.Replaced:
                counts.Replaced++;
                await _roots.SetItemAsync(run.Id, entry.EntryId, entry.MainKey, "replaced", outcome.Reason!, Describe(row), CancellationToken.None);

                break;
            default:
                counts.Flagged++;
                await _roots.SetItemAsync(run.Id, entry.EntryId, entry.MainKey, "flagged", outcome.Reason ?? "Not written.", Describe(row), CancellationToken.None);

                break;
        }
    }

    private static string Describe(VocabRootAnswerRow row) =>
        row.Simple ? "simple" : VocabRootRunWriter.Describe(row.Prefixes.Count == 0 ? null : string.Join(" + ", row.Prefixes), row.RootKey ?? row.RootLetters ?? "?", null);

    private sealed class Counts
    {
        public int Written;
        public int Replaced;
        public int Confirmed;
        public int Rejected;
        public int Flagged;
        public int Dropped;
        public int Unsure;
        public int Failed;

        public string Summary() =>
            $"Wrote {Written} roots, replaced {Replaced} prefill rows, confirmed {Confirmed}; {Rejected} rejected by the validator, {Flagged} flagged, {Unsure} left out by the model, {Dropped} skipped (manual row), {Failed} not answered after failed calls.";
    }
}
