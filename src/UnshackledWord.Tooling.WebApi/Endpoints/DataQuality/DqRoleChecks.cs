using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Domain.Models.DataQuality;

namespace UnshackledWord.Tooling.WebApi.Endpoints.DataQuality;

/// <summary>
/// Link-role checks R1–R6 (roadmap 0018, 04-roles-and-counting.md "Invariants", 05-data-model.md "Stats") over DqWordFacts (alias f).
/// A source morpheme instance is (Lang, StepWordId, MorphemeStrongs) of the 'aligned' rows. R1 lists one row per instance, R2–R4 the link rows,
/// R5/R6 German words (first row of the word).
/// </summary>
internal static class DqRoleChecks
{
    public static readonly string[] Codes = ["R1", "R2", "R3", "R4", "R5", "R6"];

    /// <summary>Checks whose every finding is an error (the others are warnings).</summary>
    public static bool IsError(string code) => code is "R1" or "R3" or "R6";

    /// <summary>The legacy 1871 spellings (lower case), the same list the agent validator uses; passed as parameter @legacy.</summary>
    public static string[] LegacySpellings => [.. AgentLemmaRules.LegacySpellings.Keys.Select(k => k.ToLowerInvariant())];

    // The contraction exception of invariant 2: an APPRART word ("zum") that is a unit of the article morpheme and the head of another one.
    private const string ContractionUnit = """
        (f."Role" = 'unit'
         AND f."Stts" = 'APPRART'
         AND substring(f."MorphemeStrongs" FROM '^[GH]\d+') IN ('G3588', 'H9009')
         AND EXISTS (SELECT 1
                     FROM "unshackled-word"."DqWordFacts" h
                     WHERE h."ElbWordId" = f."ElbWordId"
                       AND h."Kind" = 'aligned'
                       AND h."Role" = 'head'))
        """;

    private static string Instances(string facets) => $"""
        WITH "Inst" AS MATERIALIZED
        (
            SELECT f."Lang"                                                  AS "Lang"
                 , f."StepWordId"                                            AS "StepWordId"
                 , f."MorphemeStrongs"                                       AS "MorphemeStrongs"
                 , count(*) FILTER (WHERE f."Role" = 'head')                 AS "Heads"
                 , COALESCE(bool_and(f."MultiHead") FILTER (WHERE f."Role" = 'head'), false) AS "AllMultiHead"
                 , bool_and({ContractionUnit})                               AS "AllContraction"
                 , min(f."ElbWordId")                                        AS "FirstElbWordId"
            FROM {DqSql.Facts} f
            WHERE 1=1
                AND f."Kind" = 'aligned'
                AND f."MorphemeStrongs" IS NOT NULL{facets}
            GROUP BY f."Lang", f."StepWordId", f."MorphemeStrongs"
        )
        """;

    private const string InstanceJoin = """
        JOIN "Inst" i ON i."Lang" = f."Lang" AND i."StepWordId" = f."StepWordId" AND i."MorphemeStrongs" = f."MorphemeStrongs"
        """;

