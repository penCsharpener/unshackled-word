using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.GrammarSearch;

namespace UnshackledWord.Infrastructure.GrammarSearch;

/// <summary>
/// Dapper access of the grammar search (roadmap 0029.1, Hebrew 0029.6). Every slot becomes a CTE <c>s{i}</c> returning <c>(HebRefId, WordId)</c> of the Greek and Hebrew words that satisfy it
/// (code set through <c>= ANY(@GCodes{i}/@HCodes{i})</c> on the <c>("Morphology"|"Grammar", "LxxRefId")</c> indexes, lemma through the Strong's key list); the CTEs are generated from the slot count
/// only, codes and keys are bound parameters. Since 0029.7 a slot CTE also returns <c>"Kind"</c> (0 Greek, 1 Hebrew, 2 German), <c>"WordKey"</c> (<c>Id * 3 + Kind</c>, unique across the tables) and <c>"GermanPosition"</c>:
/// a German-only slot selects <c>Elb1871Morphology</c> rows, a mixed slot joins its original words through <c>Elb1871GreekMapping</c> / <c>Elb1871HebrewMapping</c> to the German words that satisfy the German side. Greek verses map to Elb1871 verses through <c>Elb1871Verses</c>; a verse without a mapping keeps its Greek key, like the Strong's statistics do.
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

    private const string HebrewRootStrongs = "split_part(w.\"RootDisambiguatedStrongsInstance\", '_', 1)";

    /// <summary>
    /// The WITH clause (verse map, slot CTEs, <c>cand</c>, <c>cw</c>) and its parameters; <paramref name="verseCondition"/> (one of the constants above, null = none) restricts the verses and needs its parameters.
    /// A slot CTE is the union of its Greek, its Hebrew and (German-only) its German side. <c>cand</c> has one row per (slot, word, German position); <c>cw</c> one per (slot, word) and serves the verse counts.
    /// </summary>
    internal static string BuildWith(IReadOnlyList<GrammarSlotQuery> slots, string? verseCondition, DynamicParameters parameters)
    {
        var ctes = new List<string> { VerseMapCte };
        for (var i = 0; i < slots.Count; i++)
        {
            var selects = new List<string>();
            if (slots[i].IsGermanOnly) selects.Add(GermanSelect(i, slots[i].German!, verseCondition, parameters));
            if (slots[i].Greek is { } greek) selects.Add(SideSelect(i, greek, slots[i].German, false, verseCondition, parameters));
            if (slots[i].Hebrew is { } hebrew) selects.Add(SideSelect(i, hebrew, slots[i].German, true, verseCondition, parameters));

            ctes.Add($"""
                s{i} AS (
                    {string.Join("\n    UNION ALL\n    ", selects)}
                )
                """);
        }

        var union = string.Join("\n    UNION ALL\n    ", Enumerable.Range(0, slots.Count).Select(i =>
            $"SELECT {i} AS \"Slot\", s{i}.\"HebRefId\", s{i}.\"WordId\", s{i}.\"Kind\", s{i}.\"WordKey\", s{i}.\"GermanPosition\" FROM s{i}"));
        ctes.Add($"""
            cand AS (
                {union}
            )
            """);

        // A mixed slot yields one row per German word of an original word: only then the (slot, word) pairs need de-duplication.
        var distinct = slots.Any(s => s.German is not null && !s.IsGermanOnly) ? "DISTINCT " : string.Empty;
        ctes.Add($"""
            cw AS (
                SELECT {distinct}c."Slot", c."HebRefId", c."WordKey"
                FROM cand c
            )
            """);
        return "WITH " + string.Join("\n, ", ctes);
    }

    private static string GermanSelect(int slot, GrammarGermanSide german, string? verseCondition, DynamicParameters parameters)
    {
        var conditions = new List<string>();
        AddGermanConditions(slot, german, "m", conditions, parameters);
        if (verseCondition is not null) conditions.Add($"AND m.\"HebRefId\" {verseCondition}");

        return $"""
            SELECT m."HebRefId"          AS "HebRefId"
                 , m."Id"                AS "WordId"
                 , 2                     AS "Kind"
                 , m."Id" * 3 + 2        AS "WordKey"
                 , m."PositionInVerse"   AS "GermanPosition"
            FROM "unshackled-word"."Elb1871Morphology" m
            WHERE 1=1
                {string.Join("\n        ", conditions)}
            """;
    }

    /// <summary>The German lemma and <c>dt.</c> filters as conditions on <c>"Elb1871Morphology"</c> (alias <paramref name="alias"/>); values are bound parameters, column names come from the whitelist.</summary>
    private static void AddGermanConditions(int slot, GrammarGermanSide german, string alias, List<string> conditions, DynamicParameters parameters)
    {
        if (german.Lemma is { } lemma)
        {
            conditions.Add($"AND lower({alias}.\"Lemma\") = @DeLemma{slot}");
            parameters.Add($"DeLemma{slot}", lemma);
        }

        for (var j = 0; j < german.Filters.Count; j++)
        {
            var filter = german.Filters[j];
            if (!GrammarVocabulary.GermanColumns.Contains(filter.Column)) throw new ArgumentException($"'{filter.Column}' is not a German grammar column.", nameof(german));

            var test = filter.Pattern ? "ILIKE" : "=";
            var inSet = $"COALESCE({alias}.\"{filter.Column}\" {test} ANY(@DeF{slot}_{j}), false)";
            conditions.Add(filter.Negated ? $"AND NOT {inSet}" : $"AND {inSet}");
            parameters.Add($"DeF{slot}_{j}", filter.Values.ToArray());
        }
    }

    private static string SideSelect(int slot, GrammarSide side, GrammarGermanSide? german, bool hebrew, string? verseCondition, DynamicParameters parameters)
    {
        var prefix = hebrew ? "H" : "G";
        var kind = hebrew ? 1 : 0;
        var codeColumn = hebrew ? "w.\"Grammar\"" : "w.\"Morphology\"";
        var strongsColumn = hebrew ? HebrewRootStrongs : "w.\"DisambiguatedStrongs\"";
        var conditions = new List<string>();
        if (side.Codes is { } codes)
        {
            conditions.Add($"AND {codeColumn} = ANY(@{prefix}Codes{slot})");
            parameters.Add($"{prefix}Codes{slot}", codes.ToArray());
        }

        if (side.StrongsKeys is { } keys)
        {
            conditions.Add($"AND {strongsColumn} = ANY(@{prefix}Strongs{slot})");
            parameters.Add($"{prefix}Strongs{slot}", keys.ToArray());
        }

        if (verseCondition is not null) conditions.Add($"AND COALESCE(vm.\"HebRefId\", w.\"LxxRefId\") {verseCondition}");

        var mapping = string.Empty;
        var germanPosition = "NULL::int";
        var distinct = string.Empty;
        if (german is not null)
        {
            // Mixed slot: the original word counts only through a mapped German word that satisfies the German side.
            mapping = $"""

                    JOIN "unshackled-word"."{(hebrew ? "Elb1871HebrewMapping" : "Elb1871GreekMapping")}" gm ON gm."StepWordId" = w."Id"
                    JOIN "unshackled-word"."Elb1871Morphology" m ON m."HebRefId" = gm."HebRefId" AND m."PositionInVerse" = gm."PositionInVerse"
                """;
            germanPosition = "m.\"PositionInVerse\"";
            distinct = "DISTINCT ";
            AddGermanConditions(slot, german, "m", conditions, parameters);
        }

        return $"""
            SELECT {distinct}COALESCE(vm."HebRefId", w."LxxRefId") AS "HebRefId"
                 , w."Id" AS "WordId"
                 , {kind} AS "Kind"
                 , w."Id" * 3 + {kind} AS "WordKey"
                 , {germanPosition} AS "GermanPosition"
            FROM "unshackled-word"."{(hebrew ? "StepHebrewWords" : "StepGreekWords")}" w
                LEFT JOIN verse_map vm ON vm."LxxRefId" = w."LxxRefId"{mapping}
            WHERE 1=1
                {string.Join("\n        ", conditions)}
            """;
    }

    public async Task<IReadOnlyList<GrammarVerseRow>> FindVersesAsync(IReadOnlyList<GrammarSlotQuery> slots, CancellationToken token = default)
    {
        var parameters = new DynamicParameters();
        parameters.Add("SlotCount", slots.Count);
        var sql = BuildWith(slots, null, parameters) + """

            SELECT c."HebRefId"                             AS "HebRefId"
                 , (count(*) > count(DISTINCT c."WordKey")) AS "Shared"
                 , count(DISTINCT c."WordKey")::int         AS "Words"
            FROM cw c
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
                 , c."WordKey"              AS "WordKey"
                 , w."PositionInVerse"      AS "Position"
                 , false                    AS "IsHebrew"
                 , false                    AS "IsGerman"
                 , c."GermanPosition"       AS "GermanPosition"
                 , w."Greek"                AS "Form"
                 , w."Lemma"                AS "Lemma"
                 , w."DisambiguatedStrongs" AS "Strongs"
                 , w."Morphology"           AS "Morphology"
            FROM cand c
                JOIN "unshackled-word"."StepGreekWords" w ON w."Id" = c."WordId"
            WHERE 1=1
                AND c."Kind" = 0
            UNION ALL
            SELECT c."Slot"
                 , c."HebRefId"
                 , c."WordId"
                 , c."WordKey"
                 , w."PositionInVerse"
                 , true
                 , false
                 , c."GermanPosition"
                 , w."Hebrew"
                 , ''
                 , split_part(w."RootDisambiguatedStrongsInstance", '_', 1)
                 , w."Grammar"
            FROM cand c
                JOIN "unshackled-word"."StepHebrewWords" w ON w."Id" = c."WordId"
            WHERE 1=1
                AND c."Kind" = 1
            UNION ALL
            SELECT c."Slot"
                 , c."HebRefId"
                 , c."WordId"
                 , c."WordKey"
                 , m."PositionInVerse"
                 , false
                 , true
                 , c."GermanPosition"
                 , COALESCE(ew."WordInContext", '')
                 , m."Lemma"
                 , ''
                 , ''
            FROM cand c
                JOIN "unshackled-word"."Elb1871Morphology" m ON m."Id" = c."WordId"
                LEFT JOIN "unshackled-word"."Elb1871Words" ew ON ew."HebRefId" = m."HebRefId" AND ew."PositionInVerse" = m."PositionInVerse"
            WHERE 1=1
                AND c."Kind" = 2
            ORDER BY "HebRefId", "Slot", "Position"
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
                 , (count(*) > count(DISTINCT c."WordKey")) AS "Shared"
                 , count(DISTINCT c."WordKey")::int         AS "Words"
            FROM cw c
            WHERE 1=1
                AND c."HebRefId" {comparison} @Boundary
            GROUP BY c."HebRefId"
            HAVING count(DISTINCT c."Slot") = @SlotCount
            ORDER BY c."HebRefId" {direction}
            LIMIT @Limit
            """;

        return [.. await _dbReader.ReadAsListAsync<GrammarVerseRow>(sql, parameters)];
    }

    public async Task<IReadOnlyList<GrammarCodeCountRow>> CountCodesAsync(IReadOnlyList<string>? strongsKeys, CancellationToken token = default)
    {
        const string sql = """
            SELECT w."Morphology" AS "Code"
                 , count(*)::int  AS "Count"
            FROM "unshackled-word"."StepGreekWords" w
            WHERE 1=1
                AND (@Strongs::text[] IS NULL OR w."DisambiguatedStrongs" = ANY(@Strongs))
            GROUP BY w."Morphology"
            """;

        return [.. await _dbReader.ReadAsListAsync<GrammarCodeCountRow>(sql, new { Strongs = strongsKeys?.ToArray() })];
    }

    public async Task<IReadOnlyList<GrammarCodeCountRow>> CountHebrewCodesAsync(IReadOnlyList<string>? strongsKeys, CancellationToken token = default)
    {
        const string sql = """
            SELECT w."Grammar" AS "Code"
                 , count(*)::int AS "Count"
            FROM "unshackled-word"."StepHebrewWords" w
            WHERE 1=1
                AND (@Strongs::text[] IS NULL OR split_part(w."RootDisambiguatedStrongsInstance", '_', 1) = ANY(@Strongs))
            GROUP BY w."Grammar"
            """;

        return [.. await _dbReader.ReadAsListAsync<GrammarCodeCountRow>(sql, new { Strongs = strongsKeys?.ToArray() })];
    }

    public async Task<IReadOnlyList<GrammarCodeCountRow>> CountGermanAsync(string column, GrammarGermanSide? side, CancellationToken token = default)
    {
        if (!GrammarVocabulary.GermanColumns.Contains(column)) throw new ArgumentException($"'{column}' is not a German grammar column.", nameof(column));

        var parameters = new DynamicParameters();
        var conditions = new List<string>();
        if (side is not null) AddGermanConditions(0, side, "m", conditions, parameters);

        var sql = $"""
            SELECT m."{column}" AS "Code"
                 , count(*)::int AS "Count"
            FROM "unshackled-word"."Elb1871Morphology" m
            WHERE 1=1
                AND m."{column}" IS NOT NULL
                {string.Join("\n        ", conditions)}
            GROUP BY m."{column}"
            """;

        return [.. await _dbReader.ReadAsListAsync<GrammarCodeCountRow>(sql, parameters)];
    }

    public async Task<bool> GermanLemmaExistsAsync(string lowerLemma, CancellationToken token = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM "unshackled-word"."Elb1871Morphology" m
                WHERE 1=1
                    AND lower(m."Lemma") = @Lemma
            )
            """;

        return (await _dbReader.ReadAsListAsync<bool>(sql, new { Lemma = lowerLemma })).FirstOrDefault();
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

    public async Task<IReadOnlyList<GrammarMappingRow>> ReadHebrewMappingsAsync(IReadOnlyList<int> stepWordIds, CancellationToken token = default)
    {
        if (stepWordIds.Count == 0) return [];

        const string sql = """
            SELECT m."StepWordId"      AS "StepWordId"
                 , m."PositionInVerse" AS "PositionInVerse"
            FROM "unshackled-word"."Elb1871HebrewMapping" m
            WHERE 1=1
                AND m."StepWordId" = ANY(@StepWordIds)
            """;

        return [.. await _dbReader.ReadAsListAsync<GrammarMappingRow>(sql, new { StepWordIds = stepWordIds.ToArray() })];
    }
}
