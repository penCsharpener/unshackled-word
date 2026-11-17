using System.Text.Json;
using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.DataQuality;
using UnshackledWord.Domain.WebApi.DataQuality;

namespace UnshackledWord.Tooling.WebApi.Endpoints.DataQuality;

public sealed class DqState
{
    public DateTime? RefreshedAt { get; init; }
    public DateTime? PreviousRefreshedAt { get; init; }
    public DateTime? StatsRefreshedAt { get; init; }
    public DateTime? LinkRolesAppliedAt { get; init; }
    public string? Counts { get; init; }
    public string? PreviousCounts { get; init; }
    public bool IsPopulated { get; init; }
}

/// <summary>Snapshot bookkeeping, refresh, totals, coverage and the unmapped panels (everything that reads DqWordFacts in bulk).</summary>
public interface IDqFactsRepository
{
    Task<DqState> GetStateAsync(CancellationToken ct);

    Task<DateTime?> GetLastEditAtAsync(CancellationToken ct);

    /// <summary>Latest <c>SeedImports</c> row per kind, and per kind the latest update row when it is not that one.</summary>
    Task<IReadOnlyList<SeedImportInfo>> GetSeedImportsAsync(CancellationToken ct);

    /// <summary>Refreshes the materialized view (concurrently once populated); the timestamp is set by <see cref="SaveCountsAsync"/>.</summary>
    Task RefreshViewAsync(CancellationToken ct);

    Task SaveCountsAsync(IReadOnlyDictionary<string, int> counts, CancellationToken ct);

    /// <summary>Refreshes the four Stats* views in dependency-free order on one connection (<see cref="DqSql.StatsViews"/>): plain while a view is unpopulated, CONCURRENTLY afterwards. They read DqWordFacts, so call this after <see cref="RefreshViewAsync"/>.</summary>
    Task RefreshStatsViewsAsync(CancellationToken ct);

    /// <summary>Stamps DqRefreshState.StatsRefreshedAt with now(); only after <see cref="RefreshStatsViewsAsync"/> succeeded.</summary>
    Task SaveStatsRefreshedAtAsync(CancellationToken ct);

    Task<OverviewTotals> GetTotalsAsync(DqFacets facets, CancellationToken ct);

    Task<IReadOnlyList<CoverageBook>> GetCoverageAsync(string metric, DqFacets facets, CancellationToken ct);

    Task<UnmappedResponse> GetUnmappedAsync(string side, bool content, int top, DqFacets facets, CancellationToken ct);
}

