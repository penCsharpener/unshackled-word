using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Application.Features.AgentMapping.Scoring;

/// <summary>A package of the run with what it cost (tables of migrations 0025 and 0028).</summary>
public sealed class ScorePackageRow
{
    public Guid Id { get; set; }
    public string LocalIdMap { get; set; } = string.Empty;
    public long InputTokens { get; set; }
    public long CachedInputTokens { get; set; }
    public long OutputTokens { get; set; }
    public decimal CostUsd { get; set; }
}

/// <summary>Reads and writes for the scorer (implemented in Infrastructure).</summary>
public interface IAgentScoreRepository
{
    /// <summary>All proposals of the run, any status (a discarded one is still what the model answered), oldest first.</summary>
    Task<IReadOnlyList<ScoredProposal>> GetProposalsAsync(int runId, CancellationToken token = default);

    Task<IReadOnlyList<ScorePackageRow>> GetPackagesAsync(int runId, CancellationToken token = default);

    /// <summary>Stores the score JSON with the run (<c>AgentRuns.Score</c>, <c>ScoredAt</c>).</summary>
    Task SaveScoreAsync(int runId, string scoreJson, CancellationToken token = default);

    /// <summary>The stored score JSON of the run (<c>AgentRuns.Score</c>); null while it was never scored or the run does not exist.</summary>
    Task<string?> GetStoredScoreAsync(int runId, CancellationToken token = default);
}

/// <summary>Finds and loads <c>assets/agent-reference/reference-vN.json</c>.</summary>
public interface IAgentReferenceStore
{
    /// <summary>The file named <paramref name="name"/> (<c>reference-v1</c> or <c>v1</c>), else the newest. Null when there is none.</summary>
    ReferenceSet? Load(string? name);
}

public sealed class AgentReferenceStore : IAgentReferenceStore
{
    private readonly string _directory;

    public AgentReferenceStore(string directory)
    {
        _directory = directory;
    }

    public static AgentReferenceStore FromConfiguration(IConfiguration configuration) => new(ResolveDirectory(configuration));

    /// <summary><c>AgentMapping:ReferencePath</c>, else <c>AppSettings:DatabaseSeeding:SolutionAssetsPath/agent-reference</c>, else <c>assets/agent-reference</c> above the binaries.</summary>
    public static string ResolveDirectory(IConfiguration configuration)
    {
        var explicitPath = configuration["AgentMapping:ReferencePath"];
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return Path.GetFullPath(explicitPath);
        }

        var assets = configuration["AppSettings:DatabaseSeeding:SolutionAssetsPath"];
        if (!string.IsNullOrWhiteSpace(assets))
        {
            var candidate = Path.GetFullPath(Path.Combine(assets, ReferenceSetFile.Directory));
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "assets", ReferenceSetFile.Directory);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.GetFullPath(Path.Combine(assets ?? "assets", ReferenceSetFile.Directory));
    }

    public ReferenceSet? Load(string? name)
    {
        if (!Directory.Exists(_directory))
        {
            return null;
        }

        string? file;
        if (!string.IsNullOrWhiteSpace(name))
        {
            var bare = Path.GetFileNameWithoutExtension(Path.GetFileName(name.Trim()));
            file = Path.Combine(_directory, (bare.StartsWith("reference-", StringComparison.Ordinal) ? bare : "reference-" + bare) + ".json");
        }
        else
        {
            file = Directory.GetFiles(_directory, "reference-v*.json")
                .Select(f => (File: f, Number: int.TryParse(Path.GetFileNameWithoutExtension(f)["reference-v".Length..], out var n) ? n : -1))
                .Where(x => x.Number >= 0)
                .OrderByDescending(x => x.Number)
                .Select(x => x.File)
                .FirstOrDefault();
        }

        return file is not null && File.Exists(file) ? ReferenceSetFile.Parse(File.ReadAllText(file)) : null;
    }
}

public enum AgentScoreOutcome
{
    Ok,
    RunNotFound,

    /// <summary>No reference file (yet): the maintainer has to freeze one.</summary>
    NoReference,

    /// <summary>The reference file breaks its own rules (prompt example verses, duplicates, ...).</summary>
    InvalidReference,
}

public sealed class AgentScoreResult
{
    public AgentScoreOutcome Outcome { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public AgentScoreResponse? Score { get; init; }
}

public interface IAgentScoreService
{
    /// <summary>Scores the run against the reference set and stores the result with the run.</summary>
    Task<AgentScoreResult> ScoreRunAsync(int runId, string? reference, CancellationToken token = default);

    /// <summary>The score stored by the last <see cref="ScoreRunAsync"/>, without recomputing. <see cref="AgentScoreOutcome.RunNotFound"/> when the run has no stored score.</summary>
    Task<AgentScoreResult> GetStoredScoreAsync(int runId, CancellationToken token = default);
}

public sealed class AgentScoreService : IAgentScoreService
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    private readonly IAgentRunRepository _runs;
    private readonly IAgentScoreRepository _scores;
    private readonly IAgentDataRepository _data;
    private readonly IAgentReferenceStore _references;
    private readonly TimeProvider _time;

    public AgentScoreService(IAgentRunRepository runs, IAgentScoreRepository scores, IAgentDataRepository data, IAgentReferenceStore references, TimeProvider time)
    {
        _runs = runs;
        _scores = scores;
        _data = data;
        _references = references;
        _time = time;
    }

