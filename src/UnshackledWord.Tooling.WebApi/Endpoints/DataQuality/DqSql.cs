using UnshackledWord.Domain.Models.DataQuality;
using UnshackledWord.Domain.WebApi.DataQuality;

namespace UnshackledWord.Tooling.WebApi.Endpoints.DataQuality;

/// <summary>Testament / book facets of every read, as a SQL predicate over DqWordFacts (alias f). Only validated ints / enums are inlined.</summary>
public sealed record DqFacets(string? Testament, int? BookId)
{
    public static DqFacets From(DqFacetsRequest r) => new(r.Testament, r.BookId);

    public string Predicate => string.Concat(
        Testament == "ot" ? " AND f.\"BibleBookId\" <= 39" : Testament == "nt" ? " AND f.\"BibleBookId\" >= 40" : string.Empty,
        BookId is { } id ? $" AND f.\"BibleBookId\" = {id}" : string.Empty);
}

/// <summary>Shared SQL fragments of the data quality reads (single source with <see cref="PosClassRules"/>).</summary>
internal static class DqSql
{
    public const string Facts = "\"unshackled-word\".\"DqWordFacts\"";

    /// <summary>The mapping stats views (migrations 0013, 0032) in refresh order; all read DqWordFacts only, so any order works after the facts refresh.</summary>
    public static readonly IReadOnlyList<string> StatsViews = ["StatsStrongsRenderings", "StatsStrongs", "StatsGermanLemma", "StatsGermanForms"];

    /// <summary>One REFRESH statement per stats view, in <see cref="StatsViews"/> order: plain for views not yet populated (CONCURRENTLY needs data), CONCURRENTLY otherwise so readers keep the old snapshot.</summary>
    public static IReadOnlyList<string> StatsRefreshStatements(IReadOnlySet<string> populatedViews) =>
    [
        .. StatsViews.Select(view => populatedViews.Contains(view)
            ? $"REFRESH MATERIALIZED VIEW CONCURRENTLY \"unshackled-word\".\"{view}\""
            : $"REFRESH MATERIALIZED VIEW \"unshackled-word\".\"{view}\"")
    ];

    public static readonly string ContentPosList = InList(PosClassRules.ContentGermanPos);
    public static readonly string HebrewFunctionPosList = InList(PosClassRules.HebrewFunctionWordPos);

    /// <summary>Rows usable for alignment-quality checks (aligned, with Strong's, German PoS and lemma; Hebrew function words skipped).</summary>
    public static readonly string AlignedUsable = $"""
        f."Kind" = 'aligned'
        AND f."StrongsBase" IS NOT NULL
        AND f."Pos" IS NOT NULL
        AND f."LemmaLower" IS NOT NULL
        AND NOT (f."Lang" = 'H' AND f."Pos" IN ({HebrewFunctionPosList}))
        """;

    /// <summary>German-side error severity (content word without original, or no mapping row at all).</summary>
    public static readonly string GermanErrorPredicate = $"(f.\"Kind\" = 'none' OR f.\"Pos\" IN ({ContentPosList}))";

    /// <summary>One column list for every finding row; aliases equal the FactRow properties.</summary>
    public const string FactColumns = """
        f."ElbWordId"        AS "ElbWordId"
        , f."StepWordId"     AS "StepWordId"
        , f."BibleBookId"    AS "BibleBookId"
        , f."Chapter"        AS "Chapter"
        , f."Verse"          AS "Verse"
        , f."HebRefId"       AS "HebRefId"
        , f."PositionInVerse" AS "PositionInVerse"
        , f."Lang"           AS "Lang"
        , f."Kind"           AS "Kind"
        , f."Plain"          AS "Plain"
        , f."Lemma"          AS "Lemma"
        , f."Pos"            AS "Pos"
        , f."VerbForm"       AS "VerbForm"
        , f."Degree"         AS "Degree"
        , f."Tense"          AS "Tense"
        , f."Person"         AS "Person"
        , f."Number"         AS "Number"
        , f."Mood"           AS "Mood"
        , f."Case"           AS "Case"
        , f."Gender"         AS "Gender"
        , f."SourceWord"     AS "SourceWord"
        , f."StrongsBase"    AS "StrongsBase"
        , f."OrigClass"      AS "OrigClass"
        , f."ProperNoun"     AS "ProperNoun"
        , f."InNa"           AS "InNa"
        , f."InTr"           AS "InTr"
        , f."HebType"        AS "HebType"
        , f."Role"           AS "Role"
        , f."MorphemeStrongs" AS "MorphemeStrongs"
        , f."RenderLemma"    AS "RenderLemma"
        """;

