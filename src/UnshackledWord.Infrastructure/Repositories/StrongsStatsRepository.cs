using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Infrastructure.Repositories;

/// <summary>
/// The German word key is the lower-cased lemma, falling back to the exact form: Elb1871Words.Lemma is empty in the DB today
/// (the Elb1871Lemmatizer output is not seeded yet), so for now the key is the lower-cased form. The expression must stay identical
/// to the index IX_Elb1871Words_GermanKey_Book (migration 0007).
/// </summary>
public sealed class StrongsStatsRepository : IStrongsStatsRepository
{
    private readonly IDbReader _dbReader;

    public StrongsStatsRepository(IDbReader dbReader)
    {
        _dbReader = dbReader;
    }

    public async Task<IReadOnlyList<StrongsBookCountRow>> GetBookCountsAsync(IReadOnlyList<string> keys, bool hebrew, CancellationToken token = default)
    {
        const string greekSql = """
                                WITH verse_map AS (
                                    SELECT DISTINCT ON (v."LxxRefId") v."LxxRefId"
                                         , v."HebRefId"
                                    FROM "unshackled-word"."Elb1871Verses" v
                                    ORDER BY v."LxxRefId", v."Id"
                                ), hits AS (
                                    SELECT COALESCE(vm."HebRefId", sgw."LxxRefId") "ElbRefId"
                                    FROM "unshackled-word"."StepGreekWords" sgw
                                        LEFT JOIN verse_map vm ON vm."LxxRefId" = sgw."LxxRefId"
                                    WHERE 1=1
                                        AND sgw."DisambiguatedStrongs" = ANY(@Keys)
                                )
                                SELECT (hits."ElbRefId" / 1000000)::int "BibleBookId"
                                     , COUNT(*)::int "Count"
                                     , COUNT(DISTINCT hits."ElbRefId")::int "VerseCount"
                                     , 0 "AramaicCount"
                                FROM hits
                                GROUP BY 1
                                ORDER BY 1
                                """;

        const string hebrewSql = """
                                 WITH verse_map AS (
                                     SELECT DISTINCT ON (v."LxxRefId") v."LxxRefId"
                                          , v."HebRefId"
                                     FROM "unshackled-word"."Elb1871Verses" v
                                     ORDER BY v."LxxRefId", v."Id"
                                 ), hits AS (
                                     SELECT COALESCE(vm."HebRefId", shw."LxxRefId") "ElbRefId"
                                          , shw."Grammar"
                                     FROM "unshackled-word"."StepHebrewWords" shw
                                         LEFT JOIN verse_map vm ON vm."LxxRefId" = shw."LxxRefId"
                                     WHERE 1=1
                                         AND split_part(shw."RootDisambiguatedStrongsInstance", '_', 1) = ANY(@Keys)
                                 )
                                 SELECT (hits."ElbRefId" / 1000000)::int "BibleBookId"
                                      , COUNT(*)::int "Count"
                                      , COUNT(DISTINCT hits."ElbRefId")::int "VerseCount"
                                      , (COUNT(*) FILTER (WHERE hits."Grammar" LIKE 'A%'))::int "AramaicCount"
                                 FROM hits
                                 GROUP BY 1
                                 ORDER BY 1
                                 """;

        var rows = await _dbReader.ReadAsListAsync<StrongsBookCountRow>(hebrew ? hebrewSql : greekSql, new { Keys = keys.ToArray() });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<StrongsKeyCountRow>> GetKeyCountsAsync(IReadOnlyList<string> keys, bool hebrew, CancellationToken token = default)
    {
        const string greekSql = """
                                SELECT sgw."DisambiguatedStrongs" "Strongs"
                                     , COUNT(*)::int "Count"
                                FROM "unshackled-word"."StepGreekWords" sgw
                                WHERE 1=1
                                    AND sgw."DisambiguatedStrongs" = ANY(@Keys)
                                GROUP BY sgw."DisambiguatedStrongs"
                                """;

        const string hebrewSql = """
                                 SELECT split_part(shw."RootDisambiguatedStrongsInstance", '_', 1) "Strongs"
                                      , COUNT(*)::int "Count"
                                 FROM "unshackled-word"."StepHebrewWords" shw
                                 WHERE 1=1
                                     AND split_part(shw."RootDisambiguatedStrongsInstance", '_', 1) = ANY(@Keys)
                                 GROUP BY 1
                                 """;

        var rows = await _dbReader.ReadAsListAsync<StrongsKeyCountRow>(hebrew ? hebrewSql : greekSql, new { Keys = keys.ToArray() });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<StrongsRenderingRow>> GetRenderingsAsync(IReadOnlyList<string> keys, bool hebrew, CancellationToken token = default)
    {
        const string greekSql = """
                                SELECT MODE() WITHIN GROUP (ORDER BY COALESCE(NULLIF(ew."Lemma", ''), ew."PlainWord")) "Word"
                                     , LOWER(COALESCE(NULLIF(ew."Lemma", ''), ew."PlainWord")) "Key"
                                     , COUNT(DISTINCT sgw."Id")::int "Count"
                                FROM "unshackled-word"."StepGreekWords" sgw
                                    JOIN "unshackled-word"."Elb1871GreekMapping" egm ON egm."StepWordId" = sgw."Id"
                                    JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = egm."ElbWordId"
                                WHERE 1=1
                                    AND sgw."DisambiguatedStrongs" = ANY(@Keys)
                                    AND NOT egm."IsAddedWord"
                                    AND ew."PlainWord" <> ''
                                GROUP BY LOWER(COALESCE(NULLIF(ew."Lemma", ''), ew."PlainWord"))
                                ORDER BY COUNT(DISTINCT sgw."Id") DESC, LOWER(COALESCE(NULLIF(ew."Lemma", ''), ew."PlainWord"))
                                """;

        const string hebrewSql = """
                                 SELECT MODE() WITHIN GROUP (ORDER BY COALESCE(NULLIF(ew."Lemma", ''), ew."PlainWord")) "Word"
                                      , LOWER(COALESCE(NULLIF(ew."Lemma", ''), ew."PlainWord")) "Key"
                                      , COUNT(DISTINCT shw."Id")::int "Count"
                                 FROM "unshackled-word"."StepHebrewWords" shw
                                     JOIN "unshackled-word"."Elb1871HebrewMapping" ehm ON ehm."StepWordId" = shw."Id"
                                     JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = ehm."ElbWordId"
                                 WHERE 1=1
                                     AND split_part(shw."RootDisambiguatedStrongsInstance", '_', 1) = ANY(@Keys)
                                     AND NOT ehm."IsAddedWord"
                                     AND ew."PlainWord" <> ''
                                 GROUP BY LOWER(COALESCE(NULLIF(ew."Lemma", ''), ew."PlainWord"))
                                 ORDER BY COUNT(DISTINCT shw."Id") DESC, LOWER(COALESCE(NULLIF(ew."Lemma", ''), ew."PlainWord"))
                                 """;

        var rows = await _dbReader.ReadAsListAsync<StrongsRenderingRow>(hebrew ? hebrewSql : greekSql, new { Keys = keys.ToArray() });
        return rows.ToList();
    }

    public async Task<int> GetMappedCountAsync(IReadOnlyList<string> keys, bool hebrew, CancellationToken token = default)
    {
        const string greekSql = """
                                SELECT COUNT(DISTINCT egm."StepWordId")::int
                                FROM "unshackled-word"."StepGreekWords" sgw
                                    JOIN "unshackled-word"."Elb1871GreekMapping" egm ON egm."StepWordId" = sgw."Id"
                                WHERE 1=1
                                    AND sgw."DisambiguatedStrongs" = ANY(@Keys)
                                    AND NOT egm."IsAddedWord"
                                """;

        const string hebrewSql = """
                                 SELECT COUNT(DISTINCT ehm."StepWordId")::int
                                 FROM "unshackled-word"."StepHebrewWords" shw
                                     JOIN "unshackled-word"."Elb1871HebrewMapping" ehm ON ehm."StepWordId" = shw."Id"
                                 WHERE 1=1
                                     AND split_part(shw."RootDisambiguatedStrongsInstance", '_', 1) = ANY(@Keys)
                                     AND NOT ehm."IsAddedWord"
                                 """;

        return await _dbReader.ExecuteScalarAsync<int>(hebrew ? hebrewSql : greekSql, new { Keys = keys.ToArray() });
    }

    public async Task<IReadOnlyList<WordBookCountRow>> GetWordBookCountsAsync(string word, int firstBook, int lastBook, CancellationToken token = default)
    {
        const string sql = """
                           SELECT ew."BibleBookId"
                                , COUNT(*)::int "Count"
                                , COUNT(DISTINCT ew."HebRefId")::int "VerseCount"
                                , (COUNT(*) FILTER (WHERE NOT EXISTS (SELECT 1
                                                                       FROM "unshackled-word"."Elb1871GreekMapping" egm
                                                                       WHERE 1=1
                                                                           AND egm."ElbWordId" = ew."Id"
                                                                           AND NOT egm."IsAddedWord")
                                                      AND NOT EXISTS (SELECT 1
                                                                      FROM "unshackled-word"."Elb1871HebrewMapping" ehm
                                                                      WHERE 1=1
                                                                          AND ehm."ElbWordId" = ew."Id"
                                                                          AND NOT ehm."IsAddedWord")))::int "UnmappedCount"
                                , MODE() WITHIN GROUP (ORDER BY COALESCE(NULLIF(ew."Lemma", ''), ew."PlainWord")) "Word"
                           FROM "unshackled-word"."Elb1871Words" ew
                           WHERE 1=1
                               AND LOWER(COALESCE(NULLIF(ew."Lemma", ''), ew."PlainWord")) = LOWER(@Word)
                               AND ew."BibleBookId" BETWEEN @FirstBook AND @LastBook
                           GROUP BY ew."BibleBookId"
                           ORDER BY ew."BibleBookId"
                           """;

        var rows = await _dbReader.ReadAsListAsync<WordBookCountRow>(sql, new { Word = word, FirstBook = firstBook, LastBook = lastBook });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<WordOriginalRow>> GetWordOriginalsAsync(string word, int firstBook, int lastBook, CancellationToken token = default)
    {
        const string sql = """
                           WITH words AS (
                               SELECT ew."Id"
                               FROM "unshackled-word"."Elb1871Words" ew
                               WHERE 1=1
                                   AND LOWER(COALESCE(NULLIF(ew."Lemma", ''), ew."PlainWord")) = LOWER(@Word)
                                   AND ew."BibleBookId" BETWEEN @FirstBook AND @LastBook
                           ), originals AS (
                               SELECT sgw."DisambiguatedStrongs" "Strongs"
                                    , 2 "LanguageId"
                                    , COUNT(DISTINCT sgw."Id")::int "Count"
                               FROM words
                                   JOIN "unshackled-word"."Elb1871GreekMapping" egm ON egm."ElbWordId" = words."Id"
                                   JOIN "unshackled-word"."StepGreekWords" sgw ON sgw."Id" = egm."StepWordId"
                               WHERE 1=1
                                   AND NOT egm."IsAddedWord"
                               GROUP BY sgw."DisambiguatedStrongs"
                               UNION ALL
                               SELECT split_part(shw."RootDisambiguatedStrongsInstance", '_', 1) "Strongs"
                                    , 0 "LanguageId"
                                    , COUNT(DISTINCT shw."Id")::int "Count"
                               FROM words
                                   JOIN "unshackled-word"."Elb1871HebrewMapping" ehm ON ehm."ElbWordId" = words."Id"
                                   JOIN "unshackled-word"."StepHebrewWords" shw ON shw."Id" = ehm."StepWordId"
                               WHERE 1=1
                                   AND NOT ehm."IsAddedWord"
                                   AND shw."RootDisambiguatedStrongsInstance" IS NOT NULL
                               GROUP BY 1
                           )
                           SELECT o."Strongs"
                                , o."Count"
                                , CASE
                                      WHEN o."LanguageId" = 2 THEN (SELECT COUNT(*)::int
                                                                    FROM "unshackled-word"."StepGreekWords" t
                                                                    WHERE 1=1
                                                                        AND t."DisambiguatedStrongs" = o."Strongs")
                                      ELSE (SELECT COUNT(*)::int
                                            FROM "unshackled-word"."StepHebrewWords" t
                                            WHERE 1=1
                                                AND split_part(t."RootDisambiguatedStrongsInstance", '_', 1) = o."Strongs")
                                  END "TotalCount"
                                , lex."LanguageId" "LexiconLanguageId"
                                , lex."OriginalWord" "Lemma"
                                , lex."Transliteration"
                                , lex."Gloss"
                           FROM originals o
                               LEFT JOIN LATERAL (
                                   SELECT ssl."LanguageId"
                                        , ssl."OriginalWord"
                                        , ssl."Transliteration"
                                        , ssl."Gloss"
                                   FROM "unshackled-word"."StepStrongsLexicon" ssl
                                   WHERE 1=1
                                       AND ssl."LanguageId" = ANY(CASE WHEN o."LanguageId" = 2 THEN ARRAY[2] ELSE ARRAY[0, 1] END)
                                       AND ssl."Number" = substring(o."Strongs" from '^[GH](\d+)')::int
                                       AND COALESCE(ssl."Extra", '') = substring(o."Strongs" from '^[GH]\d+([A-Za-z]*)$')
                                   ORDER BY ssl."LanguageId"
                                   LIMIT 1
                               ) lex ON TRUE
                           ORDER BY o."Count" DESC, o."Strongs"
                           """;

        var rows = await _dbReader.ReadAsListAsync<WordOriginalRow>(sql, new { Word = word, FirstBook = firstBook, LastBook = lastBook });
        return rows.ToList();
    }

    public async Task<int> CountOccurrencesAsync(IReadOnlyList<string> keys, bool hebrew, StrongsOccurrenceFilter filter, CancellationToken token = default)
    {
        const string greekSql = """
                                WITH verse_map AS (
                                    SELECT DISTINCT ON (v."LxxRefId") v."LxxRefId"
                                         , v."HebRefId"
                                    FROM "unshackled-word"."Elb1871Verses" v
                                    ORDER BY v."LxxRefId", v."Id"
                                )
                                SELECT COUNT(*)::int
                                FROM "unshackled-word"."StepGreekWords" sgw
                                    LEFT JOIN verse_map vm ON vm."LxxRefId" = sgw."LxxRefId"
                                WHERE 1=1
                                    AND sgw."DisambiguatedStrongs" = ANY(@Keys)
                                    AND (@Book::int IS NULL OR COALESCE(vm."HebRefId", sgw."LxxRefId") / 1000000 = @Book::int)
                                    AND (@Rendering::text IS NULL OR EXISTS (SELECT 1
                                                                             FROM "unshackled-word"."Elb1871GreekMapping" egm
                                                                                 JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = egm."ElbWordId"
                                                                             WHERE 1=1
                                                                                 AND egm."StepWordId" = sgw."Id"
                                                                                 AND NOT egm."IsAddedWord"
                                                                                 AND LOWER(COALESCE(NULLIF(ew."Lemma", ''), ew."PlainWord")) = LOWER(@Rendering::text)))
                                """;

        const string hebrewSql = """
                                 WITH verse_map AS (
                                     SELECT DISTINCT ON (v."LxxRefId") v."LxxRefId"
                                          , v."HebRefId"
                                     FROM "unshackled-word"."Elb1871Verses" v
                                     ORDER BY v."LxxRefId", v."Id"
                                 )
                                 SELECT COUNT(*)::int
                                 FROM "unshackled-word"."StepHebrewWords" shw
                                     LEFT JOIN verse_map vm ON vm."LxxRefId" = shw."LxxRefId"
                                 WHERE 1=1
                                     AND split_part(shw."RootDisambiguatedStrongsInstance", '_', 1) = ANY(@Keys)
                                     AND (@Book::int IS NULL OR COALESCE(vm."HebRefId", shw."LxxRefId") / 1000000 = @Book::int)
                                     AND (@Rendering::text IS NULL OR EXISTS (SELECT 1
                                                                              FROM "unshackled-word"."Elb1871HebrewMapping" ehm
                                                                                  JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = ehm."ElbWordId"
                                                                              WHERE 1=1
                                                                                  AND ehm."StepWordId" = shw."Id"
                                                                                  AND NOT ehm."IsAddedWord"
                                                                                  AND LOWER(COALESCE(NULLIF(ew."Lemma", ''), ew."PlainWord")) = LOWER(@Rendering::text)))
                                 """;

        return await _dbReader.ExecuteScalarAsync<int>(hebrew ? hebrewSql : greekSql, new { Keys = keys.ToArray(), filter.Book, filter.Rendering });
    }

    public async Task<IReadOnlyList<StrongsOccurrenceRow>> GetOccurrencesAsync(IReadOnlyList<string> keys, bool hebrew, StrongsOccurrenceFilter filter, int skip, int take, CancellationToken token = default)
    {
        const string greekSql = """
                                WITH verse_map AS (
                                    SELECT DISTINCT ON (v."LxxRefId") v."LxxRefId"
                                         , v."HebRefId"
                                    FROM "unshackled-word"."Elb1871Verses" v
                                    ORDER BY v."LxxRefId", v."Id"
                                ), page AS (
                                    SELECT sgw."Id"
                                         , sgw."LxxRefId"
                                         , sgw."PositionInVerse"
                                         , sgw."Greek" "Original"
                                         , COALESCE(vm."HebRefId", sgw."LxxRefId") "ElbRefId"
                                    FROM "unshackled-word"."StepGreekWords" sgw
                                        LEFT JOIN verse_map vm ON vm."LxxRefId" = sgw."LxxRefId"
                                    WHERE 1=1
                                        AND sgw."DisambiguatedStrongs" = ANY(@Keys)
                                        AND (@Book::int IS NULL OR COALESCE(vm."HebRefId", sgw."LxxRefId") / 1000000 = @Book::int)
                                        AND (@Rendering::text IS NULL OR EXISTS (SELECT 1
                                                                                 FROM "unshackled-word"."Elb1871GreekMapping" egm
                                                                                     JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = egm."ElbWordId"
                                                                                 WHERE 1=1
                                                                                     AND egm."StepWordId" = sgw."Id"
                                                                                     AND NOT egm."IsAddedWord"
                                                                                     AND LOWER(COALESCE(NULLIF(ew."Lemma", ''), ew."PlainWord")) = LOWER(@Rendering::text)))
                                    ORDER BY sgw."LxxRefId", sgw."PositionInVerse", sgw."Id"
                                    LIMIT @Take OFFSET @Skip
                                ), elb AS (
                                    SELECT egm."StepWordId"
                                         , array_agg(ew."PositionInVerse" ORDER BY ew."PositionInVerse") "MatchedPositions"
                                    FROM "unshackled-word"."Elb1871GreekMapping" egm
                                        JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = egm."ElbWordId"
                                    WHERE 1=1
                                        AND egm."StepWordId" IN (SELECT "Id" FROM page)
                                        AND NOT egm."IsAddedWord"
                                    GROUP BY egm."StepWordId"
                                )
                                SELECT (page."ElbRefId" / 1000000)::int "BibleBookId"
                                     , (page."ElbRefId" / 1000 % 1000)::int "Chapter"
                                     , (page."ElbRefId" % 1000)::int "Verse"
                                     , page."Original"
                                     , elb."MatchedPositions"
                                     , vw."VersePositions"
                                     , vw."VerseWords"
                                FROM page
                                    LEFT JOIN elb ON elb."StepWordId" = page."Id"
                                    LEFT JOIN LATERAL (
                                        SELECT array_agg(w."PositionInVerse" ORDER BY w."PositionInVerse") "VersePositions"
                                             , array_agg(w."WordInContext" ORDER BY w."PositionInVerse") "VerseWords"
                                        FROM "unshackled-word"."Elb1871Words" w
                                        WHERE 1=1
                                            AND w."HebRefId" = page."ElbRefId"
                                    ) vw ON TRUE
                                ORDER BY page."LxxRefId", page."PositionInVerse", page."Id"
                                """;

        const string hebrewSql = """
                                 WITH verse_map AS (
                                     SELECT DISTINCT ON (v."LxxRefId") v."LxxRefId"
                                          , v."HebRefId"
                                     FROM "unshackled-word"."Elb1871Verses" v
                                     ORDER BY v."LxxRefId", v."Id"
                                 ), page AS (
                                     SELECT shw."Id"
                                          , shw."LxxRefId"
                                          , shw."PositionInVerse"
                                          , shw."Hebrew" "Original"
                                          , COALESCE(vm."HebRefId", shw."LxxRefId") "ElbRefId"
                                     FROM "unshackled-word"."StepHebrewWords" shw
                                         LEFT JOIN verse_map vm ON vm."LxxRefId" = shw."LxxRefId"
                                     WHERE 1=1
                                         AND split_part(shw."RootDisambiguatedStrongsInstance", '_', 1) = ANY(@Keys)
                                         AND (@Book::int IS NULL OR COALESCE(vm."HebRefId", shw."LxxRefId") / 1000000 = @Book::int)
                                         AND (@Rendering::text IS NULL OR EXISTS (SELECT 1
                                                                                  FROM "unshackled-word"."Elb1871HebrewMapping" ehm
                                                                                      JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = ehm."ElbWordId"
                                                                                  WHERE 1=1
                                                                                      AND ehm."StepWordId" = shw."Id"
                                                                                      AND NOT ehm."IsAddedWord"
                                                                                      AND LOWER(COALESCE(NULLIF(ew."Lemma", ''), ew."PlainWord")) = LOWER(@Rendering::text)))
                                     ORDER BY shw."LxxRefId", shw."PositionInVerse", shw."Id"
                                     LIMIT @Take OFFSET @Skip
                                 ), elb AS (
                                     SELECT ehm."StepWordId"
                                          , array_agg(ew."PositionInVerse" ORDER BY ew."PositionInVerse") "MatchedPositions"
                                     FROM "unshackled-word"."Elb1871HebrewMapping" ehm
                                         JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = ehm."ElbWordId"
                                     WHERE 1=1
                                         AND ehm."StepWordId" IN (SELECT "Id" FROM page)
                                         AND NOT ehm."IsAddedWord"
                                     GROUP BY ehm."StepWordId"
                                 )
                                 SELECT (page."ElbRefId" / 1000000)::int "BibleBookId"
                                      , (page."ElbRefId" / 1000 % 1000)::int "Chapter"
                                      , (page."ElbRefId" % 1000)::int "Verse"
                                      , page."Original"
                                      , elb."MatchedPositions"
                                      , vw."VersePositions"
                                      , vw."VerseWords"
                                 FROM page
                                     LEFT JOIN elb ON elb."StepWordId" = page."Id"
                                     LEFT JOIN LATERAL (
                                         SELECT array_agg(w."PositionInVerse" ORDER BY w."PositionInVerse") "VersePositions"
                                              , array_agg(w."WordInContext" ORDER BY w."PositionInVerse") "VerseWords"
                                         FROM "unshackled-word"."Elb1871Words" w
                                         WHERE 1=1
                                             AND w."HebRefId" = page."ElbRefId"
                                     ) vw ON TRUE
                                 ORDER BY page."LxxRefId", page."PositionInVerse", page."Id"
                                 """;

        var rows = await _dbReader.ReadAsListAsync<StrongsOccurrenceRow>(hebrew ? hebrewSql : greekSql, new { Keys = keys.ToArray(), filter.Book, filter.Rendering, Skip = skip, Take = take });
        return rows.ToList();
    }
}
