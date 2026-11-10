using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Infrastructure.Repositories;

public sealed class VerseMappingRepository : IVerseMappingRepository
{
    private readonly IDbReader _dbReader;
    private readonly IDbConnectionFactory _factory;

    public VerseMappingRepository(IDbReader dbReader, IDbConnectionFactory factory)
    {
        _dbReader = dbReader;
        _factory = factory;
    }

    public async Task<IReadOnlyList<VerseGermanWordRow>> GetGermanWordsAsync(int bibleBookId, int chapter, int verse, CancellationToken token = default)
    {
        var rows = await _dbReader.ReadAsListAsync<VerseGermanWordRow>(VerseMappingSql.GermanWords, VerseParams(bibleBookId, chapter, verse));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<VerseOriginalWordRow>> GetOriginalWordsAsync(bool hebrew, int bibleBookId, int chapter, int verse, CancellationToken token = default)
    {
        var sql = hebrew ? VerseMappingSql.HebrewWords : VerseMappingSql.GreekWords;
        var rows = await _dbReader.ReadAsListAsync<VerseOriginalWordRow>(sql, VerseParams(bibleBookId, chapter, verse));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<VerseMappingRow>> GetMappingRowsAsync(bool hebrew, int bibleBookId, int chapter, int verse, CancellationToken token = default)
    {
        var sql = hebrew ? VerseMappingSql.HebrewMappingRows : VerseMappingSql.GreekMappingRows;
        var rows = await _dbReader.ReadAsListAsync<VerseMappingRow>(sql, VerseParams(bibleBookId, chapter, verse));
        return rows.ToList();
    }

    public async Task ReplaceMappingAsync(bool hebrew, int bibleBookId, int chapter, int verse, IReadOnlyList<VerseMappingRow> rows, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(
            hebrew ? VerseMappingSql.DeleteHebrew : VerseMappingSql.DeleteGreek,
            VerseParams(bibleBookId, chapter, verse),
            transaction);

        if (rows.Count > 0)
        {
            await connection.ExecuteAsync(
                hebrew ? VerseMappingSql.InsertHebrew : VerseMappingSql.InsertGreek,
                rows,
                transaction);
        }

        transaction.Commit();
    }

    private static object VerseParams(int bibleBookId, int chapter, int verse)
    {
        return new { BibleBookId = bibleBookId, Chapter = chapter, Verse = verse };
    }
}

/// <summary>SQL of <see cref="VerseMappingRepository"/>, public so tests can assert its shape.</summary>
public static class VerseMappingSql
{
    public const string GermanWords = """
                                      SELECT ew."Id"
                                           , ew."PositionInVerse"
                                           , ew."WordInContext" "Text"
                                      FROM "unshackled-word"."Elb1871Words" ew
                                      WHERE 1=1
                                          AND ew."BibleBookId" = @BibleBookId
                                          AND ew."Chapter" = @Chapter
                                          AND ew."Verse" = @Verse
                                      ORDER BY ew."PositionInVerse"
                                             , ew."Id"
                                      """;

    public const string GreekWords = """
                                     SELECT sgw."Id"
                                          , sgw."PositionInVerse" "Position"
                                          , sgw."Greek" "Original"
                                          , sgw."Transliteration"
                                          , sgw."Lemma"
                                          , sgw."DisambiguatedStrongs" "Strongs"
                                          , sgw."Gloss"
                                          , sgw."Morphology" "MorphologyCode"
                                     FROM "unshackled-word"."StepGreekWords" sgw
                                     WHERE 1=1
                                         AND sgw."LxxRefId" IN (SELECT DISTINCT ew."HebRefId"
                                                                FROM "unshackled-word"."Elb1871Words" ew
                                                                WHERE 1=1
                                                                    AND ew."BibleBookId" = @BibleBookId
                                                                    AND ew."Chapter" = @Chapter
                                                                    AND ew."Verse" = @Verse)
                                     ORDER BY sgw."PositionInVerse"
                                            , sgw."Id"
                                     """;

    public const string HebrewWords = """
                                      SELECT shw."Id"
                                           , shw."PositionInVerse" "Position"
                                           , shw."Hebrew" "Original"
                                           , shw."Transliteration"
                                           , lex."OriginalWord" "Lemma"
                                           , shw."RootDisambiguatedStrongsInstance" "Strongs"
                                           , shw."Gloss"
                                           , shw."Grammar" "MorphologyCode"
                                      FROM "unshackled-word"."StepHebrewWords" shw
                                          LEFT JOIN LATERAL (SELECT ssl."OriginalWord"
                                                             FROM "unshackled-word"."StepStrongsLexicon" ssl
                                                             WHERE 1=1
                                                                 AND ssl."LanguageId" IN (0, 1)
                                                                 AND ssl."Number" = (regexp_match(shw."RootDisambiguatedStrongsInstance", '^H0*(\d+)'))[1]::int
                                                             ORDER BY (ssl."Extra" IS NOT DISTINCT FROM (regexp_match(shw."RootDisambiguatedStrongsInstance", '^H\d+([A-Za-z])'))[1]) DESC
                                                                    , ssl."Extra"
                                                             LIMIT 1) lex ON TRUE
                                      WHERE 1=1
                                          AND shw."LxxRefId" IN (SELECT DISTINCT ew."HebRefId"
                                                                 FROM "unshackled-word"."Elb1871Words" ew
                                                                 WHERE 1=1
                                                                     AND ew."BibleBookId" = @BibleBookId
                                                                     AND ew."Chapter" = @Chapter
                                                                     AND ew."Verse" = @Verse)
                                      ORDER BY shw."PositionInVerse"
                                             , shw."Id"
                                      """;

    public const string GreekMappingRows = """
                                           SELECT egm."ElbWordId"
                                                , egm."StepWordId"
                                                , COALESCE(egm."IsAddedWord", FALSE) "IsAddedWord"
                                                , egm."ParentGermanWordId" "ParentElbWordId"
                                           FROM "unshackled-word"."Elb1871GreekMapping" egm
                                               JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = egm."ElbWordId"
                                           WHERE 1=1
                                               AND ew."BibleBookId" = @BibleBookId
                                               AND ew."Chapter" = @Chapter
                                               AND ew."Verse" = @Verse
                                           ORDER BY ew."PositionInVerse"
                                                  , egm."Id"
                                           """;

    public const string HebrewMappingRows = """
                                            SELECT ehm."ElbWordId"
                                                 , ehm."StepWordId"
                                                 , COALESCE(ehm."IsAddedWord", FALSE) "IsAddedWord"
                                                 , ehm."ParentGermanWordId" "ParentElbWordId"
                                            FROM "unshackled-word"."Elb1871HebrewMapping" ehm
                                                JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = ehm."ElbWordId"
                                            WHERE 1=1
                                                AND ew."BibleBookId" = @BibleBookId
                                                AND ew."Chapter" = @Chapter
                                                AND ew."Verse" = @Verse
                                            ORDER BY ew."PositionInVerse"
                                                   , ehm."Id"
                                            """;

    public const string DeleteGreek = """
                                      DELETE
                                      FROM "unshackled-word"."Elb1871GreekMapping" egm
                                      WHERE 1=1
                                          AND egm."ElbWordId" IN (SELECT ew."Id"
                                                                  FROM "unshackled-word"."Elb1871Words" ew
                                                                  WHERE 1=1
                                                                      AND ew."BibleBookId" = @BibleBookId
                                                                      AND ew."Chapter" = @Chapter
                                                                      AND ew."Verse" = @Verse)
                                      """;

    public const string DeleteHebrew = """
                                       DELETE
                                       FROM "unshackled-word"."Elb1871HebrewMapping" ehm
                                       WHERE 1=1
                                           AND ehm."ElbWordId" IN (SELECT ew."Id"
                                                                   FROM "unshackled-word"."Elb1871Words" ew
                                                                   WHERE 1=1
                                                                       AND ew."BibleBookId" = @BibleBookId
                                                                       AND ew."Chapter" = @Chapter
                                                                       AND ew."Verse" = @Verse)
                                       """;

    // Mirrors the seed import: GermanWordPart stays NULL for whole words, HebRefId and PositionInVerse come from the German word,
    // StrongsNumber (Greek only) is the disambiguated Strong's number of the linked STEP word.
    public const string InsertGreek = """
                                      INSERT INTO "unshackled-word"."Elb1871GreekMapping"
                                          ("ElbWordId", "StepWordId", "HebRefId", "StrongsNumber", "IsAddedWord", "ParentGermanWordId", "PositionInVerse", "GermanWordPart", "CreatedAt")
                                      SELECT ew."Id"
                                           , sgw."Id"
                                           , ew."HebRefId"
                                           , sgw."DisambiguatedStrongs"
                                           , @IsAddedWord
                                           , @ParentElbWordId
                                           , ew."PositionInVerse"
                                           , NULL
                                           , now()
                                      FROM "unshackled-word"."Elb1871Words" ew
                                          LEFT JOIN "unshackled-word"."StepGreekWords" sgw ON sgw."Id" = @StepWordId
                                      WHERE 1=1
                                          AND ew."Id" = @ElbWordId
                                      """;

    public const string InsertHebrew = """
                                       INSERT INTO "unshackled-word"."Elb1871HebrewMapping"
                                           ("ElbWordId", "StepWordId", "HebRefId", "IsAddedWord", "ParentGermanWordId", "PositionInVerse", "GermanWordPart", "CreatedAt")
                                       SELECT ew."Id"
                                            , shw."Id"
                                            , ew."HebRefId"
                                            , @IsAddedWord
                                            , @ParentElbWordId
                                            , ew."PositionInVerse"
                                            , NULL
                                            , now()
                                       FROM "unshackled-word"."Elb1871Words" ew
                                           LEFT JOIN "unshackled-word"."StepHebrewWords" shw ON shw."Id" = @StepWordId
                                       WHERE 1=1
                                           AND ew."Id" = @ElbWordId
                                       """;
}
