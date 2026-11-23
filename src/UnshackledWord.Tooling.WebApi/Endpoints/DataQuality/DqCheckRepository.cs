using System.Text;
using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.DataQuality;
using UnshackledWord.Domain.WebApi.DataQuality;

namespace UnshackledWord.Tooling.WebApi.Endpoints.DataQuality;

public static class DqChecks
{
    /// <summary>Must-tier checks served by checks/{code} and listed in the overview, in tile order.</summary>
    public static readonly string[] Codes = ["U1", "U2", "U3", "C1", "M1", "M2", "M3", "M4", "L1", "L2", "L3", "L4", "L5", "L6", "L7", .. DqTskChecks.Codes, .. DqRoleChecks.Codes];

    public static bool IsKnown(string code) => Codes.Contains(code);
}

public sealed record CheckCount(int Count, int ErrorCount, KeyCount? Sub);

public interface IDqCheckRepository
{
    Task<CheckResponse> GetAsync(CheckRequest request, DateTime? refreshedAt, CancellationToken ct);

    Task<CheckCount> CountAsync(string code, DqFacets facets, CancellationToken ct);
}

public sealed class DqCheckRepository : IDqCheckRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly StrongsLexiconIndex _lexicon;
    private readonly IDqFactsRepository _facts;

    public DqCheckRepository(IDbConnectionFactory connectionFactory, StrongsLexiconIndex lexicon, IDqFactsRepository facts)
    {
        _connectionFactory = connectionFactory;
        _lexicon = lexicon;
        _facts = facts;
    }

    private const int GroupTimeoutSeconds = 120;

    /// <summary>L6 class value for words without an STTS tag.</summary>
    public const string EmptyStts = "-";

    private const string MorphologyJoin = """
        JOIN "unshackled-word"."Elb1871Morphology" m ON m."HebRefId" = f."HebRefId" AND m."PositionInVerse" = f."PositionInVerse"
        """;

    private const string SttsUnknown = """
        (m."Stts" IS NULL OR NOT EXISTS (SELECT 1 FROM "unshackled-word"."SttsTags" s WHERE s."Tag" = m."Stts"))
        """;

    private const string SttsMismatch = """
        (m."Stts" IS NOT NULL AND EXISTS (SELECT 1 FROM "unshackled-word"."SttsTags" s WHERE s."Tag" = m."Stts" AND m."PartOfSpeech" <> ALL(s."AllowedUpos")))
        """;

    // German PoS vs original class not in the expected table (exempt: PoS X, no class).
    private const string Mismatch = """
        (f."OrigClass" IS NOT NULL AND f."Pos" <> 'X' AND (f."Lang" || '|' || f."Pos" || '|' || f."OrigClass") <> ALL(@expected))
        """;

    private static readonly string ContentFunction = """
        ((f."Lang" || '|' || f."Pos" || '|' || f."OrigClass") = ANY(@contentFunction))
        """;

    // The shared paging wrapper of aggregated checks: inner returns Key, Label, Lang, Count, Total, Minority.
    private static string GroupPage(string ctes, string inner) => $"""
        {ctes}
        SELECT g.*
             , count(*) OVER () AS "GroupTotal"
        FROM ({inner}) g
        WHERE g."Count" >= @minCount
        ORDER BY g."Count" DESC, g."Key"
        LIMIT @take OFFSET @skip
        """;

    private static string GroupCount(string ctes, string inner) => $"""
        {ctes}
        SELECT count(*)
        FROM ({inner}) g
        WHERE g."Count" >= @minCount
        """;

    private sealed class GroupRow
    {
        public string Key { get; init; } = string.Empty;
        public string Label { get; init; } = string.Empty;
        public string? Lang { get; init; }
        public int Count { get; init; }
        public int? Total { get; init; }
        public bool Minority { get; init; }
        public int GroupTotal { get; init; }
    }

    private sealed class MatrixRow
    {
        public string Pos { get; init; } = string.Empty;
        public string Lang { get; init; } = string.Empty;
        public string OrigClass { get; init; } = string.Empty;
        public int Count { get; init; }
    }

    private sealed class MorphCombo
    {
        public string? Pos { get; init; }
        public string? VerbForm { get; init; }
        public string? Degree { get; init; }
        public string? Tense { get; init; }
        public string? Person { get; init; }
        public string? Number { get; init; }
        public string? Mood { get; init; }
        public string? Case { get; init; }
        public string? Gender { get; init; }
        public int Count { get; init; }

        public MorphologyValues Values => new(Pos, VerbForm, Degree, Tense, Person, Number, Mood, Case, Gender);
    }

    /// <summary>Everything one check needs: predicate for its items, optional aggregation.</summary>
    private sealed class Spec
    {
        public string Ctes { get; set; } = string.Empty;
        public string ItemsWhere { get; set; } = "true";
        public string? GroupInner { get; set; }
        public string Join { get; set; } = string.Empty;
    }

    private static void AddCommon(DynamicParameters p, CheckRequest r)
    {
        p.Add("expected", PosClassRules.ExpectedKeys());
        p.Add("contentFunction", PosClassRules.ContentFunctionKeys());
        p.Add("allowedPairs", PosClassRules.AllowedPosPairs());
        p.Add("minCount", r.MinCount ?? 0);
        p.Add("take", r.PageSize);
        p.Add("skip", (r.Page - 1) * r.PageSize);
        p.Add("minority", DataQualityThresholds.MinorityShare);
        p.Add("keyShare", DataQualityThresholds.LemmaKeyShare);
        p.Add("minOcc", DataQualityThresholds.LemmaMinOccurrences);
        p.Add("maxCount", DataQualityThresholds.OutlierMaxCount);
        p.Add("formMinority", DataQualityThresholds.FormMinorityShare);
        p.Add("lemmaSetMin", DataQualityThresholds.LemmaSetMinCount);
    }

    private static string Filters(CheckRequest r, DynamicParameters p)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(r.Pos))
        {
            sb.Append(" AND f.\"Pos\" = @pos");
            p.Add("pos", r.Pos);
        }

        if (!string.IsNullOrEmpty(r.Lang))
        {
            sb.Append(" AND f.\"Lang\" = @lang");
            p.Add("lang", r.Lang);
        }

        if (r.Chapter is { } chapter)
        {
            sb.Append(" AND f.\"Chapter\" = @chapter");
            p.Add("chapter", chapter);
        }

        return sb.ToString();
    }

    /// <summary>M3 class parameter: "G:N", "H:N" or "N" (with lang).</summary>
    public static (string? Lang, string? Class) SplitClass(string? cls, string? lang)
    {
        if (string.IsNullOrEmpty(cls)) return (lang, null);
        var parts = cls.Split(':', 2);
        return parts.Length == 2 ? (parts[0], parts[1]) : (lang, cls);
    }

    private Spec BuildSpec(CheckRequest r, DqFacets facets, DynamicParameters p)
    {
        var f = facets.Predicate;
        var filters = Filters(r, p);
        var group = r.Group;
        string? gLemma = null, gPos = null;
        if (group is not null)
        {
            var parts = group.Split('~', 2);
            gLemma = parts[0];
            gPos = parts.Length > 1 ? parts[1] : null;
            p.Add("gLemma", gLemma);
            p.Add("gPos", gPos);
            p.Add("gKey", group);
        }

        switch (r.Code)
        {
            case "U1":
            {
                // class = the unmapped/german class key: UPOS, "?" (no PoS) or "none" (no mapping row at all).
                var where = $"f.\"Kind\" IN ('added', 'none'){f}{filters}";
                if (r.Class == "none") where += " AND f.\"Kind\" = 'none'";
                else if (r.Class == "?") where += " AND f.\"Kind\" <> 'none' AND f.\"Pos\" IS NULL";
                else if (!string.IsNullOrEmpty(r.Class))
                {
                    where += " AND f.\"Kind\" <> 'none' AND f.\"Pos\" = @ucls";
                    p.Add("ucls", r.Class);
                }

                return new Spec { ItemsWhere = where };
            }
            case "U2":
            case "U3":
            {
                var lang = r.Code == "U2" ? "G" : "H";
                var where = $"f.\"Kind\" = 'unmappedSource' AND f.\"Lang\" = '{lang}'{f}";
                if (!string.IsNullOrEmpty(r.Class))
                {
                    where += $" AND ({UnmappedRules.SourceClassKeySql}) = @cls";
                    p.Add("cls", r.Class);
                }

                if (!string.IsNullOrEmpty(r.Pos)) where += " AND f.\"OrigClass\" = @pos";
                if (r.Chapter is not null) where += " AND f.\"Chapter\" = @chapter";
                return new Spec { ItemsWhere = where };
            }
            case "M3":
            {
                var (lang, cls) = SplitClass(r.Class, r.Lang);
                var where = $"{DqSql.AlignedUsable}{f}";
                if (!string.IsNullOrEmpty(r.Pos)) where += " AND f.\"Pos\" = @pos";
                if (lang is not null) where += " AND f.\"Lang\" = @lang";
                if (r.Chapter is not null) where += " AND f.\"Chapter\" = @chapter";
                if (cls is not null)
                {
                    where += " AND f.\"OrigClass\" = @cls";
                    p.Add("cls", cls);
                }

                if (lang is not null) p.Add("lang", lang);
                if (cls is null || string.IsNullOrEmpty(r.Pos)) where += $" AND {Mismatch}";
                return new Spec { ItemsWhere = where };
            }
            case "M4":
                return new Spec { ItemsWhere = $"{DqSql.AlignedUsable}{f}{filters} AND {ContentFunction}" };
            case "L1":
                return new Spec { ItemsWhere = $"f.\"GermanRowNo\" = 1 AND (NOT f.\"HasMorphology\" OR f.\"Lemma\" IS NULL OR f.\"Pos\" IS NULL){f}{filters}" };
            case "M1":
                return BuildM1(f, filters, group is not null, r.Lang, p);
            case "M2":
                return BuildM2(f, filters, group is not null);
            case "L2":
                return BuildL2(f, filters, group is not null);
            case "L3":
                return BuildL3(f, group is not null);
            case "L4":
                return BuildL4(f, group is not null);
            case "L5":
                return new Spec { ItemsWhere = $"f.\"GermanRowNo\" = 1 AND f.\"HasMorphology\"{f}{filters}" };
            case "L6":
            case "L7":
                return BuildStts(r, f, p);
            case "R1":
            case "R2":
            case "R3":
            case "R4":
            case "R5":
            case "R6":
                return BuildRole(r.Code, f, filters, p);
            default:
                throw new ArgumentOutOfRangeException(nameof(r), r.Code, "Unknown check");
        }
    }

    private static Spec BuildRole(string code, string f, string filters, DynamicParameters p)
    {
        var (ctes, where, join) = DqRoleChecks.Build(code, f);
        if (code == "R6") p.Add("legacy", DqRoleChecks.LegacySpellings);
        return new Spec { Ctes = ctes, ItemsWhere = $"{where}{f}{filters}", Join = join };
    }

    private static Spec BuildM1(string f, string filters, bool hasGroup, string? lang, DynamicParameters p)
    {
        var inner = $"""
            SELECT x."LemmaLower" || '~' || x."Pos"                       AS "Key"
                 , max(x."Lemma") || ' (' || x."Pos" || ')'               AS "Label"
                 , x."Lang"                                                AS "Lang"
                 , (sum(x.c) - max(x.c))::int                              AS "Count"
                 , sum(x.c)::int                                           AS "Total"
                 , (min(x.c) < @minority * sum(x.c))                       AS "Minority"
            FROM (SELECT s.*
                       , sum(s.c) OVER (PARTITION BY s."LemmaLower", s."Pos", s."Lang") AS tot
                  FROM (SELECT f."LemmaLower"
                             , f."Pos"
                             , f."Lang"
                             , f."StrongsBase"
                             , mode() WITHIN GROUP (ORDER BY f."Lemma") AS "Lemma"
                             , count(*) AS c
                        FROM {DqSql.Facts} f
                        WHERE {DqSql.AlignedUsable}{f}{filters}
                        GROUP BY f."LemmaLower", f."Pos", f."Lang", f."StrongsBase") s) x
            GROUP BY x."LemmaLower", x."Pos", x."Lang"
            HAVING sum(x.c) >= @minOcc
               AND count(*) FILTER (WHERE x.c >= @keyShare * x.tot) >= 2
            """;
        var langFilter = lang is null ? string.Empty : " AND f.\"Lang\" = @lang";
        if (lang is not null) p.Add("lang", lang);
        var topKey = $"""
            WITH "TopKey" AS MATERIALIZED
            (
                SELECT DISTINCT ON (t."Lang") t."Lang", t."StrongsBase"
                FROM (SELECT f."Lang", f."StrongsBase", count(*) AS c
                      FROM {DqSql.Facts} f
                      WHERE f."Kind" = 'aligned'
                        AND f."LemmaLower" = @gLemma
                        AND f."Pos" = @gPos
                        AND f."StrongsBase" IS NOT NULL
                      GROUP BY f."Lang", f."StrongsBase") t
                ORDER BY t."Lang", t.c DESC, t."StrongsBase"
            )
            """;
        var items = $"""
            {DqSql.AlignedUsable}{f}
            AND f."LemmaLower" = @gLemma
            AND f."Pos" = @gPos{langFilter}
            AND EXISTS (SELECT 1 FROM "TopKey" tk WHERE tk."Lang" = f."Lang" AND tk."StrongsBase" <> f."StrongsBase")
            """;
        return new Spec { GroupInner = inner, Ctes = hasGroup ? topKey : string.Empty, ItemsWhere = hasGroup ? items : "false" };
    }

    private static Spec BuildM2(string f, string filters, bool hasGroup)
    {
        var minorityCte = $"""
            WITH "Minor" AS
            (
                SELECT t."Lang", t."StrongsBase", t."LemmaLower", t.tot
                FROM (SELECT g."Lang", g."StrongsBase", g."LemmaLower", g.c
                           , sum(g.c) OVER (PARTITION BY g."Lang", g."StrongsBase") AS tot
                      FROM (SELECT f."Lang", f."StrongsBase", f."LemmaLower", count(*) AS c
                            FROM {DqSql.Facts} f
                            WHERE {DqSql.AlignedUsable}{f}{(hasGroup ? " AND f.\"StrongsBase\" = @gKey" : string.Empty)}
                            GROUP BY f."Lang", f."StrongsBase", f."LemmaLower") g) t
                WHERE t.c < @minority * t.tot
                   OR t.c < @maxCount
            )
            """;
        var join = "f.\"LemmaLower\" IS NOT NULL AND EXISTS (SELECT 1 FROM \"Minor\" m WHERE m.\"Lang\" = f.\"Lang\" AND m.\"StrongsBase\" = f.\"StrongsBase\" AND m.\"LemmaLower\" = f.\"LemmaLower\")";
        var inner = $"""
            SELECT f."StrongsBase"            AS "Key"
                 , f."StrongsBase"            AS "Label"
                 , f."Lang"                   AS "Lang"
                 , count(*)::int              AS "Count"
                 , max(m.tot)::int            AS "Total"
                 , true                       AS "Minority"
            FROM {DqSql.Facts} f
                JOIN "Minor" m ON m."Lang" = f."Lang" AND m."StrongsBase" = f."StrongsBase" AND m."LemmaLower" = f."LemmaLower"
            WHERE {DqSql.AlignedUsable}{f}{filters}
              AND {Mismatch}
            GROUP BY f."StrongsBase", f."Lang"
            """;
        return new Spec
        {
            Ctes = minorityCte,
            GroupInner = inner,
            ItemsWhere = hasGroup ? $"{DqSql.AlignedUsable}{f}{filters} AND f.\"StrongsBase\" = @gKey AND {Mismatch} AND {join}" : "false",
        };
    }

    private static string L2Predicate(string f, string filters) => $"""
        f."GermanRowNo" = 1
        AND f."LemmaLower" IS NOT NULL
        AND f."LemmaLower" = f."PlainLower"
        AND length(f."LemmaLower") > 3{f}{filters}
        AND (
              (f."Pos" = 'NOUN'
               AND (f."Number" = 'P' OR f."Case" IN ('Gen', 'Dat', 'Acc'))
               AND EXISTS (SELECT 1 FROM "LemmaSet" ls WHERE ls."Pos" = f."Pos" AND ls."LemmaLower" = ANY(ARRAY[left(f."LemmaLower", -1), left(f."LemmaLower", -2), left(f."LemmaLower", -3)])))
           OR (f."Pos" = 'ADJ'
               AND f."Case" IS NOT NULL
               AND f."LemmaLower" ~ '(e|en|em|er|es)$'
               AND EXISTS (SELECT 1 FROM "LemmaSet" ls WHERE ls."Pos" = f."Pos" AND ls."LemmaLower" = ANY(ARRAY[left(f."LemmaLower", -1), left(f."LemmaLower", -2)])))
           OR (f."Pos" IN ('VERB', 'AUX')
               AND f."VerbForm" = 'Fin'
               AND f."LemmaLower" !~ 'n$')
        )
        """;

    private static Spec BuildL2(string f, string filters, bool hasGroup)
    {
        var ctes = $"""
            WITH "LemmaSet" AS
            (
                SELECT l."LemmaLower", l."Pos"
                FROM {DqSql.Facts} l
                WHERE l."GermanRowNo" = 1
                  AND l."LemmaLower" IS NOT NULL
                  AND l."Pos" IS NOT NULL
                GROUP BY l."LemmaLower", l."Pos"
                HAVING count(*) >= @lemmaSetMin
            )
            """;
        var predicate = L2Predicate(f, filters);
        var inner = $"""
            SELECT f."LemmaLower" || '~' || f."Pos"                 AS "Key"
                 , mode() WITHIN GROUP (ORDER BY f."Lemma") || ' (' || f."Pos" || ')' AS "Label"
                 , NULL::text                                       AS "Lang"
                 , count(*)::int                                    AS "Count"
                 , NULL::int                                        AS "Total"
                 , false                                            AS "Minority"
            FROM {DqSql.Facts} f
            WHERE {predicate}
            GROUP BY f."LemmaLower", f."Pos"
            """;
        return new Spec
        {
            Ctes = ctes,
            GroupInner = inner,
            ItemsWhere = hasGroup ? $"{predicate} AND f.\"LemmaLower\" = @gLemma AND f.\"Pos\" = @gPos" : "false",
        };
    }

    /// <summary>
    /// L6 (STTS empty or not in SttsTags) and L7 (STTS does not accept the part of speech). Both read Elb1871Morphology live, joined to
    /// the facts rows of the German words, so DqWordFacts needs no STTS column. pos = UD tag, class = STTS tag.
    /// </summary>
    private static Spec BuildStts(CheckRequest r, string f, DynamicParameters p)
    {
        var where = new StringBuilder("f.\"GermanRowNo\" = 1");
        where.Append(r.Code == "L6" ? $" AND {SttsUnknown}" : $" AND {SttsMismatch}");
        where.Append(f);
        if (!string.IsNullOrEmpty(r.Pos))
        {
            where.Append(" AND m.\"PartOfSpeech\" = @pos");
            p.Add("pos", r.Pos);
        }

        if (!string.IsNullOrEmpty(r.Class))
        {
            where.Append(r.Class == EmptyStts ? " AND m.\"Stts\" IS NULL" : " AND m.\"Stts\" = @sttsTag");
            p.Add("sttsTag", r.Class);
        }

        if (r.Chapter is { } chapter)
        {
            where.Append(" AND f.\"Chapter\" = @chapter");
            p.Add("chapter", chapter);
        }

        return new Spec { ItemsWhere = where.ToString(), Join = MorphologyJoin };
    }

    private async Task<CheckMatrix> GetSttsMatrixAsync(System.Data.IDbConnection connection, CheckRequest r, DqFacets facets, CancellationToken ct)
    {
        var sql = $"""
            SELECT m."PartOfSpeech"                  AS "Pos"
                 , COALESCE(m."Stts", '{EmptyStts}') AS "Lang"
                 , ''                                AS "OrigClass"
                 , count(*)::int                     AS "Count"
            FROM {DqSql.Facts} f
                {MorphologyJoin}
            WHERE f."GermanRowNo" = 1
                AND {(r.Code == "L6" ? SttsUnknown : SttsMismatch)}{facets.Predicate}
            GROUP BY m."PartOfSpeech", m."Stts"
            """;
        var rows = (await connection.QueryAsync<MatrixRow>(new CommandDefinition(sql, commandTimeout: GroupTimeoutSeconds, cancellationToken: ct))).ToList();
        return new CheckMatrix
        {
            Rows = [.. rows.Select(x => x.Pos).Distinct().Order()],
            Columns = [.. rows.Select(x => x.Lang).Distinct().Order()],
            Cells = [.. rows.Select(x => new MatrixCell { Row = x.Pos, Column = x.Lang, Count = x.Count, Expected = false })],
        };
    }

    private static Spec BuildL3(string f, bool hasGroup)
    {
        var inner = $"""
            SELECT t."PlainLower"                              AS "Key"
                 , t."PlainLower"                              AS "Label"
                 , NULL::text                                  AS "Lang"
                 , (t.total - t.top)::int                      AS "Count"
                 , t.total::int                                AS "Total"
                 , true                                        AS "Minority"
            FROM (SELECT x."PlainLower", sum(x.c) AS total, max(x.c) AS top
                  FROM (SELECT f."PlainLower", f."LemmaLower", count(*) AS c
                        FROM {DqSql.Facts} f
                        WHERE f."GermanRowNo" = 1
                          AND f."LemmaLower" IS NOT NULL{f}
                        GROUP BY f."PlainLower", f."LemmaLower") x
                  GROUP BY x."PlainLower"
                  HAVING count(*) >= 2) t
            WHERE (t.total - t.top) < @formMinority * t.total
            """;
        var items = $"""
            f."GermanRowNo" = 1
            AND f."PlainLower" = @gKey
            AND f."LemmaLower" IS NOT NULL{f}
            AND f."LemmaLower" <> (SELECT t."LemmaLower"
                                   FROM {DqSql.Facts} t
                                   WHERE t."GermanRowNo" = 1
                                     AND t."PlainLower" = @gKey
                                     AND t."LemmaLower" IS NOT NULL
                                   GROUP BY t."LemmaLower"
                                   ORDER BY count(*) DESC, t."LemmaLower"
                                   LIMIT 1)
            """;
        return new Spec { GroupInner = inner, ItemsWhere = hasGroup ? items : "false" };
    }

    private static Spec BuildL4(string f, bool hasGroup)
    {
        var inner = $"""
            SELECT r."LemmaLower"                                       AS "Key"
                 , max(r."Lemma")                                       AS "Label"
                 , NULL::text                                           AS "Lang"
                 , sum(r.c)::int                                        AS "Count"
                 , max(r.total)::int                                    AS "Total"
                 , (sum(r.c) < @minority * max(r.total))                AS "Minority"
            FROM (SELECT x."LemmaLower", x."Pos", x."Lemma", x.c
                       , sum(x.c) OVER (PARTITION BY x."LemmaLower") AS total
                       , row_number() OVER (PARTITION BY x."LemmaLower" ORDER BY x.c DESC, x."Pos") AS rn
                       , first_value(x."Pos") OVER (PARTITION BY x."LemmaLower" ORDER BY x.c DESC, x."Pos") AS "TopPos"
                  FROM (SELECT f."LemmaLower", f."Pos", mode() WITHIN GROUP (ORDER BY f."Lemma") AS "Lemma", count(*) AS c
                        FROM {DqSql.Facts} f
                        WHERE f."GermanRowNo" = 1
                          AND f."LemmaLower" IS NOT NULL
                          AND f."Pos" IS NOT NULL{f}
                        GROUP BY f."LemmaLower", f."Pos") x) r
            WHERE r.rn > 1
              AND (r."TopPos" || '|' || r."Pos") <> ALL(@allowedPairs)
            GROUP BY r."LemmaLower"
            """;
        var items = $"""
            f."GermanRowNo" = 1
            AND f."LemmaLower" = @gKey
            AND f."Pos" IS NOT NULL{f}
            AND f."Pos" <> (SELECT t."Pos"
                            FROM {DqSql.Facts} t
                            WHERE t."GermanRowNo" = 1
                              AND t."LemmaLower" = @gKey
                              AND t."Pos" IS NOT NULL
                            GROUP BY t."Pos"
                            ORDER BY count(*) DESC, t."Pos"
                            LIMIT 1)
            AND ((SELECT t."Pos" FROM {DqSql.Facts} t WHERE t."GermanRowNo" = 1 AND t."LemmaLower" = @gKey AND t."Pos" IS NOT NULL GROUP BY t."Pos" ORDER BY count(*) DESC, t."Pos" LIMIT 1) || '|' || f."Pos") <> ALL(@allowedPairs)
            """;
        return new Spec { GroupInner = inner, ItemsWhere = hasGroup ? items : "false" };
    }

    public async Task<CheckResponse> GetAsync(CheckRequest r, DateTime? refreshedAt, CancellationToken ct)
    {
        var facets = DqFacets.From(r);
        var response = new CheckResponse { Code = r.Code, Page = r.Page, PageSize = r.PageSize, RefreshedAt = refreshedAt };

        if (r.Code == "C1")
        {
            await FillCoverageGroupsAsync(response, r, facets, ct);
            return response;
        }

        var p = new DynamicParameters();
        AddCommon(p, r);
        var spec = BuildSpec(r, facets, p);
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);

        if (r.Code == "M3") response.Matrix = await GetMatrixAsync(connection, facets, ct);
        if (r.Code == "M4") response.Groups = await GetM4GroupsAsync(connection, facets, p, ct);
        if (r.Code == "L5") return await GetL5Async(response, r, facets, connection, p, ct);
        if (r.Code is "L6" or "L7") response.Matrix = await GetSttsMatrixAsync(connection, r, facets, ct);

        var aggregated = spec.GroupInner is not null;
        if (aggregated && r.Group is null)
        {
            var groups = (await connection.QueryAsync<GroupRow>(new CommandDefinition(GroupPage(spec.Ctes, spec.GroupInner!), p, commandTimeout: GroupTimeoutSeconds, cancellationToken: ct))).ToList();
            response.Total = groups.Count > 0 ? groups[0].GroupTotal : 0;
            response.Groups = await ToGroupsAsync(r.Code, groups, ct);
            return response;
        }

        response.Total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(DqSql.CountFindings(spec.Ctes, spec.ItemsWhere, spec.Join), p, commandTimeout: GroupTimeoutSeconds, cancellationToken: ct));
        var extraColumns = r.Code is "L6" or "L7" ? ", m.\"Stts\" AS \"Stts\", m.\"PartOfSpeech\" AS \"LivePos\"" : string.Empty;
        var rows = (await connection.QueryAsync<FactRow>(new CommandDefinition(DqSql.PagedFindings(spec.Ctes, spec.ItemsWhere, spec.Join, extraColumns), p, commandTimeout: GroupTimeoutSeconds, cancellationToken: ct))).ToList();
        var lexicon = await _lexicon.GetManyAsync(FindingFactory.StrongsOf(rows), ct);
        response.Items = [.. rows.Select(row => FindingFactory.Create(r.Code, row, lexicon, r.Group))];
        return response;
    }

    private async Task<List<CheckGroup>> ToGroupsAsync(string code, List<GroupRow> groups, CancellationToken ct)
    {
        var lexicon = code == "M2" ? await _lexicon.GetManyAsync(groups.Select(g => g.Key), ct) : null;
        return [.. groups.Select(g =>
        {
            var label = g.Label;
            if (lexicon is not null && lexicon.TryGetValue(g.Key, out var e)) label = $"{g.Key} {e.Lemma}";
            return new CheckGroup
            {
                Key = g.Key,
                Label = label,
                Count = g.Count,
                Share = g.Total is > 0 ? Math.Round((double)g.Count / g.Total.Value, 4) : null,
                Flags = g.Minority ? ["minority"] : null,
                Lang = g.Lang,
            };
        })];
    }

    private async Task FillCoverageGroupsAsync(CheckResponse response, CheckRequest r, DqFacets facets, CancellationToken ct)
    {
        var books = await _facts.GetCoverageAsync("german", facets, ct);
        var groups = books
            .SelectMany(b => b.Chapters.Select(c => (b.BibleBookId, c)))
            .Where(x => x.c.Total > 0 && (double)x.c.Unaligned / x.c.Total >= DataQualityThresholds.CoverageFindingShare && x.c.Unaligned >= Math.Max(1, r.MinCount ?? 0))
            .OrderByDescending(x => (double)x.c.Unaligned / x.c.Total)
            .ThenBy(x => x.BibleBookId)
            .ThenBy(x => x.c.Chapter)
            .ToList();
        response.Total = groups.Count;
        response.Groups = [.. groups.Skip((r.Page - 1) * r.PageSize).Take(r.PageSize).Select(x => new CheckGroup
        {
            Key = $"{x.BibleBookId}:{x.c.Chapter}",
            Label = $"{x.BibleBookId} {x.c.Chapter}",
            Count = x.c.Unaligned,
            Share = Math.Round((double)x.c.Unaligned / x.c.Total, 4),
            Flags = (double)x.c.Unaligned / x.c.Total >= DataQualityThresholds.CoverageErrorShare ? ["error"] : null,
        })];
    }

    private async Task<CheckMatrix> GetMatrixAsync(System.Data.IDbConnection connection, DqFacets facets, CancellationToken ct)
    {
        var sql = $"""
            SELECT f."Pos"        AS "Pos"
                 , f."Lang"       AS "Lang"
                 , f."OrigClass"  AS "OrigClass"
                 , count(*)::int  AS "Count"
            FROM {DqSql.Facts} f
            WHERE {DqSql.AlignedUsable}{facets.Predicate}
              AND f."OrigClass" IS NOT NULL
              AND f."Pos" <> 'X'
            GROUP BY f."Pos", f."Lang", f."OrigClass"
            """;
        var rows = (await connection.QueryAsync<MatrixRow>(new CommandDefinition(sql, commandTimeout: GroupTimeoutSeconds, cancellationToken: ct))).ToList();
        return new CheckMatrix
        {
            Rows = [.. rows.Select(x => x.Pos).Distinct().Order()],
            Columns = [.. rows.Select(x => $"{x.Lang}:{x.OrigClass}").Distinct().Order()],
            Cells = [.. rows.Select(x => new MatrixCell
            {
                Row = x.Pos,
                Column = $"{x.Lang}:{x.OrigClass}",
                Count = x.Count,
                Expected = PosClassRules.IsExpected(x.Lang, x.Pos, x.OrigClass),
            })],
        };
    }

    private async Task<List<CheckGroup>> GetM4GroupsAsync(System.Data.IDbConnection connection, DqFacets facets, DynamicParameters p, CancellationToken ct)
    {
        var sql = $"""
            SELECT (CASE WHEN f."BibleBookId" <= 39 THEN 'ot' ELSE 'nt' END) AS "Key"
                 , count(*)::int AS "Count"
            FROM {DqSql.Facts} f
            WHERE {DqSql.AlignedUsable}{facets.Predicate} AND {ContentFunction}
            GROUP BY 1
            """;
        var rows = (await connection.QueryAsync<KeyCount>(new CommandDefinition(sql, p, cancellationToken: ct))).ToDictionary(x => x.Key, x => x.Count);
        return
        [
            new CheckGroup { Key = "ot", Label = "AT", Count = rows.GetValueOrDefault("ot") },
            new CheckGroup { Key = "nt", Label = "NT", Count = rows.GetValueOrDefault("nt") },
        ];
    }

    private static string ComboKey(MorphologyValues v) =>
        string.Join('|', new[] { v.VerbForm, v.Degree, v.Tense, v.Person, v.Number, v.Mood, v.Case, v.Gender }.Select(x => x ?? string.Empty));

    private async Task<IReadOnlyList<(MorphCombo Combo, IReadOnlyList<MorphologyIssue> Issues)>> GetL5CombosAsync(System.Data.IDbConnection connection, DqFacets facets, CancellationToken ct)
    {
        var sql = $"""
            SELECT f."Pos" AS "Pos", f."VerbForm" AS "VerbForm", f."Degree" AS "Degree", f."Tense" AS "Tense", f."Person" AS "Person"
                 , f."Number" AS "Number", f."Mood" AS "Mood", f."Case" AS "Case", f."Gender" AS "Gender", count(*)::int AS "Count"
            FROM {DqSql.Facts} f
            WHERE f."GermanRowNo" = 1
              AND f."HasMorphology"{facets.Predicate}
            GROUP BY f."Pos", f."VerbForm", f."Degree", f."Tense", f."Person", f."Number", f."Mood", f."Case", f."Gender"
            """;
        var combos = await connection.QueryAsync<MorphCombo>(new CommandDefinition(sql, commandTimeout: GroupTimeoutSeconds, cancellationToken: ct));
        return [.. combos.Select(c => (c, MorphologyIssues.Evaluate(c.Values))).Where(x => x.Item2.Count > 0)];
    }

    private async Task<CheckResponse> GetL5Async(CheckResponse response, CheckRequest r, DqFacets facets, System.Data.IDbConnection connection, DynamicParameters p, CancellationToken ct)
    {
        var violating = await GetL5CombosAsync(connection, facets, ct);
        var cells = violating
            .SelectMany(v => v.Issues.Select(i => (Pos: v.Combo.Pos ?? "?", i.Field, v.Combo.Count)))
            .GroupBy(x => (x.Pos, x.Field))
            .Select(g => new MatrixCell { Row = g.Key.Pos, Column = g.Key.Field, Count = g.Sum(x => x.Count), Expected = false })
            .ToList();
        response.Matrix = new CheckMatrix
        {
            Rows = [.. cells.Select(c => c.Row).Distinct().Order()],
            Columns = [.. cells.Select(c => c.Column).Distinct().Order()],
            Cells = cells,
        };

        var selected = violating
            .Where(v => (string.IsNullOrEmpty(r.Pos) || v.Combo.Pos == r.Pos) && (string.IsNullOrEmpty(r.Class) || v.Issues.Any(i => i.Field == r.Class)))
            .ToList();
        response.Total = selected.Sum(v => v.Combo.Count);
        if (selected.Count == 0) return response;

        p.Add("combos", selected.Select(v => $"{v.Combo.Pos}|{ComboKey(v.Combo.Values)}").ToArray());
        var where = $"""
            f."GermanRowNo" = 1
            AND f."HasMorphology"{facets.Predicate}{(r.Chapter is null ? string.Empty : " AND f.\"Chapter\" = @chapter")}
            AND (f."Pos" || '|' || concat_ws('|', COALESCE(f."VerbForm", ''), COALESCE(f."Degree", ''), COALESCE(f."Tense", ''), COALESCE(f."Person", ''), COALESCE(f."Number", ''), COALESCE(f."Mood", ''), COALESCE(f."Case", ''), COALESCE(f."Gender", ''))) = ANY(@combos)
            """;
        if (r.Chapter is { } chapter) p.Add("chapter", chapter);
        var rows = (await connection.QueryAsync<FactRow>(new CommandDefinition(DqSql.PagedFindings(string.Empty, where), p, commandTimeout: GroupTimeoutSeconds, cancellationToken: ct))).ToList();
        response.Items = [.. rows.Select(row => FindingFactory.Create("L5", row, new Dictionary<string, LexiconEntry>()))];
        return response;
    }

    public async Task<CheckCount> CountAsync(string code, DqFacets facets, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        var f = facets.Predicate;
        switch (code)
        {
            case "U1":
            {
                var sql = $"""
                    SELECT count(*)::int AS "Count"
                         , (count(*) FILTER (WHERE {DqSql.GermanErrorPredicate}))::int AS "ErrorCount"
                         , (count(*) FILTER (WHERE f."Pos" IN ({DqSql.ContentPosList})))::int AS "Content"
                    FROM {DqSql.Facts} f
                    WHERE f."Kind" IN ('added', 'none'){f}
                    """;
                var row = await connection.QuerySingleAsync<(int Count, int ErrorCount, int Content)>(new CommandDefinition(sql, cancellationToken: ct));
                return new CheckCount(row.Count, row.ErrorCount, new KeyCount { Key = "content", Count = row.Content });
            }
            case "U2":
            case "U3":
            {
                var lang = code == "U2" ? "G" : "H";
                var sql = $"""
                    SELECT count(*)::int AS "Count"
                         , (count(*) FILTER (WHERE ({UnmappedRules.SourceClassKeySql}) IN ({DqSql.InList(UnmappedRules.ErrorKeys(lang))})))::int AS "ErrorCount"
                    FROM {DqSql.Facts} f
                    WHERE f."Kind" = 'unmappedSource' AND f."Lang" = '{lang}'{f}
                    """;
                var row = await connection.QuerySingleAsync<(int Count, int ErrorCount)>(new CommandDefinition(sql, cancellationToken: ct));
                return new CheckCount(row.Count, row.ErrorCount, null);
            }
            case "C1":
            {
                var books = await _facts.GetCoverageAsync("german", facets, ct);
                var shares = books.SelectMany(b => b.Chapters).Where(c => c.Total > 0).Select(c => (double)c.Unaligned / c.Total).ToList();
                return new CheckCount(
                    shares.Count(s => s >= DataQualityThresholds.CoverageFindingShare),
                    shares.Count(s => s >= DataQualityThresholds.CoverageErrorShare),
                    null);
            }
            case "M3":
            case "M4":
            case "L1":
            {
                var p = new DynamicParameters();
                AddCommon(p, new CheckRequest { Code = code });
                var spec = BuildSpec(new CheckRequest { Code = code }, facets, p);
                var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(DqSql.CountFindings(spec.Ctes, spec.ItemsWhere), p, commandTimeout: GroupTimeoutSeconds, cancellationToken: ct));
                if (code == "M3") return new CheckCount(count, 0, null);
                KeyCount? sub = null;
                if (code == "M4")
                {
                    var groups = await GetM4GroupsAsync(connection, facets, p, ct);
                    sub = new KeyCount { Key = "nt", Count = groups.First(g => g.Key == "nt").Count };
                }

                return new CheckCount(count, count, sub);
            }
            case "L6":
            case "L7":
            {
                var p = new DynamicParameters();
                var spec = BuildSpec(new CheckRequest { Code = code }, facets, p);
                var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(DqSql.CountFindings(spec.Ctes, spec.ItemsWhere, spec.Join), p, commandTimeout: GroupTimeoutSeconds, cancellationToken: ct));
                return new CheckCount(count, code == "L6" ? count : 0, null);
            }
            case "R1":
            case "R2":
            case "R3":
            case "R4":
            case "R5":
            case "R6":
            {
                var p = new DynamicParameters();
                var spec = BuildSpec(new CheckRequest { Code = code }, facets, p);
                var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(DqSql.CountFindings(spec.Ctes, spec.ItemsWhere, spec.Join), p, commandTimeout: GroupTimeoutSeconds, cancellationToken: ct));
                return new CheckCount(count, DqRoleChecks.IsError(code) ? count : 0, null);
            }
            case "T1":
            case "T2":
                return await DqTskChecks.CountAsync(connection, null, code, facets, ct);
            case "L5":
            {
                var violating = await GetL5CombosAsync(connection, facets, ct);
                return new CheckCount(violating.Sum(v => v.Combo.Count), 0, null);
            }
            default:
            {
                // M1, M2, L2, L3, L4: number of groups
                var req = new CheckRequest { Code = code };
                var p = new DynamicParameters();
                AddCommon(p, req);
                var spec = BuildSpec(req, facets, p);
                var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(GroupCount(spec.Ctes, spec.GroupInner!), p, commandTimeout: GroupTimeoutSeconds, cancellationToken: ct));
                return new CheckCount(count, 0, null);
            }
        }
    }
}
