using System.Text.Json;
using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Grammar;
using UnshackledWord.Domain.WebApi.Lemmas;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Lemmas;

public interface ILemmaBulkRepository
{
    Task<IReadOnlyList<LemmaCountItem>> GetLemmaCountsAsync(string q, int limit, CancellationToken ct);

    Task<BulkOccurrencesResponse> GetOccurrencesAsync(BulkOccurrencesRequest request, CancellationToken ct);

    Task<BulkWordIdsResponse> GetWordIdsAsync(BulkFilterRequest request, CancellationToken ct);

    /// <summary>
    /// All-or-nothing: returns the stale word ids (nothing written) or the new change set id and row count.
    /// </summary>
    Task<(IReadOnlyList<BulkStaleDetail> Stale, int ChangeSetId, int Updated)> ApplyAsync(BulkApplyRequest request, int userId, CancellationToken ct);

    /// <summary>Null when the change set does not exist; ChangeSetId 0 with Restored 0 when nothing could be restored.</summary>
    Task<RevertResult> RevertAsync(int changeSetId, int userId, CancellationToken ct);
}

public enum RevertStatus
{
    Ok,
    NotFound,
    AlreadyReverted,
    NotRevertable,
}

public sealed record RevertResult(RevertStatus Status, int ChangeSetId = 0, int Restored = 0, int Skipped = 0);

