using UnshackledWord.Application.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public class StepFullTextSearchRunner : IRunner
{
    private readonly IDbReader _reader;
    private readonly IDbWriter _writer;
    private readonly ILogger<StepFullTextSearchRunner> _logger;

    public StepFullTextSearchRunner(IDbReader reader, IDbWriter writer, ILogger<StepFullTextSearchRunner> logger)
    {
        _reader = reader;
        _writer = writer;
        _logger = logger;
    }

    public async Task Run(CancellationToken token = default)
    {
        var countSql = """
                       SELECT COUNT(*)
                       FROM "unshackled-word"."StepBibleVerses"
                       """;

        var count = await _reader.ExecuteScalarAsync<int>(countSql);

        if (count > 0)
        {
            _logger.LogInformation("Full text for Step Bible data already imported.");
            return;
        }

        var sql = """
                  INSERT INTO "unshackled-word"."StepBibleVerses" ("HebRefId", "LxxRefId", "VerseText", "LemmaText")
                  SELECT fts."HebRefId", fts."LxxRefId", fts."VerseText", fts."LemmaText"
                  FROM (
                      -- title: final Greek FTS data
                      SELECT
                          bvcm."HebRefId"
                          , bvcm."LxxRefId"
                          , LOWER(STRING_AGG(sgw."GreekNoDiacritics", ' ' ORDER BY sgw."PositionInVerse")) AS "VerseText"
                          , LOWER(STRING_AGG(sgw."LemmaNoDiacritics", ' ' ORDER BY sgw."PositionInVerse")) AS "LemmaText"
                      FROM "unshackled-word"."StepGreekWords" sgw
                          INNER JOIN "unshackled-word"."BibleVerseCountingMapping" bvcm ON bvcm."LxxRefId" = sgw."LxxRefId"
                      GROUP BY bvcm."HebRefId", bvcm."LxxRefId"
                      UNION
                      -- title: final Hebrew FTS data
                      SELECT
                          bvcm."HebRefId"
                          , bvcm."LxxRefId"
                          , LOWER(STRING_AGG(shw."HebrewNoDiacritics", ' ' ORDER BY shw."PositionInVerse")) AS "VerseText"
                          , LOWER(STRING_AGG(lemmas."OriginalWordNoDiacritics", ' ' ORDER BY shw."PositionInVerse")) AS "LemmaText"
                      FROM "unshackled-word"."StepHebrewWords" shw
                          INNER JOIN "unshackled-word"."BibleVerseCountingMapping" bvcm ON bvcm."LxxRefId" = shw."LxxRefId"
                          INNER JOIN (
                              SELECT DISTINCT shw."LxxRefId", shw."PositionInVerse", shw."HebrewNoDiacritics", shw."RootDisambiguatedStrongsInstance", sstt."Number", ssl."OriginalWordNoDiacritics"
                              FROM "unshackled-word"."StepHebrewWords" shw
                                  INNER JOIN "unshackled-word"."StepStrongsToText" sstt ON shw."Id" = sstt."StepHebrewWordId" AND sstt."IsRoot" = true
                                  INNER JOIN "unshackled-word"."StepStrongsLexicon" ssl ON sstt."Number" = ssl."Number" AND ssl."LanguageId" IN (0,1)
                              ORDER BY shw."LxxRefId", shw."PositionInVerse"
                          ) lemmas ON shw."LxxRefId" = lemmas."LxxRefId" AND shw."PositionInVerse" = lemmas."PositionInVerse"
                      GROUP BY bvcm."HebRefId", bvcm."LxxRefId"
                  ) fts
                  ORDER BY fts."HebRefId"
                  """;

        await _writer.ExecuteScalarAsync<int>(sql);
    }
}