    public const string FindingOrder = "\"BibleBookId\", \"Chapter\", \"Verse\", \"PositionInVerse\", \"StepWordId\"";

    /// <summary>
    /// Paged finding rows with ~5 words of verse context. <paramref name="ctes"/> is an optional "WITH ..." prefix (also used by the count query).
    /// </summary>
    public static string PagedFindings(string ctes, string where, string join = "", string extraColumns = "") => $"""
        {ctes}
        SELECT p.*
             , ctx."Context" AS "Context"
        FROM (SELECT {FactColumns}{extraColumns}
              FROM {Facts} f
              {join}
              WHERE {where}
              ORDER BY {FindingOrder}
              LIMIT @take OFFSET @skip) p
            LEFT JOIN LATERAL (SELECT string_agg(c."WordInContext", ' ' ORDER BY c."PositionInVerse") AS "Context"
                               FROM "unshackled-word"."Elb1871Words" c
                               WHERE p."HebRefId" IS NOT NULL
                                 AND p."ElbWordId" IS NOT NULL
                                 AND c."HebRefId" = p."HebRefId"
                                 AND c."PositionInVerse" BETWEEN p."PositionInVerse" - {DataQualityThresholds.ContextWords} AND p."PositionInVerse" + {DataQualityThresholds.ContextWords}) ctx ON true
        ORDER BY {FindingOrder}
        """;

    public static string CountFindings(string ctes, string where, string join = "") => $"""
        {ctes}
        SELECT count(*)
        FROM {Facts} f
        {join}
        WHERE {where}
        """;

    public static string InList(IEnumerable<string> values) => string.Join(", ", values.Select(v => "'" + v.Replace("'", "''") + "'"));
}

/// <summary>One finding row read from DqWordFacts (plus context).</summary>
public sealed class FactRow
{
    public int? ElbWordId { get; set; }
    public int? StepWordId { get; set; }
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public int? HebRefId { get; set; }
    public int? PositionInVerse { get; set; }
    public string? Lang { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string? Plain { get; set; }
    public string? Lemma { get; set; }
    public string? Pos { get; set; }
    public string? VerbForm { get; set; }
    public string? Degree { get; set; }
    public string? Tense { get; set; }
    public string? Person { get; set; }
    public string? Number { get; set; }
    public string? Mood { get; set; }
    public string? Case { get; set; }
    public string? Gender { get; set; }
    public string? SourceWord { get; set; }
    public string? StrongsBase { get; set; }
    public string? OrigClass { get; set; }
    public bool? ProperNoun { get; set; }
    public bool? InNa { get; set; }
    public bool? InTr { get; set; }
    public string? HebType { get; set; }

    /// <summary>Link role (head/unit), source morpheme and lemma used for the rendering (part lemma or lemma); aligned rows only (0018).</summary>
    public string? Role { get; set; }

    public string? MorphemeStrongs { get; set; }
    public string? RenderLemma { get; set; }
    public string? Context { get; set; }

    /// <summary>L6/L7 only: the live STTS tag and part of speech of Elb1871Morphology.</summary>
    public string? Stts { get; set; }

    public string? LivePos { get; set; }
}
