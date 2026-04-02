using UnshackledWord.Application.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.LocalOnly.Repositories;

public sealed class CopyBackMappingRepository
{
    private readonly IDbReader _dbReader;
    private readonly IDbWriter _dbWriter;

    public CopyBackMappingRepository(IDbReader dbReader, IDbWriter dbWriter)
    {
        _dbReader = dbReader;
        _dbWriter = dbWriter;
    }

    public async Task CopyOverGreekMappingAsync(CancellationToken token = default)
    {
        var sql = $"""
                   INSERT INTO "unshackled-word"."Elb1871GreekMapping"
                   ("ElbWordId","StepWordId","HebRefId","StrongsNumber","IsAddedWord","ParentGermanWordId","PositionInVerse","GermanWordPart")
                   SELECT
                         ew."Id"                     "ElbWordId"
                       , sgw."Id"                    "StepWordId"
                       , bvcm."HebRefId"
                       , sgw."DisambiguatedStrongs"  "StrongsNumber"
                       , egm."IsAddedWord"
                       , ew3."Id"                    "ParentGermanWordId"
                       , egm."PositionInVerse"
                       , egm."GermanWordPart"
                   FROM "unshackled-word-backup01"."Elb1871GreekMapping" egm
                       INNER JOIN "unshackled-word"."BibleVerseCountingMapping" bvcm ON egm."LxxRefId" = bvcm."LxxRefId"
                       INNER JOIN "unshackled-word"."Elb1871Words" ew ON egm."LxxRefId" = ew."HebRefId"  AND egm."PositionInVerse" = ew."PositionInVerse"
                       LEFT  JOIN "unshackled-word-backup01"."Elb1871Words" ew2 ON egm."ParentGermanWordId" = ew2."Id"
                       LEFT  JOIN "unshackled-word"."Elb1871Words" ew3 ON ew2."LxxRefId" = ew3."HebRefId" AND ew2."PositionInVerse" = ew3."PositionInVerse"
                       LEFT  JOIN "unshackled-word"."StepGreekWords" sgw ON egm."StepWordId" = sgw."Id"
                   ORDER BY egm."LxxRefId", egm."PositionInVerse"
                   ON CONFLICT DO NOTHING;

                   BEGIN;
                   DELETE FROM "unshackled-word"."Elb1871GreekMapping"
                   WHERE "HebRefId" IN (
                       SELECT DISTINCT ew."HebRefId"
                       FROM "unshackled-word"."Elb1871Words" ew
                           LEFT JOIN "unshackled-word"."Elb1871GreekMapping" egm ON ew."Id" = egm."ElbWordId"
                       WHERE egm."ElbWordId" IS NULL
                           AND ew."HebRefId" > 40000000
                   );
                   COMMIT;
                   """;

        await _dbWriter.ExecuteScalarAsync<int>(sql);
    }

    public async Task CopyOverHebrewMappingAsync(CancellationToken token = default)
    {
        var sql = $"""
                   INSERT INTO "unshackled-word"."Elb1871HebrewMapping"
                   ("ElbWordId", "StepWordId", "HebRefId", "IsAddedWord", "ParentGermanWordId", "PositionInVerse", "GermanWordPart")
                   SELECT
                         ew."Id"                      "NewElbId"
                       , shw."Id"                     "StepWordId"
                       , bvcm."HebRefId"
                       , ehm."IsAddedWord"
                       , ew3."Id"                     "ParentGermanWordId"
                       , ehm."PositionInVerse"
                       , ehm."GermanWordPart"
                   FROM "unshackled-word-backup01"."Elb1871HebrewMapping" ehm
                       INNER JOIN "unshackled-word"."BibleVerseCountingMapping" bvcm ON ehm."LxxRefId" = bvcm."LxxRefId"
                       INNER JOIN "unshackled-word"."Elb1871Words" ew ON ehm."LxxRefId" = ew."HebRefId"  AND ehm."PositionInVerse" = ew."PositionInVerse"
                       LEFT  JOIN "unshackled-word-backup01"."Elb1871Words" ew2 ON ehm."ParentGermanWordId" = ew2."Id"
                       LEFT  JOIN "unshackled-word"."Elb1871Words" ew3 ON ew2."LxxRefId" = ew3."HebRefId" AND ew2."PositionInVerse" = ew3."PositionInVerse"
                       LEFT  JOIN "unshackled-word"."StepHebrewWords" shw ON ehm."StepWordId" = shw."Id"
                   ORDER BY ehm."LxxRefId", ehm."PositionInVerse"
                   ON CONFLICT DO NOTHING;

                   BEGIN;
                   DELETE FROM "unshackled-word"."Elb1871HebrewMapping"
                   WHERE "HebRefId" IN (
                       SELECT DISTINCT ew."HebRefId"
                       FROM "unshackled-word"."Elb1871Words" ew
                           LEFT JOIN "unshackled-word"."Elb1871HebrewMapping" ehm ON ew."Id" = ehm."ElbWordId"
                       WHERE ehm."ElbWordId" IS NULL
                           AND ew."HebRefId" < 10019000
                   );
                   COMMIT;
                   """;

        await _dbWriter.ExecuteScalarAsync<int>(sql);
    }
}
