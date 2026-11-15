using UnshackledWord.Application.Abstractions;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Revision.BulkReplaceSearch;

public sealed class BulkRevisionRow
{
    public string Code { get; set; } = string.Empty;
    public bool IsBase { get; set; }
}

/// <summary>A token of a verse of the source text; <c>IsVariant</c> tokens come from <c>RevisionWords</c>, the others from <c>Elb1871Words</c>.</summary>
public sealed class BulkTokenRow
{
    public int HebRefId { get; set; }
    public int PositionInVerse { get; set; }
    public int WordId { get; set; }
    public bool IsVariant { get; set; }
    public string? PlainWord { get; set; }
    public string WordInContext { get; set; } = string.Empty;
    public string? Lemma { get; set; }
    public string? Stts { get; set; }
}

public sealed class BulkStrongsRow
{
    public int WordId { get; set; }
    public string? Strongs { get; set; }
}

public sealed class BulkTargetRow
{
    public int HebRefId { get; set; }
    public int Version { get; set; }
    public string VerseText { get; set; } = string.Empty;
}

public interface IBulkReplaceSearchRepository
{
    Task<IReadOnlyList<BulkRevisionRow>> GetRevisionsAsync();

    Task<IReadOnlyList<string>> GetLemmasAsync(string word);

    /// <summary>All tokens of the (at most <paramref name="maxVerses"/>) verses of the source text that contain a hit, ordered by verse and position.</summary>
    Task<IReadOnlyList<BulkTokenRow>> GetHitVerseTokensAsync(string sourceCode, string word, IReadOnlyList<string> lemmas, int maxVerses);

    Task<(IReadOnlyList<BulkStrongsRow> Base, IReadOnlyList<BulkStrongsRow> Variant)> GetStrongsAsync(IReadOnlyList<int> baseWordIds, IReadOnlyList<int> variantWordIds);

    Task<IReadOnlyList<BulkTargetRow>> GetTargetVersesAsync(string targetCode, IReadOnlyList<int> hebRefIds);
}

public sealed class BulkReplaceSearchRepository : IBulkReplaceSearchRepository
{
    public const string Revisions = """
        SELECT r."Code"   AS "Code"
             , r."IsBase" AS "IsBase"
        FROM "unshackled-word"."Revisions" r
        """;

    public const string Lemmas = """
        SELECT DISTINCT m."Lemma"
        FROM "unshackled-word"."Elb1871Words" w
            JOIN "unshackled-word"."Elb1871Morphology" m ON m."HebRefId" = w."HebRefId" AND m."PositionInVerse" = w."PositionInVerse"
        WHERE lower(w."PlainWord") = lower(@Word)
            AND m."Lemma" IS NOT NULL
        """;

    /// <summary>
    /// Source text per verse: the active variant of the source revision, else Elb1871. First the (at most @MaxVerses) verses with a hit,
    /// found with the word / lemma predicates pushed down to the tables (so the lower("PlainWord") indexes work), then every token of those verses.
    /// </summary>
    public const string HitVerseTokens = """
        WITH "SourceVerses" AS (SELECT rv."Id"
                                     , rv."HebRefId"
                                FROM "unshackled-word"."RevisionVerses" rv
                                    JOIN "unshackled-word"."Revisions" r ON r."Id" = rv."RevisionId"
                                WHERE rv."IsActive"
                                    AND r."Code" = @SourceCode)
           , "HitVerses" AS (SELECT h."HebRefId"
                             FROM (SELECT w."HebRefId"
                                   FROM "unshackled-word"."Elb1871Words" w
                                   WHERE lower(w."PlainWord") = lower(@Word)
                                       AND NOT EXISTS (SELECT 1 FROM "SourceVerses" sv WHERE sv."HebRefId" = w."HebRefId")
                                   UNION
                                   SELECT m."HebRefId"
                                   FROM "unshackled-word"."Elb1871Morphology" m
                                   WHERE m."Lemma" = ANY (@Lemmas)
                                       AND NOT EXISTS (SELECT 1 FROM "SourceVerses" sv WHERE sv."HebRefId" = m."HebRefId")
                                   UNION
                                   SELECT sv."HebRefId"
                                   FROM "SourceVerses" sv
                                       JOIN "unshackled-word"."RevisionWords" rw ON rw."RevisionVerseId" = sv."Id"
                                   WHERE lower(rw."PlainWord") = lower(@Word)
                                   UNION
                                   SELECT sv."HebRefId"
                                   FROM "SourceVerses" sv
                                       JOIN "unshackled-word"."RevisionWords" rw ON rw."RevisionVerseId" = sv."Id"
                                       JOIN "unshackled-word"."Elb1871Morphology" m ON m."HebRefId" = sv."HebRefId" AND m."PositionInVerse" = rw."BasePositionInVerse"
                                   WHERE m."Lemma" = ANY (@Lemmas)) h
                             ORDER BY h."HebRefId"
                             LIMIT @MaxVerses)
           , "Source" AS (SELECT sv."HebRefId"
                               , rw."PositionInVerse"
                               , rw."Id"                  AS "WordId"
                               , true                     AS "IsVariant"
                               , rw."PlainWord"
                               , rw."WordInContext"
                               , rw."BasePositionInVerse" AS "BasePosition"
                          FROM "HitVerses" hv
                              JOIN "SourceVerses" sv ON sv."HebRefId" = hv."HebRefId"
                              JOIN "unshackled-word"."RevisionWords" rw ON rw."RevisionVerseId" = sv."Id"
                          UNION ALL
                          SELECT w."HebRefId"
                               , w."PositionInVerse"
                               , w."Id"
                               , false
                               , w."PlainWord"
                               , w."WordInContext"
                               , w."PositionInVerse"
                          FROM "HitVerses" hv
                              JOIN "unshackled-word"."Elb1871Words" w ON w."HebRefId" = hv."HebRefId"
                          WHERE NOT EXISTS (SELECT 1 FROM "SourceVerses" sv WHERE sv."HebRefId" = hv."HebRefId"))
        SELECT s."HebRefId"        AS "HebRefId"
             , s."PositionInVerse" AS "PositionInVerse"
             , s."WordId"          AS "WordId"
             , s."IsVariant"       AS "IsVariant"
             , s."PlainWord"       AS "PlainWord"
             , s."WordInContext"   AS "WordInContext"
             , m."Lemma"           AS "Lemma"
             , m."Stts"            AS "Stts"
        FROM "Source" s
            LEFT JOIN "unshackled-word"."Elb1871Morphology" m ON m."HebRefId" = s."HebRefId" AND m."PositionInVerse" = s."BasePosition"
        ORDER BY s."HebRefId"
               , s."PositionInVerse"
        """;

