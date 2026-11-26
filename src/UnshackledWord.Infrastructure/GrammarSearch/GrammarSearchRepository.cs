using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.GrammarSearch;

namespace UnshackledWord.Infrastructure.GrammarSearch;

/// <summary>
/// Dapper access of the grammar search (roadmap 0029.1). Every slot becomes a CTE <c>s{i}</c> returning <c>(HebRefId, WordId)</c> of the Greek words that satisfy it
/// (code set through <c>= ANY(@Codes{i})</c> on the <c>("Morphology", "LxxRefId")</c> index, lemma through the Strong's key list); the CTEs are generated from the slot count
/// only, codes and keys are bound parameters. Greek verses map to Elb1871 verses through <c>Elb1871Verses</c>; a verse without a mapping keeps its Greek key, like the Strong's statistics do.
/// </summary>
public sealed class GrammarSearchRepository : IGrammarSearchRepository
{
    private const string VerseMapCte = """
        verse_map AS (
            SELECT DISTINCT ON (v."LxxRefId") v."LxxRefId"
                 , v."HebRefId"
            FROM "unshackled-word"."Elb1871Verses" v
            ORDER BY v."LxxRefId", v."Id"
        )
        """;

    private readonly IDbReader _dbReader;

    public GrammarSearchRepository(IDbReader dbReader)
    {
        _dbReader = dbReader;
    }

    private const string VerseIdCondition = "= ANY(@VerseIds)";
    private const string VerseRangeCondition = "BETWEEN @From AND @To";

    /// <summary>The WITH clause (verse map, slot CTEs, <c>cand</c>) and its parameters; <paramref name="verseCondition"/> (one of the constants above, null = none) restricts the verses and needs its parameters.</summary>
    internal static string BuildWith(IReadOnlyList<GrammarSlotQuery> slots, string? verseCondition, DynamicParameters parameters)
    {
        var ctes = new List<string> { VerseMapCte };
        for (var i = 0; i < slots.Count; i++)
        {
            var conditions = new List<string>();
            if (slots[i].Codes is { } codes)
            {
                conditions.Add($"AND w.\"Morphology\" = ANY(@Codes{i})");
                parameters.Add($"Codes{i}", codes.ToArray());
            }

            if (slots[i].StrongsKeys is { } keys)
            {
                conditions.Add($"AND w.\"DisambiguatedStrongs\" = ANY(@Strongs{i})");
                parameters.Add($"Strongs{i}", keys.ToArray());
            }

            if (verseCondition is not null) conditions.Add($"AND COALESCE(vm.\"HebRefId\", w.\"LxxRefId\") {verseCondition}");

            ctes.Add($"""
                s{i} AS (
                    SELECT COALESCE(vm."HebRefId", w."LxxRefId") AS "HebRefId"
                         , w."Id" AS "WordId"
                    FROM "unshackled-word"."StepGreekWords" w
                        LEFT JOIN verse_map vm ON vm."LxxRefId" = w."LxxRefId"
                    WHERE 1=1
                        {string.Join("\n        ", conditions)}
                )
                """);
        }

        var union = string.Join("\n    UNION ALL\n    ", Enumerable.Range(0, slots.Count).Select(i => $"SELECT {i} AS \"Slot\", s{i}.\"HebRefId\", s{i}.\"WordId\" FROM s{i}"));
        ctes.Add($"""
            cand AS (
                {union}
            )
            """);
        return "WITH " + string.Join("\n, ", ctes);
    }

    public async Task<IReadOnlyList<GrammarVerseRow>> FindVersesAsync(IReadOnlyList<GrammarSlotQuery> slots, CancellationToken token = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("SlotCount", slots.Count);
        var sql = BuildWith(slots, null, parameters) + """

            SELECT c."HebRefId"                             AS "HebRefId"
                 , (count(*) > count(DISTINCT c."WordId"))  AS "Shared"
                 , count(DISTINCT c."WordId")::int          AS "Words"
            FROM cand c
            GROUP BY c."HebRefId"
            HAVING count(DISTINCT c."Slot") = @SlotCount
            """;

        return [.. await _dbReader.ReadAsListAsync<GrammarVerseRow>(sql, parameters)];
    }

