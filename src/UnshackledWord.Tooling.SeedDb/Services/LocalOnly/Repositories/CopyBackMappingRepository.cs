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
                   ("ElbWordId","StepGreekId","BookId","Chapter","Verse","HebRefId","StrongsNumber","IsAddedWord","ParentGermanWordId","WordOrderInVerse","GermanWordPart")
                   SELECT
                       translatedData."ElbWordId"
                       , egm."StepGreekId"
                       , translatedData."BibleBookId"
                       , translatedData."Chapter"
                       , translatedData."Verse"
                       , translatedData."HebRefId"
                       , egm."StrongsNumber"
                       , egm."IsAddedWord"
                       , parentWordData."ElbWordId"
                       , translatedData."PosIVNew"
                       , egm."GermanWordPart"
                   FROM "unshackled-word-backup01"."Elb1871GreekMapping" egm
                       INNER JOIN (
                           SELECT
                               ew."Id"
                               , ew."HebRefId"
                               , ew."BibleBookId"
                               , ew."Chapter"
                               , ew."Verse"
                               , ew."PositionInVerse" "PosIVNew"
                               , ew2."LxxRefId"
                               , ew2."Id" "ElbWordId"
                               , ew."PlainWord" "NewPlainWord"
                               , ew2."PlainWord" "OldPlainWord"
                               , ew2."PositionInVerse" "PosIVOld"
                           FROM "unshackled-word"."Elb1871Words" ew
                               INNER JOIN "unshackled-word"."BibleVerseCountingMapping" bvcm ON ew."HebRefId" = bvcm."HebRefId"
                               INNER JOIN "unshackled-word-backup01"."Elb1871Words" ew2 ON bvcm."LxxRefId" = ew2."LxxRefId" AND ew2."PositionInVerse" = ew."PositionInVerse"
                           WHERE 1=1
                           --    AND ew."HebRefId" <> ew2."LxxRefId"
                               AND ew."HebRefId" >= 40000000
                               AND ew2."PlainWord" COLLATE "und-x-icu" = ew."PlainWord" COLLATE "und-x-icu"
                   --            AND ew."Id" >= 590800
                           ORDER BY ew."HebRefId", ew."PositionInVerse"
                       ) translatedData ON egm."LxxRefId" = translatedData."LxxRefId" AND egm."WordOrderInVerse" = translatedData."PosIVNew"
                       LEFT JOIN (
                           SELECT
                               ew."Id"
                               , ew."HebRefId"
                               , ew."BibleBookId"
                               , ew."Chapter"
                               , ew."Verse"
                               , ew."PositionInVerse" "PosIVNew"
                               , ew2."LxxRefId"
                               , ew2."Id" "ElbWordId"
                               , ew."PlainWord" "NewPlainWord"
                               , ew2."PlainWord" "OldPlainWord"
                               , ew2."PositionInVerse" "PosIVOld"
                           FROM "unshackled-word"."Elb1871Words" ew
                               INNER JOIN "unshackled-word"."BibleVerseCountingMapping" bvcm ON ew."HebRefId" = bvcm."HebRefId"
                               INNER JOIN "unshackled-word-backup01"."Elb1871Words" ew2 ON bvcm."LxxRefId" = ew2."LxxRefId" AND ew2."PositionInVerse" = ew."PositionInVerse"
                           WHERE 1=1
                           --    AND ew."HebRefId" <> ew2."LxxRefId"
                               AND ew."HebRefId" >= 40000000
                               AND ew2."PlainWord" COLLATE "und-x-icu" = ew."PlainWord" COLLATE "und-x-icu"
                           ORDER BY ew."HebRefId", ew."PositionInVerse"
                       ) parentWordData ON parentWordData."ElbWordId" = egm."ParentGermanWordId"
                   WHERE 1=1
                       and egm."LxxRefId" NOT IN (
                           SELECT problematicRefIds."LxxRefId"
                           FROM (
                               select count(egm."WordOrderInVerse"), egm."WordOrderInVerse", egm."LxxRefId"
                               from "unshackled-word-backup01"."Elb1871GreekMapping" egm
                               GROUP BY egm."WordOrderInVerse", egm."LxxRefId"
                               having count(egm."WordOrderInVerse") > 1
                               ORDER BY egm."LxxRefId", egm."WordOrderInVerse"
                           ) problematicRefIds
                       )
                   ORDER BY translatedData."HebRefId", translatedData."PosIVNew"
                   """;

        await _dbWriter.ExecuteScalarAsync<int>(sql);
    }
}
