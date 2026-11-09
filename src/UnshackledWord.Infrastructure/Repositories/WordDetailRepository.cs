using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Infrastructure.Repositories;

public sealed class WordDetailRepository : IWordDetailRepository
{
    private readonly IDbReader _dbReader;

    public WordDetailRepository(IDbReader dbReader)
    {
        _dbReader = dbReader;
    }

    public async Task<ElbWordRow?> GetWordAsync(int wordId, CancellationToken token = default)
    {
        const string sql = """
                           SELECT ew."Id"
                                , ew."BibleBookId"
                                , ew."Chapter"
                                , ew."Verse"
                                , ew."HebRefId"
                                , ew."PositionInVerse"
                                , ew."WordInContext"
                                , ew."Lemma" "GermanLemma"
                           FROM "unshackled-word"."Elb1871Words" ew
                           WHERE 1=1
                               AND ew."Id" = @WordId
                           """;

        return await _dbReader.ReadFirstOrDefaultAsync<ElbWordRow>(sql, new { WordId = wordId });
    }

    public async Task<IReadOnlyList<GreekReadingRow>> GetGreekReadingsAsync(int wordId, CancellationToken token = default)
    {
        const string sql = """
                           SELECT egm."IsAddedWord"
                                , egm."GermanWordPart"
                                , egm."CreatedAt" "MappingCreatedAt"
                                , sgw."Id" "StepWordId"
                                , sgw."PositionInVerse"
                                , sgw."Greek" "Original"
                                , sgw."Transliteration"
                                , sgw."Lemma"
                                , sgw."DisambiguatedStrongs" "Strongs"
                                , sgw."Gloss"
                                , sgw."English" "ContextGloss"
                                , sgw."Morphology" "MorphologyCode"
                                , sgm."PartOfSpeech" "StepPartOfSpeech"
                                , sgm."Tense" "StepTense"
                                , sgm."Voice" "StepVoice"
                                , sgm."Mood" "StepMood"
                                , sgm."Person" "StepPerson"
                                , sgm."Case" "StepCase"
                                , sgm."Number" "StepNumber"
                                , sgm."Gender" "StepGender"
                           FROM "unshackled-word"."Elb1871GreekMapping" egm
                               LEFT JOIN "unshackled-word"."StepGreekWords" sgw ON sgw."Id" = egm."StepWordId"
                               LEFT JOIN "unshackled-word"."StepGreekMorphology" sgm ON sgm."Code" = sgw."Morphology"
                           WHERE 1=1
                               AND egm."ElbWordId" = @WordId
                           ORDER BY sgw."PositionInVerse" NULLS LAST
                           """;

        var rows = await _dbReader.ReadAsListAsync<GreekReadingRow>(sql, new { WordId = wordId });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<HebrewReadingRow>> GetHebrewReadingsAsync(int wordId, CancellationToken token = default)
    {
        const string sql = """
                           SELECT ehm."IsAddedWord"
                                , ehm."GermanWordPart"
                                , ehm."CreatedAt" "MappingCreatedAt"
                                , shw."Id" "StepWordId"
                                , shw."PositionInVerse"
                                , shw."Hebrew" "Original"
                                , shw."Transliteration"
                                , shw."RootDisambiguatedStrongsInstance" "Strongs"
                                , shw."DisambiguatedStrongs"
                                , shw."Gloss" "ContextGloss"
                                , shw."Grammar" "MorphologyCode"
                           FROM "unshackled-word"."Elb1871HebrewMapping" ehm
                               LEFT JOIN "unshackled-word"."StepHebrewWords" shw ON shw."Id" = ehm."StepWordId"
                           WHERE 1=1
                               AND ehm."ElbWordId" = @WordId
                           ORDER BY shw."PositionInVerse" NULLS LAST
                           """;

        var rows = await _dbReader.ReadAsListAsync<HebrewReadingRow>(sql, new { WordId = wordId });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<CoMappedWordRow>> GetCoMappedWordsAsync(int wordId, bool hebrew, CancellationToken token = default)
    {
        const string greekSql = """
                                WITH own AS (
                                    SELECT egm."StepWordId"
                                         , egm."IsAddedWord"
                                         , egm."ParentGermanWordId"
                                    FROM "unshackled-word"."Elb1871GreekMapping" egm
                                    WHERE 1=1
                                        AND egm."ElbWordId" = @WordId
                                )
                                SELECT ew."Id" "WordId"
                                     , ew."WordInContext"
                                     , ew."PositionInVerse"
                                     , 'sameOriginal' "Relation"
                                     , egm."StepWordId"
                                FROM own
                                    JOIN "unshackled-word"."Elb1871GreekMapping" egm ON egm."StepWordId" = own."StepWordId"
                                    JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = egm."ElbWordId"
                                    JOIN "unshackled-word"."Elb1871Words" own_word ON own_word."Id" = @WordId
                                WHERE 1=1
                                    AND egm."ElbWordId" <> @WordId
                                    AND NOT own."IsAddedWord"
                                    AND NOT egm."IsAddedWord"
                                    AND ew."BibleBookId" = own_word."BibleBookId"
                                    AND ew."Chapter" = own_word."Chapter"
                                    AND ew."Verse" = own_word."Verse"
                                UNION ALL
                                SELECT ew."Id"
                                     , ew."WordInContext"
                                     , ew."PositionInVerse"
                                     , 'addedWord'
                                     , NULL::int
                                FROM "unshackled-word"."Elb1871GreekMapping" egm
                                    JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = egm."ElbWordId"
                                    JOIN "unshackled-word"."Elb1871Words" own_word ON own_word."Id" = @WordId
                                WHERE 1=1
                                    AND egm."ParentGermanWordId" = @WordId
                                    AND egm."IsAddedWord"
                                    AND ew."Verse" = own_word."Verse"
                                    AND ew."Chapter" = own_word."Chapter"
                                    AND ew."BibleBookId" = own_word."BibleBookId"
                                UNION ALL
                                SELECT ew."Id"
                                     , ew."WordInContext"
                                     , ew."PositionInVerse"
                                     , 'parent'
                                     , NULL::int
                                FROM own
                                    JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = own."ParentGermanWordId"
                                    JOIN "unshackled-word"."Elb1871Words" own_word ON own_word."Id" = @WordId
                                WHERE 1=1
                                    AND ew."Verse" = own_word."Verse"
                                    AND ew."Chapter" = own_word."Chapter"
                                    AND ew."BibleBookId" = own_word."BibleBookId"
                                """;

        const string hebrewSql = """
                                 WITH own AS (
                                     SELECT ehm."StepWordId"
                                          , ehm."IsAddedWord"
                                          , ehm."ParentGermanWordId"
                                     FROM "unshackled-word"."Elb1871HebrewMapping" ehm
                                     WHERE 1=1
                                         AND ehm."ElbWordId" = @WordId
                                 )
                                 SELECT ew."Id" "WordId"
                                      , ew."WordInContext"
                                      , ew."PositionInVerse"
                                      , 'sameOriginal' "Relation"
                                      , ehm."StepWordId"
                                 FROM own
                                     JOIN "unshackled-word"."Elb1871HebrewMapping" ehm ON ehm."StepWordId" = own."StepWordId"
                                     JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = ehm."ElbWordId"
                                     JOIN "unshackled-word"."Elb1871Words" own_word ON own_word."Id" = @WordId
                                 WHERE 1=1
                                     AND ehm."ElbWordId" <> @WordId
                                     AND NOT own."IsAddedWord"
                                     AND NOT ehm."IsAddedWord"
                                     AND ew."BibleBookId" = own_word."BibleBookId"
                                     AND ew."Chapter" = own_word."Chapter"
                                     AND ew."Verse" = own_word."Verse"
                                 UNION ALL
                                 SELECT ew."Id"
                                      , ew."WordInContext"
                                      , ew."PositionInVerse"
                                      , 'addedWord'
                                      , NULL::int
                                 FROM "unshackled-word"."Elb1871HebrewMapping" ehm
                                     JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = ehm."ElbWordId"
                                     JOIN "unshackled-word"."Elb1871Words" own_word ON own_word."Id" = @WordId
                                 WHERE 1=1
                                     AND ehm."ParentGermanWordId" = @WordId
                                     AND ehm."IsAddedWord"
                                     AND ew."Verse" = own_word."Verse"
                                     AND ew."Chapter" = own_word."Chapter"
                                     AND ew."BibleBookId" = own_word."BibleBookId"
                                 UNION ALL
                                 SELECT ew."Id"
                                      , ew."WordInContext"
                                      , ew."PositionInVerse"
                                      , 'parent'
                                      , NULL::int
                                 FROM own
                                     JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = own."ParentGermanWordId"
                                     JOIN "unshackled-word"."Elb1871Words" own_word ON own_word."Id" = @WordId
                                 WHERE 1=1
                                     AND ew."Verse" = own_word."Verse"
                                     AND ew."Chapter" = own_word."Chapter"
                                     AND ew."BibleBookId" = own_word."BibleBookId"
                                 """;

        var rows = await _dbReader.ReadAsListAsync<CoMappedWordRow>(hebrew ? hebrewSql : greekSql, new { WordId = wordId });
        return rows.ToList();
    }
}
