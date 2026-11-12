using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.DataQuality;
using UnshackledWord.Domain.Models.Grammar;
using UnshackledWord.Domain.WebApi.DataQuality;

namespace UnshackledWord.Tooling.WebApi.Endpoints.DataQuality;

/// <summary>The two explorer filters: original word (Strong's) and German lemma, plus the verse hits behind both.</summary>
public interface IDqExplorerRepository
{
    Task<OriginalSearchResponse> SearchOriginalAsync(string q, int limit, CancellationToken ct);

    Task<OriginalDetailResponse?> GetOriginalAsync(string strongs, DateTime? refreshedAt, CancellationToken ct);

    Task<GermanSearchResponse> SearchGermanAsync(string q, string? pos, int limit, CancellationToken ct);

    Task<GermanDetailResponse> GetGermanAsync(string lemma, string? pos, DateTime? refreshedAt, CancellationToken ct);

    Task<HitsResponse> GetHitsAsync(HitsRequest request, CancellationToken ct);
}

public sealed class DqExplorerRepository : IDqExplorerRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly StrongsLexiconIndex _lexicon;

    public DqExplorerRepository(IDbConnectionFactory connectionFactory, StrongsLexiconIndex lexicon)
    {
        _connectionFactory = connectionFactory;
        _lexicon = lexicon;
    }

    private const int CandidatePool = 200;
    private const int MaxForms = 50;
    private const int MaxOtherLemmas = 5;

    private sealed class OccurrenceRow
    {
        public string StrongsBase { get; init; } = string.Empty;
        public int Occurrences { get; init; }
    }

    private sealed class OriginalStatsRow
    {
        public int Occurrences { get; init; }
        public int Aligned { get; init; }
        public int Unmapped { get; init; }
        public string? OrigClass { get; init; }
    }

    private sealed class RenderingRow
    {
        public string? Lemma { get; init; }
        public string? Pos { get; init; }
        public string? OrigClass { get; init; }
        public bool? ProperNoun { get; init; }
        public int Count { get; init; }
    }

    private sealed class SourceRow
    {
        public string Key { get; init; } = string.Empty;
        public string? Lang { get; init; }
        public string? Pos { get; init; }
        public string? OrigClass { get; init; }
        public bool? ProperNoun { get; init; }
        public int Count { get; init; }
    }

    private sealed class FormRow
    {
        public string Form { get; init; } = string.Empty;
        public int Count { get; init; }
    }

    private sealed class OtherLemmaRow
    {
        public string Form { get; init; } = string.Empty;
        public string Lemma { get; init; } = string.Empty;
        public string Pos { get; init; } = string.Empty;
        public int Count { get; init; }
    }

    private sealed class CandidateRow
    {
        public string Lemma { get; init; } = string.Empty;
        public string Pos { get; init; } = string.Empty;
        public int Count { get; init; }
    }

    public async Task<OriginalSearchResponse> SearchOriginalAsync(string q, int limit, CancellationToken ct)
    {
        var hits = await _lexicon.SearchAsync(q, CandidatePool, ct);
        var response = new OriginalSearchResponse { Q = q, Folded = TranslitFolding.Fold(q) };
        if (hits.Count == 0) return response;

        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        var counts = (await connection.QueryAsync<OccurrenceRow>(new CommandDefinition($"""
            SELECT f."StrongsBase"                     AS "StrongsBase"
                 , count(DISTINCT f."StepWordId")::int AS "Occurrences"
            FROM {DqSql.Facts} f
            WHERE f."StrongsBase" = ANY(@keys)
              AND f."StepWordId" IS NOT NULL
            GROUP BY f."StrongsBase"
            """, new { keys = hits.Select(h => h.Entry.Key).ToArray() }, cancellationToken: ct))).ToDictionary(x => x.StrongsBase, x => x.Occurrences);

        var tier = hits.Select((h, i) => (h, i)).ToDictionary(x => x.h.Entry.Key, x => TierOf(x.h.MatchedBy));
        response.Candidates = [.. hits
            .Select(h => new OriginalCandidate
            {
                Strongs = h.Entry.Key,
                Lang = h.Entry.Lang,
                Lemma = h.Entry.Lemma,
                Transliteration = h.Entry.Transliteration,
                Gloss = h.Entry.Gloss,
                Occurrences = counts.GetValueOrDefault(h.Entry.Key),
                MatchedBy = h.MatchedBy,
            })
            .OrderBy(c => tier[c.Strongs])
            .ThenByDescending(c => c.Occurrences)
            .ThenBy(c => c.Strongs, StringComparer.Ordinal)
            .Take(limit)];
        return response;
    }

    private static int TierOf(string matchedBy) => matchedBy switch { "strongs" => 0, "script" => 1, "translit" => 2, "skeleton" => 3, _ => 4 };

    public async Task<OriginalDetailResponse?> GetOriginalAsync(string strongs, DateTime? refreshedAt, CancellationToken ct)
    {
        var lang = strongs[..1];
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        var stats = await connection.QuerySingleAsync<OriginalStatsRow>(new CommandDefinition($"""
            SELECT count(DISTINCT f."StepWordId")::int                                                    AS "Occurrences"
                 , (count(DISTINCT f."StepWordId") FILTER (WHERE f."Kind" = 'aligned'))::int              AS "Aligned"
                 , (count(DISTINCT f."StepWordId") FILTER (WHERE f."Kind" = 'unmappedSource'))::int       AS "Unmapped"
                 , mode() WITHIN GROUP (ORDER BY f."OrigClass")                                           AS "OrigClass"
            FROM {DqSql.Facts} f
            WHERE f."StrongsBase" = @strongs
              AND f."StepWordId" IS NOT NULL
            """, new { strongs }, cancellationToken: ct));
        var entry = await _lexicon.GetAsync(strongs, ct);
        if (stats.Occurrences == 0 && entry is null) return null;

        var rows = (await connection.QueryAsync<RenderingRow>(new CommandDefinition($"""
            SELECT mode() WITHIN GROUP (ORDER BY f."Lemma") AS "Lemma"
                 , f."Pos"                                  AS "Pos"
                 , f."OrigClass"                            AS "OrigClass"
                 , f."ProperNoun"                           AS "ProperNoun"
                 , count(*)::int                            AS "Count"
            FROM {DqSql.Facts} f
            WHERE f."StrongsBase" = @strongs
              AND f."Kind" = 'aligned'
            GROUP BY f."LemmaLower", f."Pos", f."OrigClass", f."ProperNoun"
            """, new { strongs }, cancellationToken: ct))).ToList();

        var total = rows.Sum(r => r.Count);
        var renderings = rows
            .GroupBy(r => ((r.Lemma ?? string.Empty).ToLowerInvariant(), r.Pos ?? string.Empty))
            .Select(g =>
            {
                var dominant = g.OrderByDescending(x => x.Count).First();
                var count = g.Sum(x => x.Count);
                var share = total == 0 ? 0 : (double)count / total;
                return new Rendering
                {
                    Lemma = g.Select(x => x.Lemma).FirstOrDefault(l => l is not null) ?? string.Empty,
                    Pos = g.Key.Item2,
                    Count = count,
                    Share = Math.Round(share, 4),
                    Flags = Flags(lang, g.Key.Item2, dominant.OrigClass, dominant.ProperNoun ?? false, share, count),
                };
            })
            .OrderByDescending(r => r.Count)
            .ThenBy(r => r.Lemma, StringComparer.Ordinal)
            .ToList();

        return new OriginalDetailResponse
        {
            Strongs = strongs,
            Lang = lang,
            Lemma = entry?.Lemma ?? string.Empty,
            Transliteration = entry?.Transliteration ?? string.Empty,
            Gloss = entry?.Gloss ?? string.Empty,
            OrigClass = stats.OrigClass ?? string.Empty,
            Occurrences = stats.Occurrences,
            Aligned = stats.Aligned,
            Unmapped = stats.Unmapped,
            RefreshedAt = refreshedAt,
            Renderings = renderings,
        };
    }

    /// <summary>Flags of one rendering: minority, PoS conflict, content↔function, proper noun.</summary>
    public static List<string> Flags(string lang, string pos, string? origClass, bool properNoun, double share, int count)
    {
        var flags = new List<string>();
        if (share < DataQualityThresholds.MinorityShare || count < DataQualityThresholds.OutlierMaxCount) flags.Add("minority");
        if (!PosClassRules.IsExpected(lang, pos, origClass)) flags.Add("posConflict");
        if (PosClassRules.IsContentFunctionMismatch(lang, pos, origClass)) flags.Add("contentFunction");
        if (PosClassRules.IsProperNounConflict(pos, properNoun)) flags.Add("properNoun");
        return flags;
    }

    public async Task<GermanSearchResponse> SearchGermanAsync(string q, string? pos, int limit, CancellationToken ct)
    {
        var lowered = q.Trim().ToLowerInvariant();
        var response = new GermanSearchResponse { Q = q };
        if (lowered.Length == 0) return response;

        var posFilter = string.IsNullOrEmpty(pos) ? string.Empty : " AND f.\"Pos\" = @pos";
        string Sql(string column, string op) => $"""
            SELECT mode() WITHIN GROUP (ORDER BY f."Lemma") AS "Lemma"
                 , f."Pos"                                  AS "Pos"
                 , count(*)::int                            AS "Count"
            FROM {DqSql.Facts} f
            WHERE f."GermanRowNo" = 1
              AND f."{column}" {op} @q
              AND f."LemmaLower" IS NOT NULL
              AND f."Pos" IS NOT NULL{posFilter}
            GROUP BY f."LemmaLower", f."Pos"
            ORDER BY 3 DESC, 1
            LIMIT @limit
            """;

        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        var byLemma = await connection.QueryAsync<CandidateRow>(new CommandDefinition(Sql("LemmaLower", "LIKE"), new { q = LemmaBulkRules.LikePrefix(lowered), pos, limit }, cancellationToken: ct));
        var candidates = byLemma.Select(c => new GermanCandidate { Lemma = c.Lemma, Pos = c.Pos, Count = c.Count, MatchedBy = "lemma" }).ToList();

        if (candidates.Count < limit)
        {
            var byForm = await connection.QueryAsync<CandidateRow>(new CommandDefinition(Sql("PlainLower", "LIKE"), new { q = LemmaBulkRules.LikePrefix(lowered), pos, limit }, cancellationToken: ct));
            var seen = candidates.Select(c => (c.Lemma.ToLowerInvariant(), c.Pos)).ToHashSet();
            candidates.AddRange(byForm
                .Where(c => seen.Add((c.Lemma.ToLowerInvariant(), c.Pos)))
                .Select(c => new GermanCandidate { Lemma = c.Lemma, Pos = c.Pos, Count = c.Count, MatchedBy = "form" })
                .Take(limit - candidates.Count));
        }

        response.Candidates = candidates;
        return response;
    }

    public async Task<GermanDetailResponse> GetGermanAsync(string lemma, string? pos, DateTime? refreshedAt, CancellationToken ct)
    {
        var lowered = lemma.Trim().ToLowerInvariant();
        var posFilter = string.IsNullOrEmpty(pos) ? string.Empty : " AND f.\"Pos\" = @pos";
        var args = new { lemma = lowered, pos };
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);

        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition($"""
            SELECT count(*)::int
            FROM {DqSql.Facts} f
            WHERE f."GermanRowNo" = 1
              AND f."LemmaLower" = @lemma{posFilter}
            """, args, cancellationToken: ct));

        var rows = (await connection.QueryAsync<SourceRow>(new CommandDefinition($"""
            SELECT CASE WHEN f."Kind" = 'aligned' THEN COALESCE(f."StrongsBase", 'none') WHEN f."Kind" = 'added' THEN 'added' ELSE 'none' END AS "Key"
                 , f."Lang"          AS "Lang"
                 , f."Pos"           AS "Pos"
                 , f."OrigClass"     AS "OrigClass"
                 , f."ProperNoun"    AS "ProperNoun"
                 , count(*)::int     AS "Count"
            FROM {DqSql.Facts} f
            WHERE f."LemmaLower" = @lemma{posFilter}
              AND f."ElbWordId" IS NOT NULL
            GROUP BY 1, f."Lang", f."Pos", f."OrigClass", f."ProperNoun"
            """, args, cancellationToken: ct))).ToList();

        var total = rows.Sum(r => r.Count);
        var lexicon = await _lexicon.GetManyAsync(rows.Select(r => r.Key), ct);
        var sources = rows
            .GroupBy(r => r.Key)
            .Select(g =>
            {
                var dominant = g.OrderByDescending(x => x.Count).First();
                var n = g.Sum(x => x.Count);
                var share = total == 0 ? 0 : (double)n / total;
                var special = g.Key is "added" or "none";
                lexicon.TryGetValue(g.Key, out var entry);
                return new GermanSource
                {
                    Key = g.Key,
                    Lang = special ? null : dominant.Lang,
                    Lemma = entry?.Lemma,
                    Transliteration = entry?.Transliteration,
                    Gloss = entry?.Gloss,
                    OrigClass = special ? null : dominant.OrigClass,
                    Count = n,
                    Share = Math.Round(share, 4),
                    Flags = special ? [] : Flags(dominant.Lang ?? "G", dominant.Pos ?? string.Empty, dominant.OrigClass, dominant.ProperNoun ?? false, share, n),
                };
            })
            .OrderByDescending(s => s.Count)
            .ThenBy(s => s.Key, StringComparer.Ordinal)
            .ToList();

        var forms = (await connection.QueryAsync<FormRow>(new CommandDefinition($"""
            SELECT f."PlainLower"  AS "Form"
                 , count(*)::int   AS "Count"
            FROM {DqSql.Facts} f
            WHERE f."GermanRowNo" = 1
              AND f."LemmaLower" = @lemma{posFilter}
            GROUP BY f."PlainLower"
            ORDER BY 2 DESC, 1
            LIMIT {MaxForms}
            """, args, cancellationToken: ct))).ToList();

        var others = forms.Count == 0
            ? []
            : (await connection.QueryAsync<OtherLemmaRow>(new CommandDefinition($"""
                SELECT f."PlainLower"                              AS "Form"
                     , mode() WITHIN GROUP (ORDER BY f."Lemma")    AS "Lemma"
                     , f."Pos"                                     AS "Pos"
                     , count(*)::int                               AS "Count"
                FROM {DqSql.Facts} f
                WHERE f."GermanRowNo" = 1
                  AND f."PlainLower" = ANY(@forms)
                  AND f."LemmaLower" IS NOT NULL
                  AND f."Pos" IS NOT NULL
                  AND NOT (f."LemmaLower" = @lemma{posFilter})
                GROUP BY f."PlainLower", f."LemmaLower", f."Pos"
                """, new { lemma = lowered, pos, forms = forms.Select(x => x.Form).ToArray() }, cancellationToken: ct))).ToList();

        var display = await connection.ExecuteScalarAsync<string?>(new CommandDefinition($"""
            SELECT mode() WITHIN GROUP (ORDER BY f."Lemma")
            FROM {DqSql.Facts} f
            WHERE f."GermanRowNo" = 1
              AND f."LemmaLower" = @lemma{posFilter}
            """, args, cancellationToken: ct));

        return new GermanDetailResponse
        {
            Lemma = display ?? lemma,
            Pos = string.IsNullOrEmpty(pos) ? null : pos,
            Count = count,
            RefreshedAt = refreshedAt,
            Sources = sources,
            Forms = [.. forms.Select(f => new GermanForm
            {
                Form = f.Form,
                Count = f.Count,
                OtherLemmas = [.. others.Where(o => o.Form == f.Form).OrderByDescending(o => o.Count).Take(MaxOtherLemmas).Select(o => new OtherLemma { Lemma = o.Lemma, Pos = o.Pos, Count = o.Count })],
            })],
        };
    }

    public async Task<HitsResponse> GetHitsAsync(HitsRequest r, CancellationToken ct)
    {
        var p = new DynamicParameters();
        var where = "f.\"ElbWordId\" IS NOT NULL";
        if (r.Strongs is "added" or "none")
        {
            where += " AND f.\"Kind\" = @kind";
            p.Add("kind", r.Strongs);
        }
        else if (!string.IsNullOrEmpty(r.Strongs))
        {
            where += " AND f.\"Kind\" = 'aligned' AND f.\"StrongsBase\" = @strongs";
            p.Add("strongs", r.Strongs);
        }

        if (!string.IsNullOrEmpty(r.Lemma))
        {
            where += " AND f.\"LemmaLower\" = @lemma";
            p.Add("lemma", r.Lemma.Trim().ToLowerInvariant());
        }

        if (!string.IsNullOrEmpty(r.Pos))
        {
            where += " AND f.\"Pos\" = @pos";
            p.Add("pos", r.Pos);
        }

        p.Add("take", r.PageSize);
        p.Add("skip", (r.Page - 1) * r.PageSize);

        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(DqSql.CountFindings(string.Empty, where), p, cancellationToken: ct));
        var rows = (await connection.QueryAsync<FactRow>(new CommandDefinition(DqSql.PagedFindings(string.Empty, where), p, cancellationToken: ct))).ToList();
        var lexicon = await _lexicon.GetManyAsync(FindingFactory.StrongsOf(rows), ct);
        return new HitsResponse
        {
            Total = total,
            Page = r.Page,
            PageSize = r.PageSize,
            Items = [.. rows.Select(row => FindingFactory.Create("X", row, lexicon))],
        };
    }
}
