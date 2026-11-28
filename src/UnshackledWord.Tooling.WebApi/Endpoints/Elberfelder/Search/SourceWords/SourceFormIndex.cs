using Dapper;
using Npgsql;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.DataQuality;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.Search.SourceWords;

/// <summary>One inflected form as printed in the source text, with the Strong's number it belongs to and its occurrences.</summary>
public sealed record SourceFormEntry(string StrongsKey, string Form, string Folded, int Count);

/// <summary>
/// In-process list of every distinct surface form of the Hebrew / Greek text (from DqWordFacts, so it is empty until the data quality facts were computed once),
/// folded like <see cref="TranslitFolding.FoldScript"/>. Loaded on first use and re-read after <see cref="MaxAge"/>, since the maintainer can recompute the facts.
/// </summary>
public sealed class SourceFormIndex : ISeedDataChanged
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Snapshot? _snapshot;

    public SourceFormIndex(IServiceScopeFactory scopeFactory, TimeProvider time)
    {
        _scopeFactory = scopeFactory;
        _time = time;
    }

    private SourceFormIndex(IEnumerable<SourceFormEntry> entries, TimeProvider time)
    {
        _time = time;
        _snapshot = Snapshot.Of(entries, DateTimeOffset.MaxValue);
    }

    /// <summary>An index over ready-made forms (no database); never expires.</summary>
    public static SourceFormIndex FromEntries(IEnumerable<SourceFormEntry> entries) => new(entries, TimeProvider.System);

    private sealed class Snapshot
    {
        public required IReadOnlyList<SourceFormEntry> All { get; init; }
        public required IReadOnlyDictionary<string, List<SourceFormEntry>> ByStrongs { get; init; }
        public required IReadOnlyDictionary<string, int> Totals { get; init; }
        public required DateTimeOffset ExpiresAt { get; init; }

        public static Snapshot Of(IEnumerable<SourceFormEntry> entries, DateTimeOffset expiresAt)
        {
            var all = entries.OrderByDescending(e => e.Count).ThenBy(e => e.Form, StringComparer.Ordinal).ToList();
            var byStrongs = all.GroupBy(e => e.StrongsKey).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
            return new Snapshot
            {
                All = all,
                ByStrongs = byStrongs,
                Totals = byStrongs.ToDictionary(g => g.Key, g => g.Value.Sum(e => e.Count), StringComparer.Ordinal),
                ExpiresAt = expiresAt,
            };
        }
    }

    private sealed class FormRow
    {
        public string StrongsBase { get; init; } = string.Empty;
        public string Form { get; init; } = string.Empty;
        public int Count { get; init; }
    }

    private async Task<Snapshot> EnsureLoadedAsync(CancellationToken ct)
    {
        if (_snapshot is { } current && current.ExpiresAt > _time.GetUtcNow()) return current;
        await _gate.WaitAsync(ct);
        try
        {
            if (_snapshot is { } again && again.ExpiresAt > _time.GetUtcNow()) return again;
            await using var scope = _scopeFactory!.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var connection = await factory.CreateDbConnectionAsync(ct);
            IEnumerable<FormRow> rows;
            try
            {
                rows = (await connection.QueryAsync<FormRow>(new CommandDefinition("""
                SELECT f."StrongsBase"                     AS "StrongsBase"
                     , f."SourceWord"                      AS "Form"
                     , count(DISTINCT f."StepWordId")::int AS "Count"
                FROM "unshackled-word"."DqWordFacts" f
                WHERE f."StepWordId" IS NOT NULL
                  AND f."StrongsBase" IS NOT NULL
                  AND f."SourceWord" IS NOT NULL
                GROUP BY f."StrongsBase", f."SourceWord"
                """, cancellationToken: ct))).ToList();
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ObjectNotInPrerequisiteState)
            {
                // DqWordFacts is a materialized view that was never refreshed: not ready, same as an empty one.
                rows = [];
            }

            var entries = rows
                .Select(r => new SourceFormEntry(r.StrongsBase, r.Form, TranslitFolding.FoldScript(r.Form), r.Count))
                .Where(e => e.Folded.Length > 0);
            // An empty result (facts not computed yet) is not kept for long: the next request asks again.
            var snapshot = Snapshot.Of(entries, _time.GetUtcNow() + MaxAge);
            _snapshot = snapshot.All.Count == 0 ? null : snapshot;
            return snapshot;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>A seed step wrote data: drop the snapshot, the next request reads again.</summary>
    public Task SeedDataChangedAsync(string stepKey, CancellationToken token)
    {
        Volatile.Write(ref _snapshot, null);
        return Task.CompletedTask;
    }

    /// <summary>False while the facts were never computed (the endpoint answers 503).</summary>
    public async Task<bool> IsReadyAsync(CancellationToken ct) => (await EnsureLoadedAsync(ct)).All.Count > 0;

    /// <summary>Forms whose folded spelling starts with <paramref name="folded"/> (most frequent first, at most <paramref name="max"/>).</summary>
    public async Task<IReadOnlyList<SourceFormEntry>> MatchAsync(string folded, int max, CancellationToken ct)
    {
        var snapshot = await EnsureLoadedAsync(ct);
        return folded.Length == 0 ? [] : [.. snapshot.All.Where(e => e.Folded.StartsWith(folded, StringComparison.Ordinal)).Take(max)];
    }

    /// <summary>Forms of one Strong's number, most frequent first.</summary>
    public async Task<IReadOnlyList<SourceFormEntry>> FormsOfAsync(string strongsKey, CancellationToken ct) =>
        (await EnsureLoadedAsync(ct)).ByStrongs.GetValueOrDefault(strongsKey) ?? [];

    public async Task<int> OccurrencesOfAsync(string strongsKey, CancellationToken ct) =>
        (await EnsureLoadedAsync(ct)).Totals.GetValueOrDefault(strongsKey);
}
