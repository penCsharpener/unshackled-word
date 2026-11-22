using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Vocab.Forms;

namespace UnshackledWord.Infrastructure.Vocab;

/// <summary>
/// The run loop of mode <c>vocab-form</c> (roadmap 0022.13). Asks the model for the dictionary forms of the entries of the run's rank range
/// in batches of <see cref="BatchSize"/>, checks every form with <see cref="VocabFormValidator"/> (rejected rows are asked once more with the
/// problem listed) and writes the valid ones through <see cref="VocabEntryEditor.ApplyAgentFormAsync(int, string, int?, CancellationToken)"/>
/// under the run's AI user: only missing or generated forms, origin <c>agent</c>, a changed text bumps like an editor change. Manual and agent
/// forms are never touched. Resume asks again only for what still has no agent form. The final status note carries the counts.
/// </summary>
public sealed class VocabFormRunExecutor
{
    public const int BatchSize = 25;
    public const int MaxConsecutiveFailures = 3;

    private readonly VocabFormRepository _forms;
    private readonly VocabEntryEditor _editor;
    private readonly IAgentRunRepository _runs;
    private readonly IAgentModelClientFactory _clients;
    private readonly IAgentPromptProvider _prompts;
    private readonly AgentProfiles _profiles;
    private readonly ILogger<VocabFormRunExecutor> _logger;

    public VocabFormRunExecutor(
        VocabFormRepository forms,
        VocabEntryEditor editor,
        IAgentRunRepository runs,
        IAgentModelClientFactory clients,
        IAgentPromptProvider prompts,
        IConfiguration configuration,
        ILogger<VocabFormRunExecutor> logger)
    {
        _forms = forms;
        _editor = editor;
        _runs = runs;
        _clients = clients;
        _prompts = prompts;
        _profiles = AgentProfiles.From(configuration);
        _logger = logger;
    }

    /// <summary>Rough token count of a text (4 characters per token); the same rule for the estimate and the cap check.</summary>
    public static long EstimateTokens(string text) => (text.Length + 3) / 4;

    public static long EstimatePackageTokens(IReadOnlyList<VocabFormCandidate> batch) =>
        batch.Sum(e => EstimateTokens(e.Lemma) + 12 + (VocabFormPackage.CleanLexicon(e.Lexicon)?.Length ?? 0) / 3);

    public static long EstimateOutputTokens(int entries) => entries * 24L;

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
            await _runs.SetRunStatusUnlessCancelledAsync(runId, AgentRunStatuses.Failed, "Interrupted by shutdown; resume continues with the entries without an agent form.", CancellationToken.None);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Vocab form run {RunId} failed", runId);
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
        var prompt = _prompts.GetVersion(VocabFormPackage.LanguageOf(track), run.PromptVersion);
        var client = _clients.Create(profile);

        if (!await _runs.SetRunStatusUnlessCancelledAsync(run.Id, AgentRunStatuses.Running, null, CancellationToken.None))
        {
            return;
        }

        var candidates = await _forms.SelectCandidatesAsync(course.Track, run.FromRefId, run.ToRefId, int.MaxValue, stop);
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
                var used = await AskAsync(run, profile, client, prompt, track, batch, counts, stop);
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
                _logger.LogWarning(e, "Vocab form run {RunId}: batch from rank {Rank} failed ({Kind})", run.Id, batch[0].Rank, e.Kind);

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
    /// One batch. Rows the validator rejects are asked once more in a second call (only those entries); rows the model leaves out count as
    /// unsure and stay as they are. Returns the tokens and cost of all calls.
    /// </summary>
    private async Task<(long Tokens, decimal Cost)> AskAsync(AgentRunRecord run, AgentProfile profile, IAgentModelClient client, AgentPrompt prompt, char track, VocabFormCandidate[] batch, Counts counts, CancellationToken stop)
    {
        var pending = batch.Select(c => new VocabFormInput(c.Id, c.Lemma, c.Pos, c.Lexicon)).ToList();
        var lemmas = batch.ToDictionary(c => c.Id, c => c.Lemma);
        long tokens = 0;
        decimal cost = 0;

        for (var attempt = 0; attempt <= 1 && pending.Count > 0; attempt++)
        {
            var result = await client.CompleteAsync(new AgentModelRequest
            {
                Model = run.Model,
                StaticPrefix = prompt.StaticPrefix,
                PackageJson = VocabFormPackage.Build(track, pending),
                JsonSchema = AgentAnswerSchema.VocabFormJson,
                MaxOutputTokens = profile.MaxOutputTokens,
            }, stop);

            var callCost = profile.Cost(run.Model, result.InputTokens, result.CachedInputTokens, result.OutputTokens);
            await _runs.AddRunUsageAsync(run.Id, result.InputTokens, result.CachedInputTokens, result.OutputTokens, callCost, CancellationToken.None);
            tokens += result.InputTokens + result.OutputTokens;
            cost += callCost;

            List<VocabFormAnswerRow> rows;

            try
            {
                rows = VocabFormPackage.ParseAnswer(result.Json, pending);
            }
            catch (JsonException)
            {
                rows = [];
            }

            var answered = rows.Select(r => r.EntryId).ToHashSet();
            var rejected = new HashSet<int>();
            counts.Unsure += pending.Count(p => !answered.Contains(p.EntryId));

            foreach (var row in rows)
            {
                if (VocabFormValidator.Check(lemmas[row.EntryId], row.Form) is not null)
                {
                    rejected.Add(row.EntryId);

                    continue;
                }

                var outcome = await _editor.ApplyAgentFormAsync(row.EntryId, row.Form.Trim(), run.AiUserId, CancellationToken.None);

                if (outcome.Status == VocabEditStatus.Ok)
                {
                    counts.Written++;
                    counts.Bumped += outcome.Response!.Bumped ? 1 : 0;
                }
                else
                {
                    counts.Protected++;
                }
            }

            if (attempt == 1)
            {
                counts.Rejected += rejected.Count;
            }

            pending = pending.Where(p => rejected.Contains(p.EntryId)).ToList();
        }

        return (tokens, cost);
    }

    private sealed class Counts
    {
        public int Written;
        public int Bumped;
        public int Rejected;
        public int Protected;
        public int Unsure;
        public int Failed;

        public string Summary() =>
            $"Filled {Written} forms ({Bumped} changed a learner-visible form), {Rejected} rejected by the validator, {Unsure} left out by the model, {Protected} skipped (form already agent or manual), {Failed} not answered after failed calls.";
    }
}
