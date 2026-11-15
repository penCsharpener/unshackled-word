using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Revision;
using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Infrastructure.Repositories;

public sealed class RevisionWordDetailRepository : IRevisionWordDetailRepository
{
    private readonly IDbReader _dbReader;

    public RevisionWordDetailRepository(IDbReader dbReader)
    {
        _dbReader = dbReader;
    }

    public Task<RevisionWordHeaderRow?> GetWordAsync(int revisionWordId, CancellationToken token = default) =>
        _dbReader.ReadFirstOrDefaultAsync<RevisionWordHeaderRow>(RevisionWordDetailSql.Word, new { WordId = revisionWordId });

    public async Task<IReadOnlyList<GreekReadingRow>> GetGreekReadingsAsync(int revisionWordId, CancellationToken token = default) =>
        (await _dbReader.ReadAsListAsync<GreekReadingRow>(RevisionWordDetailSql.GreekReadings, new { WordId = revisionWordId })).ToList();

    public async Task<IReadOnlyList<HebrewReadingRow>> GetHebrewReadingsAsync(int revisionWordId, CancellationToken token = default) =>
        (await _dbReader.ReadAsListAsync<HebrewReadingRow>(RevisionWordDetailSql.HebrewReadings, new { WordId = revisionWordId })).ToList();

    public async Task<IReadOnlyList<CoMappedWordRow>> GetCoMappedWordsAsync(int revisionWordId, CancellationToken token = default) =>
        (await _dbReader.ReadAsListAsync<CoMappedWordRow>(RevisionWordDetailSql.CoMapped, new { WordId = revisionWordId })).ToList();
}

/// <summary>SQL of <see cref="RevisionWordDetailRepository"/>, public so tests can assert its shape. Every join to STEP carries <c>Lang</c>.</summary>
public static class RevisionWordDetailSql
{
    public const string Word = """
                               SELECT rw."Id"
                                    , rv."HebRefId" / 1000000      "BibleBookId"
                                    , rv."HebRefId" / 1000 % 1000  "Chapter"
                                    , rv."HebRefId" % 1000         "Verse"
                                    , rv."HebRefId"
                                    , rw."PositionInVerse"
                                    , rw."WordInContext"
                                    , rev."Level"
                                    , rw."Change"
                                    , CASE WHEN rw."Change" = 'same' THEN m."Lemma" END           "GermanLemma"
                                    , CASE WHEN rw."Change" = 'changed' THEN bw."WordInContext" END "BaseWord"
                               FROM "unshackled-word"."RevisionWords" rw
                                   JOIN "unshackled-word"."RevisionVerses" rv ON rv."Id" = rw."RevisionVerseId"
                                   JOIN "unshackled-word"."Revisions" rev ON rev."Id" = rv."RevisionId"
                                   LEFT JOIN "unshackled-word"."Elb1871Words" bw ON bw."HebRefId" = rv."HebRefId" AND bw."PositionInVerse" = rw."BasePositionInVerse"
                                   LEFT JOIN "unshackled-word"."Elb1871Morphology" m ON m."HebRefId" = rv."HebRefId" AND m."PositionInVerse" = rw."BasePositionInVerse"
                               WHERE rw."Id" = @WordId
                               """;

    public const string GreekReadings = """
                                        SELECT rm."IsAddedWord"
                                             , rm."GermanWordPart"
                                             , rm."CreatedAt" "MappingCreatedAt"
                                             , rm."Origin"    "MappingOrigin"
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
                                        FROM "unshackled-word"."RevisionMapping" rm
                                            LEFT JOIN "unshackled-word"."StepGreekWords" sgw ON sgw."Id" = rm."StepWordId"
                                            LEFT JOIN "unshackled-word"."StepGreekMorphology" sgm ON sgm."Code" = sgw."Morphology"
                                        WHERE rm."RevisionWordId" = @WordId
                                            AND rm."Lang" = 'G'
                                        ORDER BY sgw."PositionInVerse" NULLS LAST
                                        """;

    public const string HebrewReadings = """
                                         SELECT rm."IsAddedWord"
                                              , rm."GermanWordPart"
                                              , rm."CreatedAt" "MappingCreatedAt"
                                              , rm."Origin"    "MappingOrigin"
                                              , shw."Id" "StepWordId"
                                              , shw."PositionInVerse"
                                              , shw."Hebrew" "Original"
                                              , shw."Transliteration"
                                              , shw."RootDisambiguatedStrongsInstance" "Strongs"
                                              , shw."DisambiguatedStrongs"
                                              , shw."Gloss" "ContextGloss"
                                              , shw."Grammar" "MorphologyCode"
                                         FROM "unshackled-word"."RevisionMapping" rm
                                             LEFT JOIN "unshackled-word"."StepHebrewWords" shw ON shw."Id" = rm."StepWordId"
                                         WHERE rm."RevisionWordId" = @WordId
                                             AND rm."Lang" = 'H'
                                         ORDER BY shw."PositionInVerse" NULLS LAST
                                         """;

    public const string CoMapped = """
                                   WITH own AS (
                                       SELECT rm."Lang"
                                            , rm."StepWordId"
                                            , rm."IsAddedWord"
                                            , rm."ParentRevisionWordId"
                                            , rw."RevisionVerseId"
                                       FROM "unshackled-word"."RevisionMapping" rm
                                           JOIN "unshackled-word"."RevisionWords" rw ON rw."Id" = rm."RevisionWordId"
                                       WHERE rm."RevisionWordId" = @WordId
                                   )
                                   SELECT ow."Id" "WordId"
                                        , ow."WordInContext"
                                        , ow."PositionInVerse"
                                        , 'sameOriginal' "Relation"
                                        , orm."StepWordId"
                                   FROM own
                                       JOIN "unshackled-word"."RevisionWords" ow ON ow."RevisionVerseId" = own."RevisionVerseId"
                                       JOIN "unshackled-word"."RevisionMapping" orm ON orm."RevisionWordId" = ow."Id" AND orm."Lang" = own."Lang" AND orm."StepWordId" = own."StepWordId"
                                   WHERE ow."Id" <> @WordId
                                       AND NOT own."IsAddedWord"
                                       AND NOT orm."IsAddedWord"
                                   UNION ALL
                                   SELECT ow."Id"
                                        , ow."WordInContext"
                                        , ow."PositionInVerse"
                                        , 'addedWord'
                                        , NULL::int
                                   FROM "unshackled-word"."RevisionMapping" orm
                                       JOIN "unshackled-word"."RevisionWords" ow ON ow."Id" = orm."RevisionWordId"
                                   WHERE orm."ParentRevisionWordId" = @WordId
                                       AND orm."IsAddedWord"
                                   UNION ALL
                                   SELECT pw."Id"
                                        , pw."WordInContext"
                                        , pw."PositionInVerse"
                                        , 'parent'
                                        , NULL::int
                                   FROM own
                                       JOIN "unshackled-word"."RevisionWords" pw ON pw."Id" = own."ParentRevisionWordId"
                                   """;
}