public sealed class DqFactsRepository : IDqFactsRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly StrongsLexiconIndex _lexicon;

    public DqFactsRepository(IDbConnectionFactory connectionFactory, StrongsLexiconIndex lexicon)
    {
        _connectionFactory = connectionFactory;
        _lexicon = lexicon;
    }

    private const int RefreshTimeoutSeconds = 900;

    private const string SelectState = """
        SELECT s."RefreshedAt"            AS "RefreshedAt"
             , s."PreviousRefreshedAt"    AS "PreviousRefreshedAt"
             , s."StatsRefreshedAt"       AS "StatsRefreshedAt"
             , s."LinkRolesAppliedAt"     AS "LinkRolesAppliedAt"
             , s."Counts"::text           AS "Counts"
             , s."PreviousCounts"::text   AS "PreviousCounts"
             , COALESCE((SELECT v.ispopulated FROM pg_matviews v WHERE v.schemaname = 'unshackled-word' AND v.matviewname = 'DqWordFacts'), false) AS "IsPopulated"
        FROM "unshackled-word"."DqRefreshState" s
        WHERE s."Id" = 1
        """;

    private const string SelectLastEdit = """
        SELECT max(e."EditedAt")
        FROM "unshackled-word"."Elb1871MorphologyEdits" e
        """;

    private const string SelectSeedImports = """
        SELECT DISTINCT ON (s."Kind", (s."VersesUpdated" IS NOT NULL))
            s."Kind", s."SeedVersion", s."ExportedAt", s."ImportedAt", s."Rows", s."Orphaned", s."VersesUpdated", s."VersesKeptLocal"
        FROM "unshackled-word"."SeedImports" s
        ORDER BY s."Kind", (s."VersesUpdated" IS NOT NULL), s."ImportedAt" DESC, s."Id" DESC
        """;

    private const string UpdateCounts = """
        UPDATE "unshackled-word"."DqRefreshState"
        SET "PreviousRefreshedAt" = "RefreshedAt"
          , "PreviousCounts" = "Counts"
          , "RefreshedAt" = now()
          , "Counts" = @counts::jsonb
        WHERE "Id" = 1
        """;

    private const string UpdateStatsRefreshedAt = """
        UPDATE "unshackled-word"."DqRefreshState"
        SET "StatsRefreshedAt" = now()
        WHERE "Id" = 1
        """;

    private const string SelectPopulatedStatsViews = """
        SELECT v.matviewname
        FROM pg_matviews v
        WHERE v.schemaname = 'unshackled-word'
          AND v.matviewname = ANY(@names)
          AND v.ispopulated
        """;

    public async Task<DqState> GetStateAsync(CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        return await connection.QuerySingleAsync<DqState>(new CommandDefinition(SelectState, cancellationToken: ct));
    }

    public async Task<DateTime?> GetLastEditAtAsync(CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<DateTime?>(new CommandDefinition(SelectLastEdit, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<SeedImportInfo>> GetSeedImportsAsync(CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        var rows = (await connection.QueryAsync<SeedImportInfo>(new CommandDefinition(SelectSeedImports, cancellationToken: ct))).ToList();
        foreach (var row in rows)
        {
            var version = row.SeedVersion.Length >= 12 ? row.SeedVersion[..12] : row.SeedVersion;
            row.ReportPath = row.VersesUpdated is null
                ? $"temp/seed-report/{row.Kind}-{version}.csv"
                : $"temp/seed-report/{row.Kind}-update-{version}.csv";
        }

        return rows;
    }

    public async Task RefreshViewAsync(CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        var state = await connection.QuerySingleAsync<DqState>(new CommandDefinition(SelectState, cancellationToken: ct));
        var sql = state.IsPopulated
            ? "REFRESH MATERIALIZED VIEW CONCURRENTLY \"unshackled-word\".\"DqWordFacts\""
            : "REFRESH MATERIALIZED VIEW \"unshackled-word\".\"DqWordFacts\"";
        await connection.ExecuteAsync(new CommandDefinition(sql, commandTimeout: RefreshTimeoutSeconds, cancellationToken: ct));
    }

    public async Task RefreshStatsViewsAsync(CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        var populated = (await connection.QueryAsync<string>(new CommandDefinition(SelectPopulatedStatsViews, new { names = DqSql.StatsViews.ToArray() }, cancellationToken: ct))).ToHashSet();
        foreach (var sql in DqSql.StatsRefreshStatements(populated))
        {
            await connection.ExecuteAsync(new CommandDefinition(sql, commandTimeout: RefreshTimeoutSeconds, cancellationToken: ct));
        }
    }

    public async Task SaveStatsRefreshedAtAsync(CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(UpdateStatsRefreshedAt, cancellationToken: ct));
    }

    public async Task SaveCountsAsync(IReadOnlyDictionary<string, int> counts, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(UpdateCounts, new { counts = JsonSerializer.Serialize(counts) }, cancellationToken: ct));
    }

    public async Task<OverviewTotals> GetTotalsAsync(DqFacets facets, CancellationToken ct)
    {
        var sql = $"""
            SELECT (count(*) FILTER (WHERE f."GermanRowNo" = 1))::int  AS "GermanWords"
                 , (count(DISTINCT f."StepWordId") FILTER (WHERE f."Lang" = 'G') + count(DISTINCT f."StepWordId") FILTER (WHERE f."Lang" = 'H'))::int AS "SourceWords"
            FROM {DqSql.Facts} f
            WHERE true{facets.Predicate}
            """;
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        return await connection.QuerySingleAsync<OverviewTotals>(new CommandDefinition(sql, cancellationToken: ct));
    }

    private sealed class CoverageRow
    {
        public int BibleBookId { get; init; }
        public int Chapter { get; init; }
        public int Total { get; init; }
        public int Unaligned { get; init; }
    }

    public async Task<IReadOnlyList<CoverageBook>> GetCoverageAsync(string metric, DqFacets facets, CancellationToken ct)
    {
        var sourceContent = $"""
            (f."Lang" = 'G' AND ({UnmappedRules.SourceClassKeySql}) IN ({DqSql.InList(UnmappedRules.ErrorKeys("G"))}))
            OR (f."Lang" = 'H' AND ({UnmappedRules.SourceClassKeySql}) IN ({DqSql.InList(UnmappedRules.ErrorKeys("H"))}))
            """;
        var sql = metric == "source"
            ? $"""
                SELECT f."BibleBookId"                                                                 AS "BibleBookId"
                     , f."Chapter"                                                                     AS "Chapter"
                     , count(DISTINCT f."StepWordId")::int                                             AS "Total"
                     , (count(DISTINCT f."StepWordId") FILTER (WHERE f."Kind" = 'unmappedSource'))::int AS "Unaligned"
                FROM {DqSql.Facts} f
                WHERE f."StepWordId" IS NOT NULL
                  AND ({sourceContent}){facets.Predicate}
                GROUP BY f."BibleBookId", f."Chapter"
                ORDER BY f."BibleBookId", f."Chapter"
                """
            : $"""
                SELECT f."BibleBookId"                                                    AS "BibleBookId"
                     , f."Chapter"                                                        AS "Chapter"
                     , count(*)::int                                                      AS "Total"
                     , (count(*) FILTER (WHERE f."Kind" IN ('added', 'none')))::int        AS "Unaligned"
                FROM {DqSql.Facts} f
                WHERE f."GermanRowNo" = 1
                  AND f."Pos" IN ({DqSql.ContentPosList}){facets.Predicate}
                GROUP BY f."BibleBookId", f."Chapter"
                ORDER BY f."BibleBookId", f."Chapter"
                """;

        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        var rows = await connection.QueryAsync<CoverageRow>(new CommandDefinition(sql, commandTimeout: 120, cancellationToken: ct));
        return [.. rows.GroupBy(r => r.BibleBookId).Select(g => new CoverageBook
        {
            BibleBookId = g.Key,
            Chapters = [.. g.Select(r => new CoverageChapter { Chapter = r.Chapter, Total = r.Total, Unaligned = r.Unaligned })],
        })];
    }

    private sealed class ClassRow
    {
        public string Key { get; init; } = string.Empty;
        public int Count { get; init; }
    }

    private sealed class TopRow
    {
        public string Key { get; init; } = string.Empty;
        public string? Label { get; init; }
        public string? Pos { get; init; }
        public int Count { get; init; }
    }

    public async Task<UnmappedResponse> GetUnmappedAsync(string side, bool content, int top, DqFacets facets, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        return side == "german"
            ? await GetGermanUnmappedAsync(connection, content, top, facets, ct)
            : await GetSourceUnmappedAsync(connection, side == "greek" ? "G" : "H", side, content, top, facets, ct);
    }

    private async Task<UnmappedResponse> GetGermanUnmappedAsync(System.Data.IDbConnection connection, bool content, int top, DqFacets facets, CancellationToken ct)
    {
        var scope = $"f.\"Kind\" IN ('added', 'none'){facets.Predicate}";
        var contentFilter = content ? $" AND {DqSql.GermanErrorPredicate}" : string.Empty;

        var classes = (await connection.QueryAsync<ClassRow>(new CommandDefinition($"""
            SELECT CASE WHEN f."Kind" = 'none' THEN 'none' ELSE COALESCE(f."Pos", '?') END AS "Key"
                 , count(*)::int AS "Count"
            FROM {DqSql.Facts} f
            WHERE {scope}
            GROUP BY 1
            ORDER BY 2 DESC
            """, cancellationToken: ct))).ToList();

        var books = (await connection.QueryAsync<BookCount>(new CommandDefinition($"""
            SELECT f."BibleBookId" AS "BibleBookId"
                 , count(*)::int   AS "Count"
            FROM {DqSql.Facts} f
            WHERE {scope}{contentFilter}
            GROUP BY f."BibleBookId"
            ORDER BY f."BibleBookId"
            """, cancellationToken: ct))).ToList();

        var tops = (await connection.QueryAsync<TopRow>(new CommandDefinition($"""
            SELECT f."LemmaLower" || '~' || f."Pos"  AS "Key"
                 , mode() WITHIN GROUP (ORDER BY f."Lemma") AS "Label"
                 , f."Pos"                           AS "Pos"
                 , count(*)::int                     AS "Count"
            FROM {DqSql.Facts} f
            WHERE {scope}{contentFilter}
              AND f."LemmaLower" IS NOT NULL
              AND f."Pos" IS NOT NULL
            GROUP BY f."LemmaLower", f."Pos"
            ORDER BY 4 DESC, 1
            LIMIT @top
            """, new { top }, cancellationToken: ct))).ToList();

        return new UnmappedResponse
        {
            Side = "german",
            Total = classes.Sum(c => c.Count),
            ByClass = [.. classes.Select(c => new UnmappedClassCount
            {
                Key = c.Key,
                Label = GermanPosLabel(c.Key),
                Count = c.Count,
                Legit = c.Key != "?" && UnmappedRules.GermanSeverity(c.Key, c.Key == "none" ? "none" : "added") == UnmappedRules.Info,
            })],
            ByBook = books,
            Top = [.. tops.Select(t => new UnmappedTop { Key = t.Key, Label = t.Label ?? t.Key, Pos = t.Pos, Count = t.Count })],
        };
    }

    private async Task<UnmappedResponse> GetSourceUnmappedAsync(System.Data.IDbConnection connection, string lang, string side, bool content, int top, DqFacets facets, CancellationToken ct)
    {
        var scope = $"f.\"Kind\" = 'unmappedSource' AND f.\"Lang\" = '{lang}'{facets.Predicate}";
        var contentFilter = content ? $" AND ({UnmappedRules.SourceClassKeySql}) IN ({DqSql.InList(UnmappedRules.ErrorKeys(lang))})" : string.Empty;

        var classes = (await connection.QueryAsync<ClassRow>(new CommandDefinition($"""
            SELECT {UnmappedRules.SourceClassKeySql} AS "Key"
                 , count(*)::int AS "Count"
            FROM {DqSql.Facts} f
            WHERE {scope}
            GROUP BY 1
            ORDER BY 2 DESC
            """, cancellationToken: ct))).ToList();

        var books = (await connection.QueryAsync<BookCount>(new CommandDefinition($"""
            SELECT f."BibleBookId" AS "BibleBookId"
                 , count(*)::int   AS "Count"
            FROM {DqSql.Facts} f
            WHERE {scope}{contentFilter}
            GROUP BY f."BibleBookId"
            ORDER BY f."BibleBookId"
            """, cancellationToken: ct))).ToList();

        var tops = (await connection.QueryAsync<TopRow>(new CommandDefinition($"""
            SELECT f."StrongsBase"  AS "Key"
                 , count(*)::int    AS "Count"
            FROM {DqSql.Facts} f
            WHERE {scope}{contentFilter}
              AND f."StrongsBase" IS NOT NULL
            GROUP BY f."StrongsBase"
            ORDER BY 2 DESC, 1
            LIMIT @top
            """, new { top }, cancellationToken: ct))).ToList();

        var lexicon = await _lexicon.GetManyAsync(tops.Select(t => t.Key), ct);
        return new UnmappedResponse
        {
            Side = side,
            Total = classes.Sum(c => c.Count),
            ByClass = [.. classes.Select(c =>
            {
                var d = UnmappedRules.DescribeSource(lang, c.Key);
                return new UnmappedClassCount { Key = c.Key, Label = SourceClassLabel(lang, c.Key), Count = c.Count, Legit = d.Legit };
            })],
            ByBook = books,
            Top = [.. tops.Select(t =>
            {
                lexicon.TryGetValue(t.Key, out var e);
                return new UnmappedTop
                {
                    Key = t.Key,
                    Label = e is null ? t.Key : $"{t.Key} {e.Lemma}",
                    Original = e?.Lemma,
                    Transliteration = e?.Transliteration,
                    Gloss = e?.Gloss,
                    Count = t.Count,
                };
            })],
        };
    }

    private static string GermanPosLabel(string key) => key switch
    {
        "none" => "ohne Zuordnungseintrag",
        "NOUN" => "Nomen",
        "PROPN" => "Eigenname",
        "VERB" => "Verb",
        "ADJ" => "Adjektiv",
        "ADV" => "Adverb",
        "DET" => "Artikel / Determinativ",
        "PRON" => "Pronomen",
        "ADP" => "Präposition",
        "CCONJ" => "Konjunktion (nebenordnend)",
        "SCONJ" => "Konjunktion (unterordnend)",
        "AUX" => "Hilfsverb",
        "PART" => "Partikel",
        "NUM" => "Zahlwort",
        "INTJ" => "Interjektion",
        _ => key,
    };

    private static string SourceClassLabel(string lang, string key)
    {
        switch (key)
        {
            case UnmappedRules.NotInNaTr: return "nicht in NA/TR";
            case UnmappedRules.ObjectMarker: return "Objektzeichen (אֵת)";
            case UnmappedRules.Ketiv: return "Ketiv/Qere";
        }

        var greek = new Dictionary<string, string>
        {
            ["T"] = "Artikel", ["N"] = "Nomen", ["V"] = "Verb", ["A"] = "Adjektiv", ["ADV"] = "Adverb", ["CONJ"] = "Konjunktion",
            ["PREP"] = "Präposition", ["PRT"] = "Partikel", ["COND"] = "Konditionalpartikel", ["P"] = "Personalpronomen", ["R"] = "Relativpronomen",
            ["D"] = "Demonstrativpronomen", ["F"] = "Reflexivpronomen", ["S"] = "Possessivpronomen", ["I"] = "Interrogativpronomen", ["X"] = "Indefinitpronomen",
            ["INJ"] = "Interjektion",
        };
        var hebrew = new Dictionary<string, string>
        {
            ["N"] = "Nomen", ["V"] = "Verb", ["A"] = "Adjektiv", ["D"] = "Adverb", ["C"] = "Konjunktion", ["R"] = "Präposition", ["P"] = "Pronomen",
            ["T"] = "Partikel", ["S"] = "Suffix",
        };
        return (lang == "G" ? greek : hebrew).GetValueOrDefault(key, key);
    }
}