    public const string BaseStrongs = """
        SELECT m."ElbWordId"                  AS "WordId"
             , sh."DisambiguatedStrongs"      AS "Strongs"
        FROM "unshackled-word"."Elb1871HebrewMapping" m
            JOIN "unshackled-word"."StepHebrewWords" sh ON sh."Id" = m."StepWordId"
        WHERE m."ElbWordId" = ANY (@Ids)
        UNION ALL
        SELECT m."ElbWordId"
             , sg."DisambiguatedStrongs"
        FROM "unshackled-word"."Elb1871GreekMapping" m
            JOIN "unshackled-word"."StepGreekWords" sg ON sg."Id" = m."StepWordId"
        WHERE m."ElbWordId" = ANY (@Ids)
        """;

    public const string VariantStrongs = """
        SELECT rm."RevisionWordId"                                 AS "WordId"
             , COALESCE(sh."DisambiguatedStrongs", sg."DisambiguatedStrongs") AS "Strongs"
        FROM "unshackled-word"."RevisionMapping" rm
            LEFT JOIN "unshackled-word"."StepHebrewWords" sh ON rm."Lang" = 'H' AND sh."Id" = rm."StepWordId"
            LEFT JOIN "unshackled-word"."StepGreekWords" sg ON rm."Lang" = 'G' AND sg."Id" = rm."StepWordId"
        WHERE rm."RevisionWordId" = ANY (@Ids)
            AND rm."StepWordId" IS NOT NULL
        """;

    public const string TargetVerses = """
        SELECT rv."HebRefId"  AS "HebRefId"
             , rv."Version"   AS "Version"
             , rv."VerseText" AS "VerseText"
        FROM "unshackled-word"."RevisionVerses" rv
            JOIN "unshackled-word"."Revisions" r ON r."Id" = rv."RevisionId"
        WHERE rv."IsActive"
            AND r."Code" = @TargetCode
            AND rv."HebRefId" = ANY (@HebRefIds)
        """;

    private readonly IDbReader _reader;

    public BulkReplaceSearchRepository(IDbReader reader)
    {
        _reader = reader;
    }

    public async Task<IReadOnlyList<BulkRevisionRow>> GetRevisionsAsync() =>
        (await _reader.ReadAsListAsync<BulkRevisionRow>(Revisions)).ToList();

    public async Task<IReadOnlyList<string>> GetLemmasAsync(string word) =>
        (await _reader.ReadAsListAsync<string>(Lemmas, new { Word = word })).ToList();

    public async Task<IReadOnlyList<BulkTokenRow>> GetHitVerseTokensAsync(string sourceCode, string word, IReadOnlyList<string> lemmas, int maxVerses) =>
        (await _reader.ReadAsListAsync<BulkTokenRow>(HitVerseTokens, new { SourceCode = sourceCode, Word = word, Lemmas = lemmas.ToArray(), MaxVerses = maxVerses })).ToList();

    public async Task<(IReadOnlyList<BulkStrongsRow> Base, IReadOnlyList<BulkStrongsRow> Variant)> GetStrongsAsync(IReadOnlyList<int> baseWordIds, IReadOnlyList<int> variantWordIds)
    {
        IReadOnlyList<BulkStrongsRow> baseRows = baseWordIds.Count == 0
            ? []
            : (await _reader.ReadAsListAsync<BulkStrongsRow>(BaseStrongs, new { Ids = baseWordIds.ToArray() })).ToList();
        IReadOnlyList<BulkStrongsRow> variantRows = variantWordIds.Count == 0
            ? []
            : (await _reader.ReadAsListAsync<BulkStrongsRow>(VariantStrongs, new { Ids = variantWordIds.ToArray() })).ToList();
        return (baseRows, variantRows);
    }

    public async Task<IReadOnlyList<BulkTargetRow>> GetTargetVersesAsync(string targetCode, IReadOnlyList<int> hebRefIds) =>
        (await _reader.ReadAsListAsync<BulkTargetRow>(TargetVerses, new { TargetCode = targetCode, HebRefIds = hebRefIds.ToArray() })).ToList();
}
