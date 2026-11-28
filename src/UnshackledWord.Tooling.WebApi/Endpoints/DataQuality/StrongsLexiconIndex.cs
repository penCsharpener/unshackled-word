using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.DataQuality;

namespace UnshackledWord.Tooling.WebApi.Endpoints.DataQuality;

public sealed class LexiconEntry
{
    public required string Key { get; init; }
    public required string Lang { get; init; }
    public required string Lemma { get; init; }
    public required string Transliteration { get; init; }
    public required string Gloss { get; init; }

    public required string FoldedScript { get; init; }
    public required string FoldedTranslit { get; init; }
    public required string Skeleton { get; init; }
    public required string[] GlossWords { get; init; }
}

public sealed record LexiconHit(LexiconEntry Entry, string MatchedBy);

/// <summary>
/// In-process search index over StepStrongsLexicon (one entry per base Strong's number; ~20k), loaded on first use.
/// Matching order: exact Strong's → script without diacritics → folded transliteration prefix → Hebrew consonant skeleton → English gloss word.
/// </summary>
public sealed class StrongsLexiconIndex : ISeedDataChanged
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Dictionary<string, LexiconEntry>? _byKey;
    private List<LexiconEntry> _all = [];

    public StrongsLexiconIndex(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>An index over ready-made entries (no database); the tests build the source word search on it.</summary>
    public static StrongsLexiconIndex FromEntries(IEnumerable<LexiconEntry> entries)
    {
        var index = new StrongsLexiconIndex(null!);
        var byKey = entries.ToDictionary(e => e.Key, StringComparer.Ordinal);
        index._all = [.. byKey.Values.OrderBy(e => e.Key, StringComparer.Ordinal)];
        index._byKey = byKey;
        return index;
    }

    private const string SelectLexicon = """
        SELECT l."LanguageId"                AS "LanguageId"
             , l."Number"                    AS "Number"
             , l."Extra"                     AS "Extra"
             , l."OriginalWord"              AS "OriginalWord"
             , l."OriginalWordNoDiacritics"  AS "OriginalWordNoDiacritics"
             , l."Transliteration"           AS "Transliteration"
             , l."Gloss"                     AS "Gloss"
        FROM "unshackled-word"."StepStrongsLexicon" l
        WHERE l."LanguageId" IN (0, 1, 2)
        ORDER BY l."Number", l."LanguageId", l."Extra" NULLS FIRST
        """;

    private sealed class LexiconRow
    {
        public int LanguageId { get; init; }
        public int Number { get; init; }
        public string? Extra { get; init; }
        public string? OriginalWord { get; init; }
        public string? OriginalWordNoDiacritics { get; init; }
        public string? Transliteration { get; init; }
        public string? Gloss { get; init; }
    }

    private async Task EnsureLoadedAsync(CancellationToken ct)
    {
        if (_byKey is not null) return;
        await _gate.WaitAsync(ct);
        try
        {
            if (_byKey is not null) return;
            await using var scope = _scopeFactory.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var connection = await factory.CreateDbConnectionAsync(ct);
            var rows = (await connection.QueryAsync<LexiconRow>(new CommandDefinition(SelectLexicon, cancellationToken: ct))).ToList();

            var entries = new Dictionary<string, LexiconEntry>(StringComparer.Ordinal);
            foreach (var group in rows.GroupBy(r => (Lang: r.LanguageId == 2 ? "G" : "H", r.Number)))
            {
                var key = group.Key.Lang + group.Key.Number.ToString("D4");
                if (entries.ContainsKey(key)) continue;

                // Prefer the general sense (no Extra, then G); Hebrew (LanguageId 0) before Aramaic (1).
                var chosen = group.OrderBy(r => r.LanguageId).ThenBy(r => r.Extra is null ? 0 : r.Extra == "G" ? 1 : 2).First();
                var translit = chosen.Transliteration ?? string.Empty;
                var translits = group.Select(r => r.Transliteration).Where(t => !string.IsNullOrEmpty(t)).Distinct().ToList();
                var allScript = string.Join(' ', group.Select(r => r.OriginalWordNoDiacritics).Where(t => !string.IsNullOrEmpty(t)).Distinct());
                var glossText = string.Join(' ', group.Select(r => r.Gloss).Where(g => !string.IsNullOrEmpty(g)).Distinct());

                entries[key] = new LexiconEntry
                {
                    Key = key,
                    Lang = group.Key.Lang,
                    Lemma = chosen.OriginalWord ?? string.Empty,
                    Transliteration = translit,
                    Gloss = chosen.Gloss ?? string.Empty,
                    FoldedScript = TranslitFolding.FoldScript(allScript),
                    FoldedTranslit = string.Join(' ', translits.Select(TranslitFolding.Latin)),
                    Skeleton = string.Join(' ', translits.Select(TranslitFolding.Skeleton)),
                    GlossWords = glossText.ToLowerInvariant().Split([' ', ',', ';', '(', ')', '/', '-', '.', '?', '!'], StringSplitOptions.RemoveEmptyEntries),
                };
            }

            _all = [.. entries.Values.OrderBy(e => e.Key, StringComparer.Ordinal)];
            _byKey = entries;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>A seed step wrote data: forget the loaded lexicon, the next request reads it again.</summary>
    public Task SeedDataChangedAsync(string stepKey, CancellationToken token)
    {
        Volatile.Write(ref _byKey, null);
        return Task.CompletedTask;
    }

    public async Task<LexiconEntry?> GetAsync(string? key, CancellationToken ct)
    {
        if (key is null) return null;
        await EnsureLoadedAsync(ct);
        return _byKey!.GetValueOrDefault(key);
    }

    /// <summary>Entries for many keys at once (missing keys are skipped).</summary>
    public async Task<IReadOnlyDictionary<string, LexiconEntry>> GetManyAsync(IEnumerable<string?> keys, CancellationToken ct)
    {
        await EnsureLoadedAsync(ct);
        var result = new Dictionary<string, LexiconEntry>();
        foreach (var k in keys)
        {
            if (k is not null && _byKey!.TryGetValue(k, out var e)) result[k] = e;
        }

        return result;
    }

    /// <summary>All matches in ranking order (best tier first), each Strong's number once, at most <paramref name="max"/>.</summary>
    public async Task<IReadOnlyList<LexiconHit>> SearchAsync(string q, int max, CancellationToken ct)
    {
        await EnsureLoadedAsync(ct);
        var hits = new List<LexiconHit>();
        var seen = new HashSet<string>();

        void Add(IEnumerable<LexiconEntry> entries, string matchedBy)
        {
            foreach (var e in entries)
            {
                if (hits.Count >= max) return;
                if (seen.Add(e.Key)) hits.Add(new LexiconHit(e, matchedBy));
            }
        }

        // 1. Strong's number
        Add(TranslitFolding.ParseStrongs(q).Select(k => _byKey!.GetValueOrDefault(k)).OfType<LexiconEntry>(), "strongs");

        var folded = TranslitFolding.FoldScript(q);
        if (folded.Length == 0) return hits;

        // 2. Greek / Hebrew script without diacritics
        if (!TranslitFolding.IsLatinInput(folded))
        {
            Add(_all.Where(e => e.FoldedScript.Split(' ').Any(w => w.StartsWith(folded, StringComparison.Ordinal))), "script");
            return hits;
        }

        // 3. folded transliteration prefix
        var latin = TranslitFolding.Latin(q);
        Add(_all.Where(e => e.FoldedTranslit.Split(' ').Any(w => w.StartsWith(latin, StringComparison.Ordinal))), "translit");
        var translitHits = hits.Count(h => h.MatchedBy == "translit");

        // 4. Hebrew consonant skeleton (dbr finds dabar)
        var skeleton = TranslitFolding.Skeleton(q);
        if (skeleton.Length >= 2 && translitHits == 0)
        {
            Add(_all.Where(e => e.Lang == "H" && e.Skeleton.Split(' ').Any(w => w.StartsWith(skeleton, StringComparison.Ordinal))), "skeleton");
        }

        // 5. English gloss word
        var word = q.Trim().ToLowerInvariant();
        if (word.Length >= 3)
        {
            Add(_all.Where(e => e.GlossWords.Contains(word)), "gloss");
        }

        return hits;
    }
}
