-- Stable keys on "Tsk" (roadmap 0024.2): the source verse and the target range in Elberfelder (Hebrew) numbering, stored once instead of
-- translated per request. "Sort" is the TSK file's sort column (order of the keyword entries within a verse); it stays NULL for existing rows,
-- whose order is "Id" as before, and is written by the seed (TskStrategy).
--
-- The backfill reproduces the former read path of TskRepository.GetOutgoingAsync plus CrossReferenceMapper exactly:
--   source  = BibleVerseCountingMapping (unmapped sources stay NULL and are not shown, as before)
--   targets = MIN/MAX("HebRefId") with "LxxRefId" fallback, then normalised: a backward range is swapped, start = end collapses to one verse
--             (end NULL), a range across books or chapters is cut to its first verse (end NULL).
-- A source "LxxRefId" that maps to several Hebrew verses (1Sam 20,42 -> 20,42 + 21,1 and four more) showed its rows under every one of them;
-- each such row is copied once per further Hebrew verse so that every row has exactly one source key.
-- Keep the two statements below identical to TskKeySql (UnshackledWord.Infrastructure), which the seed runs after its insert.

ALTER TABLE "unshackled-word"."Tsk"
    ADD COLUMN IF NOT EXISTS "Sort"                int4 NULL,
    ADD COLUMN IF NOT EXISTS "SourceHebRefId"      int4 NULL,
    ADD COLUMN IF NOT EXISTS "TargetStartHebRefId" int4 NULL,
    ADD COLUMN IF NOT EXISTS "TargetEndHebRefId"   int4 NULL;

-- TskKeySql.CopyMultiMappedSources
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

-- TskKeySql.FillKeys
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

CREATE INDEX IF NOT EXISTS "Tsk_Source_idx" ON "unshackled-word"."Tsk" ("SourceHebRefId", "Sort", "Id");
