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

    public async Task CopyOverGreekMapping(CancellationToken token = default)
    {
        var sql = $"""
                   INSERT INTO "unshackled-word"."Elb1871GreekMapping"
                   ("ElbWordId","StepGreekId","HebRefId","StrongsNumber","IsAddedWord","ParentGermanWordId","PositionInVerse","GermanWordPart")
                   SELECT
                         ew."Id"                     "ElbWordId"
                       , sgw."Id"                    "StepGreekId"
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
                       LEFT  JOIN "unshackled-word"."StepGreekWords" sgw ON egm."StepGreekId" = sgw."Id"
                   ORDER BY egm."LxxRefId", egm."PositionInVerse"
                   ON CONFLICT DO NOTHING;
                   """;

        await _dbWriter.ExecuteScalarAsync<int>(sql);
    }
}