    public async Task<IReadOnlyList<GrammarMatchRow>> ReadMatchesAsync(IReadOnlyList<GrammarSlotQuery> slots, IReadOnlyList<int> hebRefIds, CancellationToken token = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("VerseIds", hebRefIds.ToArray());
        return await ReadMatchesAsync(slots, VerseIdCondition, parameters);
    }

    public async Task<IReadOnlyList<GrammarMatchRow>> ReadMatchesInRangeAsync(IReadOnlyList<GrammarSlotQuery> slots, int fromHebRefId, int toHebRefId, CancellationToken token = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("From", fromHebRefId);
        parameters.Add("To", toHebRefId);
        return await ReadMatchesAsync(slots, VerseRangeCondition, parameters);
    }

    private async Task<IReadOnlyList<GrammarMatchRow>> ReadMatchesAsync(IReadOnlyList<GrammarSlotQuery> slots, string verseCondition, DynamicParameters parameters)
    {
        var sql = BuildWith(slots, verseCondition, parameters) + """

            SELECT c."Slot"                 AS "Slot"
                 , c."HebRefId"             AS "HebRefId"
                 , c."WordId"               AS "WordId"
                 , w."PositionInVerse"      AS "Position"
                 , w."Greek"                AS "Greek"
                 , w."Lemma"                AS "Lemma"
                 , w."DisambiguatedStrongs" AS "Strongs"
                 , w."Morphology"           AS "Morphology"
            FROM cand c
                JOIN "unshackled-word"."StepGreekWords" w ON w."Id" = c."WordId"
            ORDER BY c."HebRefId", c."Slot", w."PositionInVerse"
            """;

        return [.. await _dbReader.ReadAsListAsync<GrammarMatchRow>(sql, parameters)];
    }

    public async Task<IReadOnlyList<GrammarVerseRow>> FindVersesNearAsync(IReadOnlyList<GrammarSlotQuery> slots, int boundaryHebRefId, bool before, int limit, CancellationToken token = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("SlotCount", slots.Count);
        parameters.Add("Boundary", boundaryHebRefId);
        parameters.Add("Limit", limit);
        var comparison = before ? "<" : ">";
        var direction = before ? "DESC" : "ASC";
        var sql = BuildWith(slots, null, parameters) + $"""

            SELECT c."HebRefId"                             AS "HebRefId"
                 , (count(*) > count(DISTINCT c."WordId"))  AS "Shared"
                 , count(DISTINCT c."WordId")::int          AS "Words"
            FROM cand c
            WHERE 1=1
                AND c."HebRefId" {comparison} @Boundary
            GROUP BY c."HebRefId"
            HAVING count(DISTINCT c."Slot") = @SlotCount
            ORDER BY c."HebRefId" {direction}
            LIMIT @Limit
            """;

        return [.. await _dbReader.ReadAsListAsync<GrammarVerseRow>(sql, parameters)];
    }

    public async Task<IReadOnlyList<GrammarGermanWordRow>> ReadGermanWordsAsync(IReadOnlyList<int> hebRefIds, CancellationToken token = default)
    {
        const string sql = """
            SELECT w."HebRefId"        AS "HebRefId"
                 , w."PositionInVerse" AS "PositionInVerse"
                 , w."WordInContext"   AS "WordInContext"
            FROM "unshackled-word"."Elb1871Words" w
            WHERE 1=1
                AND w."HebRefId" = ANY(@HebRefIds)
            ORDER BY w."HebRefId", w."PositionInVerse"
            """;

        return [.. await _dbReader.ReadAsListAsync<GrammarGermanWordRow>(sql, new { HebRefIds = hebRefIds.ToArray() })];
    }

    public async Task<IReadOnlyList<GrammarMappingRow>> ReadMappingsAsync(IReadOnlyList<int> stepWordIds, CancellationToken token = default)
    {
        if (stepWordIds.Count == 0) return [];

        const string sql = """
            SELECT m."StepWordId"      AS "StepWordId"
                 , m."PositionInVerse" AS "PositionInVerse"
            FROM "unshackled-word"."Elb1871GreekMapping" m
            WHERE 1=1
                AND m."StepWordId" = ANY(@StepWordIds)
            """;

        return [.. await _dbReader.ReadAsListAsync<GrammarMappingRow>(sql, new { StepWordIds = stepWordIds.ToArray() })];
    }
}