    public async Task<AgentScoreResult> ScoreRunAsync(int runId, string? reference, CancellationToken token = default)
    {
        if (await _runs.GetRunAsync(runId, token) is not { } run)
        {
            return new AgentScoreResult { Outcome = AgentScoreOutcome.RunNotFound, Errors = [$"Run {runId} does not exist."] };
        }

        if (run.Mode == AgentModes.LxxTag)
        {
            // Roadmap 0028.7: the reference set holds Elb1871 mappings; LXX tags are reviewed on their own page (0028.7f).
            return new AgentScoreResult { Outcome = AgentScoreOutcome.NoReference, Errors = [$"Run {runId} is an LXX tagging run; LXX runs are not scored against the mapping reference."] };
        }

        ReferenceSet? set;
        try
        {
            set = _references.Load(reference);
        }
        catch (Exception e) when (e is JsonException or InvalidDataException)
        {
            return new AgentScoreResult { Outcome = AgentScoreOutcome.InvalidReference, Errors = [$"The reference file cannot be read: {e.Message}"] };
        }

        if (set is null)
        {
            return new AgentScoreResult { Outcome = AgentScoreOutcome.NoReference, Errors = ["No reference file found in assets/agent-reference. Review verses in the editor, then freeze them (POST maintainer/agent-reference/freeze)."] };
        }

        var problems = ReferenceSetFile.Validate(set);
        if (problems.Count > 0)
        {
            return new AgentScoreResult { Outcome = AgentScoreOutcome.InvalidReference, Errors = problems };
        }

        var proposals = (await _scores.GetProposalsAsync(runId, token))
            .GroupBy(p => (p.BibleBookId, p.Chapter, p.Verse))
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Id).Last());
        var packageShares = PackageShares(await _scores.GetPackagesAsync(runId, token));

        var inputs = new List<ScoredVerseInput>();
        var notInRun = 0;
        foreach (var expected in set.Verses)
        {
            var key = expected.Key;
            var proposal = proposals.GetValueOrDefault(key);
            var share = packageShares.GetValueOrDefault(key);
            if (!InRun(run, key, proposal is not null, share is not null))
            {
                notInRun++;
                continue;
            }

            var data = await _data.GetRangeAsync(expected.Book, expected.Chapter, expected.Verse, expected.Chapter, expected.Verse, token);
            var baseline = AgentVerseSnapshots.Build(expected.Book, expected.Chapter, expected.Verse, data);
            inputs.Add(new ScoredVerseInput
            {
                Expected = expected,
                Actual = proposal is null ? baseline.State : AgentProposalState.Apply(baseline, proposal),
                HasProposal = proposal is not null,
                InPackage = share is not null,
                FlagCodes = proposal is null ? [] : AgentProposalState.FlagCodes(proposal.FlagsJson),
                InputTokens = share?.InputTokens ?? 0,
                CachedInputTokens = share?.CachedInputTokens ?? 0,
                OutputTokens = share?.OutputTokens ?? 0,
                CostUsd = share?.CostUsd ?? 0,
            });
        }

        var referenceKeys = set.Verses.Select(v => v.Key).ToHashSet();
        var score = AgentScorer.Score(new AgentScoreInput
        {
            RunId = run.Id,
            Mode = run.Mode,
            Profile = run.Profile,
            Model = run.Model,
            PromptVersion = run.PromptVersion,
            RunStatus = run.Status,
            ReferenceVersion = set.Version,
            ReferenceVerses = set.Verses.Count,
            NotInRun = notInRun,
            ExtraRunVerses = proposals.Keys.Count(k => !referenceKeys.Contains(k)),
            Verses = inputs,
            ComputedAt = _time.GetUtcNow(),
        });

        await _scores.SaveScoreAsync(run.Id, JsonSerializer.Serialize(score, Json), token);

        return new AgentScoreResult { Outcome = AgentScoreOutcome.Ok, Score = score };
    }

    public async Task<AgentScoreResult> GetStoredScoreAsync(int runId, CancellationToken token = default)
    {
        var json = await _scores.GetStoredScoreAsync(runId, token);
        var score = string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<AgentScoreResponse>(json, Json);
        return score is null
            ? new AgentScoreResult { Outcome = AgentScoreOutcome.RunNotFound, Errors = [$"Run {runId} has no stored score."] }
            : new AgentScoreResult { Outcome = AgentScoreOutcome.Ok, Score = score };
    }

    /// <summary>A verse is in the run when it sits in a package, has a proposal or lies inside the run's range (silence keeps the stored state there).</summary>
    private static bool InRun(AgentRunRecord run, (int Book, int Chapter, int Verse) key, bool hasProposal, bool inPackage)
    {
        if (hasProposal || inPackage)
        {
            return true;
        }

        var refId = key.Book * 1_000_000 + key.Chapter * 1000 + key.Verse;

        return key.Book == run.BibleBookId && refId >= run.FromRefId && refId <= run.ToRefId;
    }

    /// <summary>Tokens and cost of a package split evenly over its verses.</summary>
    public static Dictionary<(int Book, int Chapter, int Verse), ScorePackageRow> PackageShares(IReadOnlyList<ScorePackageRow> packages)
    {
        var shares = new Dictionary<(int, int, int), ScorePackageRow>();
        foreach (var package in packages)
        {
            AgentPackageMap map;
            try
            {
                map = AgentPackageMap.FromJson(package.LocalIdMap);
            }
            catch (JsonException)
            {
                continue;
            }

            var count = Math.Max(1, map.Verses.Count);
            foreach (var verse in map.Verses.Values)
            {
                shares[(map.BookId, verse.Chapter, verse.Verse)] = new ScorePackageRow
                {
                    Id = package.Id,
                    InputTokens = (long)Math.Round((double)package.InputTokens / count),
                    CachedInputTokens = (long)Math.Round((double)package.CachedInputTokens / count),
                    OutputTokens = (long)Math.Round((double)package.OutputTokens / count),
                    CostUsd = package.CostUsd / count,
                };
            }
        }

        return shares;
    }
}