public sealed class LemmaBulkRepository : ILemmaBulkRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public LemmaBulkRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private const string MatchedDef = """
        "Matched" AS
        (
            SELECT w."Id"              AS "WordId"
                 , w."BibleBookId"     AS "BookId"
                 , w."Chapter"         AS "Chapter"
                 , w."Verse"           AS "Verse"
                 , w."PositionInVerse" AS "PositionInVerse"
                 , w."WordInContext"   AS "WordInContext"
                 , w."PlainWord"       AS "PlainWord"
                 , m."Lemma"           AS "Lemma"
                 , m."PartOfSpeech"    AS "PartOfSpeech"
                 , m."Case"            AS "Case"
                 , m."Number"          AS "Number"
                 , m."Gender"          AS "Gender"
            FROM "unshackled-word"."Elb1871Morphology" m
                JOIN "unshackled-word"."Elb1871Words" w ON w."HebRefId" = m."HebRefId" AND w."PositionInVerse" = m."PositionInVerse"
            WHERE m."Lemma" = ANY(@from)
                AND w."BibleBookId" BETWEEN @bookFrom AND @bookTo
                AND (@chapter::int4 IS NULL OR w."Chapter" = @chapter)
                AND (@partOfSpeech::text IS NULL OR m."PartOfSpeech" = @partOfSpeech)
                AND (@plainWord::text IS NULL OR w."PlainWord" = @plainWord)
        )
        """;

    // THE one place that derives the original-language key of an Elb word from the current mapping tables
    // (Mapping v2 swaps this fragment). Expects a CTE "KeyIds"("WordId", "BookId", "PartOfSpeech") and yields
    // "OrigKeys"("WordId", "Kind", "Key", "Strongs", "Word", "Pos"):
    //   Kind  aligned | multi (several different base Strong's) | added (only added-word rows) | none (no mapping row)
    //   Key   G4680 / H2450 (base Strong's; Hebrew: braced main morpheme) / G1+G2 / added / none
    // Hebrew "Pos" is skipped (NULL) for German function words: there the host word is meaningless and the flags are suppressed anyway.
    // Strong's comes from the STEP word tables, never from Elb1871GreekMapping.StrongsNumber (stale).
    private const string OriginalKeyCtes = """
        "OrigRows" AS
        (
            SELECT i."WordId"                                                    AS "WordId"
                 , COALESCE(g."IsAddedWord", false)                              AS "IsAdded"
                 , s."Id"                                                        AS "StepId"
                 , CASE WHEN left(s."DisambiguatedStrongs", 1) = 'G' THEN left(s."DisambiguatedStrongs", 1 + length(substr(s."DisambiguatedStrongs", 2)) - length(ltrim(substr(s."DisambiguatedStrongs", 2), '0123456789')))
                        ELSE substring(s."DisambiguatedStrongs" FROM 'G\d+')
                   END                                                           AS "Key"
                 , s."DisambiguatedStrongs"                                      AS "Strongs"
                 , s."Greek"                                                     AS "Word"
                 , split_part(s."Morphology", '-', 1)                            AS "Pos"
            FROM "KeyIds" i
                JOIN "unshackled-word"."Elb1871GreekMapping" g ON g."ElbWordId" = i."WordId"
                LEFT JOIN "unshackled-word"."StepGreekWords" s ON s."Id" = g."StepWordId"
            WHERE i."BookId" >= 40
            UNION ALL
            SELECT i."WordId"
                 , COALESCE(h."IsAddedWord", false)
                 , s."Id"
                 , CASE WHEN left(hb."Braced", 1) = 'H' THEN left(hb."Braced", 1 + length(substr(hb."Braced", 2)) - length(ltrim(substr(hb."Braced", 2), '0123456789')))
                        ELSE substring(s."DisambiguatedStrongs" FROM 'H\d+')
                   END
                 , COALESCE(NULLIF(hb."Braced", ''), s."DisambiguatedStrongs")
                 , s."Hebrew"
                 , CASE WHEN hb."Braced" <> '' AND COALESCE(i."PartOfSpeech", '') NOT IN ('DET', 'ADP', 'CCONJ', 'SCONJ', 'PRON', 'AUX', 'PART')
                        THEN left(CASE WHEN hb."Index" = 1 AND left(split_part(s."Grammar", '/', hb."Index"), 1) IN ('H', 'A') THEN substr(split_part(s."Grammar", '/', hb."Index"), 2)
                                       ELSE split_part(s."Grammar", '/', hb."Index") END, 1)
                   END
            FROM "KeyIds" i
                JOIN "unshackled-word"."Elb1871HebrewMapping" h ON h."ElbWordId" = i."WordId"
                LEFT JOIN "unshackled-word"."StepHebrewWords" s ON s."Id" = h."StepWordId"
                CROSS JOIN LATERAL (SELECT CASE WHEN position('{' IN s."DisambiguatedStrongs") > 0 THEN split_part(split_part(s."DisambiguatedStrongs", '{', 2), '}', 1) ELSE '' END AS "Braced"
                                         , length(split_part(s."DisambiguatedStrongs", '{', 1)) - length(replace(split_part(s."DisambiguatedStrongs", '{', 1), '/', '')) + 1 AS "Index") hb
            WHERE i."BookId" <= 39
        )
        , "OrigAgg" AS
        (
            SELECT r."WordId"                AS "WordId"
                 , count(r."StepId")         AS "Steps"
                 , bool_or(r."IsAdded")      AS "Added"
                 , min(r."Key")              AS "MinKey"
                 , max(r."Key")              AS "MaxKey"
                 , min(r."StepId")           AS "FirstStep"
            FROM "OrigRows" r
            GROUP BY r."WordId"
        )
        , "OrigMulti" AS
        (
            SELECT r."WordId" AS "WordId"
                 , string_agg(DISTINCT r."Key", '+' ORDER BY r."Key") AS "Key"
            FROM "OrigRows" r
                JOIN "OrigAgg" a ON a."WordId" = r."WordId" AND a."MinKey" <> a."MaxKey"
            WHERE r."StepId" IS NOT NULL
            GROUP BY r."WordId"
        )
        , "OrigKeys" AS
        (
            SELECT i."WordId" AS "WordId"
                 , CASE WHEN COALESCE(a."Steps", 0) > 0 THEN CASE WHEN a."MinKey" <> a."MaxKey" THEN 'multi' ELSE 'aligned' END
                        WHEN COALESCE(a."Added", false) THEN 'added'
                        ELSE 'none'
                   END AS "Kind"
                 , CASE WHEN COALESCE(a."Steps", 0) > 0 THEN COALESCE(mu."Key", a."MinKey", 'none')
                        WHEN COALESCE(a."Added", false) THEN 'added'
                        ELSE 'none'
                   END AS "Key"
                 , f."Strongs" AS "Strongs"
                 , f."Word"    AS "Word"
                 , f."Pos"     AS "Pos"
            FROM "KeyIds" i
                LEFT JOIN "OrigAgg" a ON a."WordId" = i."WordId"
                LEFT JOIN "OrigMulti" mu ON mu."WordId" = i."WordId"
                LEFT JOIN "OrigRows" f ON f."WordId" = a."WordId" AND f."StepId" = a."FirstStep"
        )
        """;

    // One statement per search: the matching set and the original keys are computed once, every summary and the page
    // read from them. Result: a single row of scalars / json texts.
    private const string OccurrencesSql = "WITH " + MatchedDef + """

        , "KeyIds" AS (SELECT "WordId", "BookId", "PartOfSpeech" FROM "Matched")
        ,
        """ + OriginalKeyCtes + """

        , "Keyed" AS MATERIALIZED
        (
            SELECT mt.*
                 , k."Kind"    AS "OrigKind"
                 , k."Key"     AS "OrigKey"
                 , k."Strongs" AS "OrigStrongs"
                 , k."Word"    AS "OrigWord"
                 , k."Pos"     AS "OrigPos"
            FROM "Matched" mt
                JOIN "OrigKeys" k ON k."WordId" = mt."WordId"
        )
        , "Filtered" AS NOT MATERIALIZED
        (
            SELECT * FROM "Keyed" WHERE cardinality(@original::text[]) = 0 OR "OrigKey" = ANY(@original)
        )
        , "Cells" AS
        (
            SELECT "OrigKey", "OrigKind", "OrigPos", "PartOfSpeech", count(*) AS "Count", min("OrigWord") AS "Word"
            FROM "Keyed"
            GROUP BY "OrigKey", "OrigKind", "OrigPos", "PartOfSpeech"
        )
        , "KeyLex" AS
        (
            SELECT DISTINCT ON (k."OrigKey") k."OrigKey", l."OriginalWord", l."Gloss"
            FROM (SELECT DISTINCT "OrigKey" FROM "Cells" WHERE "OrigKind" = 'aligned') k
                JOIN "unshackled-word"."StepStrongsLexicon" l ON l."LanguageId" = CASE WHEN left(k."OrigKey", 1) = 'G' THEN 2 ELSE 0 END
                    AND l."Number" = substring(k."OrigKey" FROM 2)::int
            ORDER BY k."OrigKey", (COALESCE(l."Extra", '') = '') DESC, l."Extra"
        )
        , "Page" AS
        (
            SELECT * FROM "Filtered" ORDER BY "BookId", "Chapter", "Verse", "PositionInVerse" OFFSET @offset LIMIT @pageSize
        )
        SELECT (SELECT count(*) FROM "Filtered") AS "Total"
             , (SELECT COALESCE(json_agg(json_build_object('bookId', x."BookId", 'count', x."Count") ORDER BY x."BookId"), '[]')::text
                FROM (SELECT "BookId", count(*) AS "Count" FROM "Filtered" GROUP BY "BookId") x) AS "ByBook"
             , (SELECT COALESCE(json_agg(json_build_object('lemma', x."Lemma", 'count', x."Count") ORDER BY x."Count" DESC, x."Lemma"), '[]')::text
                FROM (SELECT "Lemma", count(*) AS "Count" FROM "Filtered" GROUP BY "Lemma") x) AS "ByLemma"
             , (SELECT COALESCE(json_agg(json_build_object('origKey', x."OrigKey", 'plainWord', x."PlainWord", 'partOfSpeech', x."PartOfSpeech", 'count', x."Count")
                                         ORDER BY x."Count" DESC, x."PlainWord", x."PartOfSpeech", x."OrigKey"), '[]')::text
                FROM (SELECT "OrigKey", "PlainWord", "PartOfSpeech", count(*) AS "Count" FROM "Filtered" GROUP BY "OrigKey", "PlainWord", "PartOfSpeech") x) AS "Groups"
             , (SELECT COALESCE(json_agg(json_build_object('plainWord', x."PlainWord", 'partOfSpeech', x."PartOfSpeech", 'count', x."Count")
                                         ORDER BY x."Count" DESC, x."PlainWord", x."PartOfSpeech"), '[]')::text
                FROM (SELECT "PlainWord", "PartOfSpeech", count(*) AS "Count" FROM "Filtered" GROUP BY "PlainWord", "PartOfSpeech") x) AS "FormGroups"
             , (SELECT COALESCE(json_agg(json_build_object('key', c."OrigKey", 'kind', c."OrigKind", 'word', COALESCE(kl."OriginalWord", c."Word"), 'gloss', kl."Gloss",
                                                            'origPos', c."OrigPos", 'germanPos', c."PartOfSpeech", 'count', c."Count")), '[]')::text
                FROM "Cells" c LEFT JOIN "KeyLex" kl ON kl."OrigKey" = c."OrigKey") AS "Cells"
             , (SELECT COALESCE(json_agg(json_build_object(
                                    'wordId', p."WordId", 'bookId', p."BookId", 'chapter', p."Chapter", 'verse', p."Verse", 'positionInVerse', p."PositionInVerse",
                                    'lemma', p."Lemma", 'wordInContext', p."WordInContext", 'plainWord', p."PlainWord", 'partOfSpeech', p."PartOfSpeech",
                                    'case', p."Case", 'number', p."Number", 'gender', p."Gender",
                                    'origKind', p."OrigKind", 'origKey', p."OrigKey", 'origStrongs', p."OrigStrongs", 'origWord', p."OrigWord", 'origPos', p."OrigPos",
                                    'left', p."Left", 'right', p."Right")
                                 ORDER BY p."BookId", p."Chapter", p."Verse", p."PositionInVerse"), '[]')::text
                FROM (SELECT pg.*
                           , COALESCE((SELECT string_agg(x."WordInContext", ' ' ORDER BY x."PositionInVerse")
                                       FROM "unshackled-word"."Elb1871Words" x
                                       WHERE x."BibleBookId" = pg."BookId" AND x."Chapter" = pg."Chapter" AND x."Verse" = pg."Verse"
                                           AND x."PositionInVerse" BETWEEN pg."PositionInVerse" - @context AND pg."PositionInVerse" - 1), '') AS "Left"
                           , COALESCE((SELECT string_agg(x."WordInContext", ' ' ORDER BY x."PositionInVerse")
                                       FROM "unshackled-word"."Elb1871Words" x
                                       WHERE x."BibleBookId" = pg."BookId" AND x."Chapter" = pg."Chapter" AND x."Verse" = pg."Verse"
                                           AND x."PositionInVerse" BETWEEN pg."PositionInVerse" + 1 AND pg."PositionInVerse" + @context), '') AS "Right"
                      FROM "Page" pg) p) AS "Page"
        """;

    private const string WordIdsTail = """

        SELECT (SELECT count(*) FROM "Filtered") AS "Total"
             , (SELECT COALESCE(array_agg(x."WordId" ORDER BY x."BookId", x."Chapter", x."Verse", x."PositionInVerse"), '{}')
                FROM (SELECT "WordId", "BookId", "Chapter", "Verse", "PositionInVerse" FROM "Filtered" ORDER BY "BookId", "Chapter", "Verse", "PositionInVerse" LIMIT @limit) x) AS "WordIds"
        """;

    private const string WordIdsPlainSql = "WITH " + MatchedDef + """

        , "Filtered" AS (SELECT * FROM "Matched")
        """ + WordIdsTail;

    private const string WordIdsKeyedSql = "WITH " + MatchedDef + """

        , "KeyIds" AS (SELECT "WordId", "BookId", "PartOfSpeech" FROM "Matched")
        ,
        """ + OriginalKeyCtes + """

        , "Filtered" AS
        (
            SELECT mt.* FROM "Matched" mt JOIN "OrigKeys" k ON k."WordId" = mt."WordId" WHERE k."Key" = ANY(@original)
        )
        """ + WordIdsTail;

    private const string OriginalKeysOfWordsSql = """
        WITH "KeyIds" AS
        (
            SELECT w."Id" AS "WordId", w."BibleBookId" AS "BookId", NULL::text AS "PartOfSpeech" FROM "unshackled-word"."Elb1871Words" w WHERE w."Id" = ANY(@wordIds)
        )
        ,
        """ + OriginalKeyCtes + """

        SELECT "WordId", "Key" FROM "OrigKeys"
        """;

    private const string LemmaCountsSql = """
        SELECT m."Lemma" AS "Lemma"
             , count(*)  AS "Count"
        FROM "unshackled-word"."Elb1871Morphology" m
        WHERE lower(m."Lemma") LIKE lower(@pattern)
        GROUP BY m."Lemma"
        ORDER BY count(*) DESC, m."Lemma"
        LIMIT @limit
        """;

    private const string CaseVariantsSql = """
        SELECT m."Lemma" AS "Lemma"
             , count(*)  AS "Count"
        FROM "unshackled-word"."Elb1871Morphology" m
        WHERE lower(m."Lemma") = ANY(@lowered)
        GROUP BY m."Lemma"
        """;

    private const string LockAndReadSql = """
        SELECT w."Id"    AS "WordId"
             , m."Lemma" AS "Lemma"
        FROM unnest(@wordIds) AS i("WordId")
            JOIN "unshackled-word"."Elb1871Words" w ON w."Id" = i."WordId"
            JOIN "unshackled-word"."Elb1871Morphology" m ON m."HebRefId" = w."HebRefId" AND m."PositionInVerse" = w."PositionInVerse"
        ORDER BY m."Id"
        FOR UPDATE OF m
        """;

    private const string PartOfSpeechOfWordsSql = """
        SELECT w."Id"           AS "WordId"
             , m."PartOfSpeech" AS "PartOfSpeech"
        FROM unnest(@wordIds) AS i("WordId")
            JOIN "unshackled-word"."Elb1871Words" w ON w."Id" = i."WordId"
            JOIN "unshackled-word"."Elb1871Morphology" m ON m."HebRefId" = w."HebRefId" AND m."PositionInVerse" = w."PositionInVerse"
        """;

    private const string AllowedUposSql = """
        SELECT s."AllowedUpos" AS "AllowedUpos"
        FROM "unshackled-word"."SttsTags" s
        WHERE s."Tag" = @stts
        """;

    private const string InsertChangeSetSql = """
        INSERT INTO "unshackled-word"."Elb1871MorphologyChangeSets" ("UserId", "Kind", "FromLemma", "ToLemma", "Scope", "Note", "WordCount", "RevertOf")
        VALUES (@userId, @kind, @fromLemma, @toLemma, @scope::jsonb, @note, @wordCount, @revertOf)
        RETURNING "Id"
        """;

    private const string ApplySql = """
        WITH "Old" AS
        (
            SELECT m."Id"    AS "MorphologyId"
                 , w."Id"    AS "WordId"
                 , m."Lemma" AS "OldLemma"
                 , m."Stts"  AS "OldStts"
                 , k."Key"   AS "OrigKey"
            FROM "unshackled-word"."Elb1871Morphology" m
                JOIN "unshackled-word"."Elb1871Words" w ON w."HebRefId" = m."HebRefId" AND w."PositionInVerse" = m."PositionInVerse"
                JOIN unnest(@wordIds, @origKeys) AS k("WordId", "Key") ON k."WordId" = w."Id"
            WHERE w."Id" = ANY(@wordIds)
                AND m."Lemma" = ANY(@from)
        )
        , "Updated" AS
        (
            UPDATE "unshackled-word"."Elb1871Morphology" m
            SET "Lemma" = @to
              , "Stts" = COALESCE(@stts::varchar, m."Stts")
            FROM "Old" o
            WHERE m."Id" = o."MorphologyId"
            RETURNING m."HebRefId", m."PositionInVerse", m."PartOfSpeech", m."Stts", m."Degree", m."VerbForm", m."Tense", m."Person", m."Number", m."Mood", m."Case", m."Gender", o."WordId", o."OldLemma", o."OldStts", o."OrigKey"
        )
        , "Audited" AS
        (
            INSERT INTO "unshackled-word"."Elb1871MorphologyEdits" ("ElbWordId", "HebRefId", "PositionInVerse", "UserId", "ChangeSetId", "OriginalKey", "BeforeValues", "AfterValues")
            SELECT u."WordId"
                 , u."HebRefId"
                 , u."PositionInVerse"
                 , @userId
                 , @changeSetId
                 , u."OrigKey"
                 , jsonb_build_object('Lemma', u."OldLemma", 'PartOfSpeech', u."PartOfSpeech", 'Stts', u."OldStts", 'Degree', u."Degree", 'VerbForm', u."VerbForm", 'Tense', u."Tense", 'Person', u."Person", 'Number', u."Number", 'Mood', u."Mood", 'Case', u."Case", 'Gender', u."Gender")
                 , jsonb_build_object('Lemma', @to::text, 'PartOfSpeech', u."PartOfSpeech", 'Stts', u."Stts", 'Degree', u."Degree", 'VerbForm', u."VerbForm", 'Tense', u."Tense", 'Person', u."Person", 'Number', u."Number", 'Mood', u."Mood", 'Case', u."Case", 'Gender', u."Gender")
            FROM "Updated" u
            RETURNING 1
        )
        SELECT count(*) FROM "Audited"
        """;

    private const string SelectChangeSetSql = """
        SELECT cs."Kind"       AS "Kind"
             , cs."ToLemma"    AS "ToLemma"
             , cs."RevertedAt" AS "RevertedAt"
        FROM "unshackled-word"."Elb1871MorphologyChangeSets" cs
        WHERE cs."Id" = @changeSetId
        FOR UPDATE
        """;

    private const string RevertSql = """
        WITH "Candidates" AS
        (
            SELECT m."Id"                       AS "MorphologyId"
                 , e."ElbWordId"                AS "WordId"
                 , e."BeforeValues" ->> 'Lemma' AS "RestoredLemma"
                 , m."Stts"                     AS "CurrentStts"
                 , CASE WHEN e."BeforeValues" ? 'Stts' AND m."Stts" IS NOT DISTINCT FROM e."AfterValues" ->> 'Stts' THEN e."BeforeValues" ->> 'Stts' ELSE m."Stts" END AS "RestoredStts"
                 , e."OriginalKey"              AS "OriginalKey"
                 , m."Lemma"                    AS "CurrentLemma"
            FROM "unshackled-word"."Elb1871MorphologyEdits" e
                JOIN "unshackled-word"."Elb1871Morphology" m ON m."HebRefId" = e."HebRefId" AND m."PositionInVerse" = e."PositionInVerse"
            WHERE e."ChangeSetId" = @changeSetId
                AND m."Lemma" = e."AfterValues" ->> 'Lemma'
            ORDER BY m."Id"
            FOR UPDATE OF m
        )
        , "Updated" AS
        (
            UPDATE "unshackled-word"."Elb1871Morphology" m
            SET "Lemma" = c."RestoredLemma"
              , "Stts" = c."RestoredStts"
            FROM "Candidates" c
            WHERE m."Id" = c."MorphologyId"
            RETURNING m."HebRefId", m."PositionInVerse", m."PartOfSpeech", m."Degree", m."VerbForm", m."Tense", m."Person", m."Number", m."Mood", m."Case", m."Gender", c."WordId", c."CurrentLemma", c."RestoredLemma", c."CurrentStts", c."RestoredStts", c."OriginalKey"
        )
        , "Audited" AS
        (
            INSERT INTO "unshackled-word"."Elb1871MorphologyEdits" ("ElbWordId", "HebRefId", "PositionInVerse", "UserId", "ChangeSetId", "OriginalKey", "BeforeValues", "AfterValues")
            SELECT u."WordId"
                 , u."HebRefId"
                 , u."PositionInVerse"
                 , @userId
                 , @newChangeSetId
                 , u."OriginalKey"
                 , jsonb_build_object('Lemma', u."CurrentLemma", 'PartOfSpeech', u."PartOfSpeech", 'Stts', u."CurrentStts", 'Degree', u."Degree", 'VerbForm', u."VerbForm", 'Tense', u."Tense", 'Person', u."Person", 'Number', u."Number", 'Mood', u."Mood", 'Case', u."Case", 'Gender', u."Gender")
                 , jsonb_build_object('Lemma', u."RestoredLemma", 'PartOfSpeech', u."PartOfSpeech", 'Stts', u."RestoredStts", 'Degree', u."Degree", 'VerbForm', u."VerbForm", 'Tense', u."Tense", 'Person', u."Person", 'Number', u."Number", 'Mood', u."Mood", 'Case', u."Case", 'Gender', u."Gender")
            FROM "Updated" u
            RETURNING 1
        )
        SELECT count(*) FROM "Audited"
        """;

    // Keeps Elb1871Verses."LemmaText" (and with it the verse search vector) in step with the morphology rows written in the same transaction.
    private const string RefreshLemmaTextOfWordsSql = """
        SELECT "unshackled-word"."RefreshVerseLemmaText"(ARRAY(SELECT DISTINCT w."HebRefId" FROM "unshackled-word"."Elb1871Words" w WHERE w."Id" = ANY(@wordIds)))
        """;

    private const string RefreshLemmaTextOfChangeSetSql = """
        SELECT "unshackled-word"."RefreshVerseLemmaText"(ARRAY(SELECT DISTINCT e."HebRefId" FROM "unshackled-word"."Elb1871MorphologyEdits" e WHERE e."ChangeSetId" = @changeSetId))
        """;

    private const string CountSetRowsSql = """
        SELECT count(*) FROM "unshackled-word"."Elb1871MorphologyEdits" e WHERE e."ChangeSetId" = @changeSetId
        """;

    private const string FinishRevertSql = """
        UPDATE "unshackled-word"."Elb1871MorphologyChangeSets" SET "WordCount" = @restored WHERE "Id" = @newChangeSetId;
        UPDATE "unshackled-word"."Elb1871MorphologyChangeSets" SET "RevertedAt" = now() WHERE "Id" = @changeSetId
        """;

    private static object FilterParams(BulkFilterRequest r)
    {
        var (bookFrom, bookTo) = LemmaBulkRules.BookRange(r.Testament, r.BookId);
        return new
        {
            from = LemmaBulkRules.NormalizeFrom(r.From).ToArray(),
            bookFrom,
            bookTo,
            chapter = r.Chapter,
            partOfSpeech = r.PartOfSpeech,
            plainWord = r.PlainWord,
            original = r.Original.Distinct(StringComparer.Ordinal).ToArray(),
        };
    }

    public async Task<IReadOnlyList<LemmaCountItem>> GetLemmaCountsAsync(string q, int limit, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        var items = (await connection.QueryAsync<LemmaCountItem>(new CommandDefinition(LemmaCountsSql, new { pattern = LemmaBulkRules.LikePrefix(q.Trim()), limit }, cancellationToken: ct))).ToList();
        if (items.Count == 0) return items;

        var lowered = items.Select(i => i.Lemma.ToLowerInvariant()).Distinct().ToArray();
        var variants = (await connection.QueryAsync<LemmaCaseVariant>(new CommandDefinition(CaseVariantsSql, new { lowered }, cancellationToken: ct))).ToList();
        foreach (var item in items)
        {
            item.CaseVariants = variants
                .Where(v => !string.Equals(v.Lemma, item.Lemma, StringComparison.Ordinal) && string.Equals(v.Lemma, item.Lemma, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(v => v.Count)
                .ToList();
        }

        return items;
    }

    public async Task<BulkOccurrencesResponse> GetOccurrencesAsync(BulkOccurrencesRequest request, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        var p = new DynamicParameters(FilterParams(request));
        p.Add("offset", (request.Page - 1) * request.PageSize);
        p.Add("pageSize", request.PageSize);
        p.Add("context", LemmaBulkRules.ContextWords);

        var row = await connection.QuerySingleAsync<OccurrencesRow>(new CommandDefinition(OccurrencesSql, p, cancellationToken: ct));

        var cells = Deserialize<List<CellRaw>>(row.Cells)
            .Select(c => new OriginalCell(c.Key, c.Kind, c.Word, c.Gloss, c.OrigPos, c.GermanPos, c.Count))
            .ToList();
        var summary = OriginalLanguageRules.Summarize(cells);

        var occurrences = Deserialize<List<BulkOccurrence>>(row.Page);
        foreach (var o in occurrences)
            o.PosConflict = OriginalLanguageRules.RowPosConflict(o.PartOfSpeech, o.OrigPos, o.OrigKey ?? string.Empty, o.OrigKind ?? string.Empty);

        return new BulkOccurrencesResponse
        {
            From = LemmaBulkRules.NormalizeFrom(request.From),
            Page = request.Page,
            PageSize = request.PageSize,
            Total = row.Total,
            TotalPages = (int)((row.Total + request.PageSize - 1) / request.PageSize),
            ByBook = Deserialize<List<BulkBookCount>>(row.ByBook),
            ByLemma = Deserialize<List<BulkLemmaCount>>(row.ByLemma),
            Groups = Deserialize<List<BulkGroupCount>>(row.Groups),
            FormGroups = Deserialize<List<BulkGroupCount>>(row.FormGroups),
            ByOriginal = summary.Groups.Select(g => new BulkOriginalCount
            {
                Key = g.Key, Kind = g.Kind, Lang = g.Lang, Word = g.Word, Gloss = g.Gloss, Pos = g.Pos, Count = g.Count,
                Share = Math.Round(g.Share, 4), Minority = g.Minority, PosConflict = g.PosConflict, Unmapped = g.Unmapped, Added = g.Added,
            }).ToList(),
            Ambiguous = summary.Ambiguous,
            Occurrences = occurrences,
        };
    }

    public async Task<BulkWordIdsResponse> GetWordIdsAsync(BulkFilterRequest request, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        var p = new DynamicParameters(FilterParams(request));
        p.Add("limit", LemmaBulkRules.MaxApplyWords);

        var sql = request.Original.Length == 0 ? WordIdsPlainSql : WordIdsKeyedSql;
        var row = await connection.QuerySingleAsync<WordIdsRow>(new CommandDefinition(sql, p, cancellationToken: ct));
        return new BulkWordIdsResponse { Total = row.Total, Truncated = row.Total > row.WordIds.Length, WordIds = row.WordIds };
    }

    private static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonOptions)!;

    private sealed class OccurrencesRow
    {
        public long Total { get; set; }
        public string ByBook { get; set; } = "[]";
        public string ByLemma { get; set; } = "[]";
        public string Groups { get; set; } = "[]";
        public string FormGroups { get; set; } = "[]";
        public string Cells { get; set; } = "[]";
        public string Page { get; set; } = "[]";
    }

    private sealed class WordIdsRow
    {
        public long Total { get; set; }
        public int[] WordIds { get; set; } = [];
    }

    private sealed class CellRaw
    {
        public string Key { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string? Word { get; set; }
        public string? Gloss { get; set; }
        public string? OrigPos { get; set; }
        public string? GermanPos { get; set; }
        public long Count { get; set; }
    }

    private sealed class KeyRow
    {
        public int WordId { get; set; }
        public string Key { get; set; } = string.Empty;
    }

    public async Task<(IReadOnlyList<BulkStaleDetail> Stale, int ChangeSetId, int Updated)> ApplyAsync(BulkApplyRequest request, int userId, CancellationToken ct)
    {
        var from = LemmaBulkRules.NormalizeFrom(request.From);
        var to = LemmaBulkRules.Normalize(request.To);
        var wordIds = request.WordIds.ToArray();

        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        using var transaction = connection.BeginTransaction();

        var current = (await connection.QueryAsync<LockedRow>(
            new CommandDefinition(LockAndReadSql, new { wordIds }, transaction, cancellationToken: ct))).ToDictionary(r => r.WordId, r => r.Lemma);

        // The original keys are recomputed here, in the same transaction, from the current mapping (content comparison:
        // mapping saves delete and reinsert rows, so ids and timestamps say nothing).
        var keys = (await connection.QueryAsync<KeyRow>(
            new CommandDefinition(OriginalKeysOfWordsSql, new { wordIds }, transaction, cancellationToken: ct))).ToDictionary(r => r.WordId, r => r.Key);

        var staleLemma = LemmaBulkRules.FindStale(wordIds, current, from).ToHashSet();
        var details = new List<BulkStaleDetail>();
        for (var i = 0; i < wordIds.Length; i++)
        {
            var id = wordIds[i];
            if (staleLemma.Contains(id))
            {
                details.Add(new BulkStaleDetail { WordId = id, Reason = "lemma", Lemma = current.GetValueOrDefault(id) });
                continue;
            }

            var actual = keys.GetValueOrDefault(id, OriginalLanguageRules.KindNone);
            if (!OriginalLanguageRules.KeyMatchesExpected(request.ExpectedOriginal[i], actual))
                details.Add(new BulkStaleDetail { WordId = id, Reason = "original", OrigKey = actual });
        }

        if (request.Stts is not null && details.Count == 0)
        {
            // Every listed row is edited, so the requested tag has to be allowed for its part of speech.
            var allowed = await connection.QuerySingleOrDefaultAsync<string[]>(new CommandDefinition(AllowedUposSql, new { stts = request.Stts }, transaction, cancellationToken: ct)) ?? [];
            var partsOfSpeech = (await connection.QueryAsync<PosRow>(new CommandDefinition(PartOfSpeechOfWordsSql, new { wordIds }, transaction, cancellationToken: ct)))
                .ToDictionary(r => r.WordId, r => r.PartOfSpeech);
            details.AddRange(wordIds
                .Where(id => !partsOfSpeech.TryGetValue(id, out var pos) || !allowed.Contains(pos))
                .Select(id => new BulkStaleDetail { WordId = id, Reason = "stts" }));
        }

        if (details.Count > 0)
        {
            transaction.Rollback();
            return (details, 0, 0);
        }

        var changeSetId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(InsertChangeSetSql, new
        {
            userId,
            kind = "bulkLemma",
            fromLemma = string.Join(" | ", from),
            toLemma = to,
            scope = JsonSerializer.Serialize(new { from, original = wordIds.Select(id => keys[id]).Distinct().Order(StringComparer.Ordinal).ToArray() }),
            note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            wordCount = wordIds.Length,
            revertOf = (int?)null,
        }, transaction, cancellationToken: ct));

        var updated = await connection.ExecuteScalarAsync<int>(new CommandDefinition(ApplySql, new
        {
            wordIds,
            origKeys = wordIds.Select(id => keys[id]).ToArray(),
            from = from.ToArray(),
            to,
            stts = request.Stts,
            userId,
            changeSetId,
        }, transaction, cancellationToken: ct));

        if (updated != wordIds.Length)
        {
            transaction.Rollback();
            throw new InvalidOperationException($"Bulk lemma update touched {updated} rows, expected {wordIds.Length}; rolled back.");
        }

        await connection.ExecuteAsync(new CommandDefinition(RefreshLemmaTextOfWordsSql, new { wordIds }, transaction, cancellationToken: ct));

        transaction.Commit();
        return ([], changeSetId, updated);
    }

    public async Task<RevertResult> RevertAsync(int changeSetId, int userId, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        using var transaction = connection.BeginTransaction();

        var set = await connection.QuerySingleOrDefaultAsync<ChangeSetRow>(
            new CommandDefinition(SelectChangeSetSql, new { changeSetId }, transaction, cancellationToken: ct));
        if (set is null) return new RevertResult(RevertStatus.NotFound);
        if (set.RevertedAt is not null) return new RevertResult(RevertStatus.AlreadyReverted);
        if (set.Kind != "bulkLemma") return new RevertResult(RevertStatus.NotRevertable);

        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(CountSetRowsSql, new { changeSetId }, transaction, cancellationToken: ct));

        var newChangeSetId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(InsertChangeSetSql, new
        {
            userId,
            kind = "revert",
            fromLemma = set.ToLemma,
            toLemma = (string?)null,
            scope = (string?)null,
            note = (string?)null,
            wordCount = 0,
            revertOf = changeSetId,
        }, transaction, cancellationToken: ct));

        var restored = await connection.ExecuteScalarAsync<int>(new CommandDefinition(RevertSql, new { changeSetId, newChangeSetId, userId }, transaction, cancellationToken: ct));
        if (restored == 0)
        {
            transaction.Rollback();
            return new RevertResult(RevertStatus.Ok, 0, 0, total);
        }

        await connection.ExecuteAsync(new CommandDefinition(FinishRevertSql, new { restored, newChangeSetId, changeSetId }, transaction, cancellationToken: ct));
        await connection.ExecuteAsync(new CommandDefinition(RefreshLemmaTextOfChangeSetSql, new { changeSetId }, transaction, cancellationToken: ct));
        transaction.Commit();
        return new RevertResult(RevertStatus.Ok, newChangeSetId, restored, total - restored);
    }

    private sealed class PosRow
    {
        public int WordId { get; init; }
        public string PartOfSpeech { get; init; } = string.Empty;
    }

    private sealed class LockedRow
    {
        public int WordId { get; set; }
        public string Lemma { get; set; } = string.Empty;
    }

    private sealed class ChangeSetRow
    {
        public string Kind { get; set; } = string.Empty;
        public string? ToLemma { get; set; }
        public DateTimeOffset? RevertedAt { get; set; }
    }
}