    /// <summary>CTE prefix, WHERE (without facets and filters) and JOIN of one check.</summary>
    public static (string Ctes, string Where, string Join) Build(string code, string facets) => code switch
    {
        // R1: links on the morpheme but no head (all units), contraction articles excepted; one row per instance
        "R1" => (Instances(facets),
            "f.\"Kind\" = 'aligned' AND i.\"Heads\" = 0 AND NOT i.\"AllContraction\" AND f.\"ElbWordId\" = i.\"FirstElbWordId\"",
            InstanceJoin),
        // R2: more than one head without the accepted multi-head flag
        "R2" => (Instances(facets),
            "f.\"Kind\" = 'aligned' AND f.\"Role\" = 'head' AND i.\"Heads\" > 1 AND NOT i.\"AllMultiHead\"",
            InstanceJoin),
        // R3: a unit whose morpheme has no head, contraction articles excepted
        "R3" => (Instances(facets),
            $"f.\"Kind\" = 'aligned' AND f.\"Role\" = 'unit' AND i.\"Heads\" = 0 AND NOT {ContractionUnit}",
            InstanceJoin),
        // R4: the same (part) lemma twice on one morpheme
        "R4" => ($"""
                WITH "Dup" AS MATERIALIZED
                (
                    SELECT f."Lang", f."StepWordId", f."MorphemeStrongs", f."RenderLemmaLower"
                    FROM {DqSql.Facts} f
                    WHERE 1=1
                        AND f."Kind" = 'aligned'
                        AND f."MorphemeStrongs" IS NOT NULL
                        AND f."RenderLemmaLower" IS NOT NULL{facets}
                    GROUP BY f."Lang", f."StepWordId", f."MorphemeStrongs", f."RenderLemmaLower"
                    HAVING count(*) > 1
                )
                """,
            "f.\"Kind\" = 'aligned'",
            """
            JOIN "Dup" d ON d."Lang" = f."Lang" AND d."StepWordId" = f."StepWordId" AND d."MorphemeStrongs" = f."MorphemeStrongs" AND d."RenderLemmaLower" = f."RenderLemmaLower"
            """),
        // R5: PTKVZ without a finite verb in the verse whose lemma starts with the particle and is longer (AgentLemmaRules.CheckParticle);
        // infinitive "zu" (PTKVZ right before *INF / *IZU, Elb1871 has no PTKZU) is not a particle
        "R5" => (string.Empty,
            """
            f."GermanRowNo" = 1
            AND f."Stts" = 'PTKVZ'
            AND NOT (f."PlainLower" = 'zu'
                     AND EXISTS (SELECT 1
                                 FROM "unshackled-word"."Elb1871Morphology" n
                                 WHERE n."HebRefId" = f."HebRefId"
                                   AND n."PositionInVerse" = f."PositionInVerse" + 1
                                   AND (n."Stts" LIKE '%INF' OR n."Stts" LIKE '%IZU')))
            AND NOT EXISTS (SELECT 1
                            FROM "unshackled-word"."DqWordFacts" v
                            WHERE v."HebRefId" = f."HebRefId"
                              AND v."GermanRowNo" = 1
                              AND v."Stts" LIKE 'V%'
                              AND (v."Stts" LIKE '%FIN' OR v."Stts" LIKE '%IMP')
                              AND length(v."LemmaLower") > length(f."PlainLower")
                              AND starts_with(v."LemmaLower", f."PlainLower"))
            """,
            string.Empty),
        // R6: an 1871 spelling as lemma or part lemma
        "R6" => (string.Empty,
            "f.\"GermanRowNo\" = 1 AND (f.\"LemmaLower\" = ANY(@legacy) OR lower(f.\"PartLemma\") = ANY(@legacy))",
            string.Empty),
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown role check"),
    };

    /// <summary>Severity and German reason of one finding.</summary>
    public static (string Severity, string Detail) Judge(string code, FactRow r)
    {
        var morpheme = $"{r.SourceWord} ({r.MorphemeStrongs})";
        var severity = IsError(code) ? UnmappedRules.Error : UnmappedRules.Warning;
        return code switch
        {
            "R1" => (severity, $"{morpheme}: verknüpft, aber ohne Kopfwort (nur unit)"),
            "R2" => (severity, $"„{r.Plain}“ ist eines von mehreren Kopfwörtern auf {morpheme} ohne Mehrfach-Freigabe"),
            "R3" => (severity, $"„{r.Plain}“ ist unit von {morpheme}, das Morphem hat kein Kopfwort"),
            "R4" => (severity, $"Lemma „{r.RenderLemma}“ steht mehrfach auf {morpheme}"),
            "R5" => (severity, $"Verbpartikel „{r.Plain}“ ohne finites Verb „{r.Plain?.ToLowerInvariant()}…“ im Vers"),
            "R6" => (severity, r.Lemma is not null && AgentLemmaRules.LegacySpellings.TryGetValue(r.Lemma, out var modern)
                ? $"Lemma „{r.Lemma}“ ist eine Schreibung von 1871, modern „{modern}“"
                : $"Lemma „{r.RenderLemma ?? r.Lemma}“ ist eine Schreibung von 1871"),
            _ => (UnmappedRules.Info, string.Empty),
        };
    }
}
