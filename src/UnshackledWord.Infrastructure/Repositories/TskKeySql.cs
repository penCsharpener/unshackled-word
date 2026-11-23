namespace UnshackledWord.Infrastructure.Repositories;

/// <summary>
/// Roadmap 0024.2: fills the stable keys of <c>Tsk</c> (<c>SourceHebRefId</c>, <c>TargetStartHebRefId</c>, <c>TargetEndHebRefId</c>) for rows that do
/// not have them yet. Run by the TSK seed after its insert; migration <c>0044_TskKeys.sql</c> holds the same two statements as backfill (a test keeps
/// them identical). The normalisation equals <c>CrossReferenceMapper.Build</c>; a source verse that maps to several Hebrew verses gets one copy
/// of its rows per further verse.
/// </summary>
public static class TskKeySql
{
    public const string CopyMultiMappedSources = """
                                                 INSERT INTO "unshackled-word"."Tsk" ("LxxRefId", "Scope", "RelatedStartLxxRefId", "RelatedEndLxxRefId", "Sort", "SourceHebRefId")
                                                 SELECT t."LxxRefId"
                                                      , t."Scope"
                                                      , t."RelatedStartLxxRefId"
                                                      , t."RelatedEndLxxRefId"
                                                      , t."Sort"
                                                      , bvcm."HebRefId"
                                                 FROM "unshackled-word"."Tsk" t
                                                 INNER JOIN "unshackled-word"."BibleVerseCountingMapping" bvcm ON bvcm."LxxRefId" = t."LxxRefId"
                                                 WHERE 1=1
                                                     AND t."SourceHebRefId" IS NULL
                                                     AND bvcm."HebRefId" > (SELECT MIN(m."HebRefId")
                                                                            FROM "unshackled-word"."BibleVerseCountingMapping" m
                                                                            WHERE m."LxxRefId" = t."LxxRefId")
                                                 ORDER BY bvcm."HebRefId", t."Id";
                                                 """;

    public const string FillKeys = """
                                   UPDATE "unshackled-word"."Tsk" t
                                   SET "SourceHebRefId"      = COALESCE(t."SourceHebRefId", k."SourceHebRefId")
                                     , "TargetStartHebRefId" = CASE WHEN k."EndRefId" IS NOT NULL AND k."EndRefId" / 1000 = k."StartRefId" / 1000
                                                                    THEN LEAST(k."StartRefId", k."EndRefId")
                                                                    ELSE k."StartRefId" END
                                     , "TargetEndHebRefId"   = CASE WHEN k."EndRefId" IS NOT NULL AND k."EndRefId" / 1000 = k."StartRefId" / 1000 AND k."EndRefId" <> k."StartRefId"
                                                                    THEN GREATEST(k."StartRefId", k."EndRefId") END
                                   FROM (SELECT r."Id"
                                              , (SELECT MIN(m."HebRefId")
                                                 FROM "unshackled-word"."BibleVerseCountingMapping" m
                                                 WHERE m."LxxRefId" = r."LxxRefId") AS "SourceHebRefId"
                                              , COALESCE((SELECT MIN(s."HebRefId")
                                                          FROM "unshackled-word"."BibleVerseCountingMapping" s
                                                          WHERE s."LxxRefId" = r."RelatedStartLxxRefId"), r."RelatedStartLxxRefId") AS "StartRefId"
                                              , CASE WHEN r."RelatedEndLxxRefId" IS NULL THEN NULL
                                                     ELSE COALESCE((SELECT MAX(e."HebRefId")
                                                                    FROM "unshackled-word"."BibleVerseCountingMapping" e
                                                                    WHERE e."LxxRefId" = r."RelatedEndLxxRefId"), r."RelatedEndLxxRefId") END AS "EndRefId"
                                         FROM "unshackled-word"."Tsk" r
                                         WHERE r."TargetStartHebRefId" IS NULL) k
                                   WHERE k."Id" = t."Id";
                                   """;

    /// <summary>Both statements in order, for one round trip (Npgsql runs them in one implicit transaction).</summary>
    public const string All = CopyMultiMappedSources + "\n" + FillKeys;
}
