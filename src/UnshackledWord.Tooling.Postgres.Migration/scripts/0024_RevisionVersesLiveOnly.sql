-- Revision audit log (roadmap 0014, destructive part): "RevisionVerses" holds only the live version of a verse (one row per verse and revision).
-- The history of every text save, mapping save and withdraw lives in "RevisionVerseAudit" (0023). Words and mapping of ended versions are deleted
-- for good: afterwards they exist only as verse text plus the mapping snapshot of the audit row. Back up the database before running this script.
--
-- The self-checks run FIRST, before anything is changed, so a failing check leaves the database untouched whether or not the runner wraps the
-- script in a transaction; a last check at the end compares the result.
-- VACUUM cannot run inside a migration: run VACUUM (ANALYZE) on the three revision tables once afterwards.

DO
$$
    DECLARE
        verse_rows   int8;
        audit_text   int8;
        missing_live int8;
    BEGIN
        IF to_regclass('"unshackled-word"."RevisionVerseAudit"') IS NULL THEN
            RAISE EXCEPTION 'RevisionVerseAudit is missing: run migration 0023 first';
        END IF;

        SELECT count(*) INTO verse_rows FROM "unshackled-word"."RevisionVerses";
        SELECT count(*) INTO audit_text FROM "unshackled-word"."RevisionVerseAudit" WHERE "Action" = 'text';

        -- Every old verse row (one per text version) has a backfilled or dual-written 'text' audit row.
        IF audit_text < verse_rows THEN
            RAISE EXCEPTION 'RevisionVerseAudit is incomplete: % text audit rows for % RevisionVerses rows. Nothing was changed.', audit_text, verse_rows;
        END IF;

        -- Every live (active) verse has an audit 'text' row with its version.
        SELECT count(*)
        INTO missing_live
        FROM "unshackled-word"."RevisionVerses" rv
        WHERE rv."IsActive"
            AND NOT EXISTS (SELECT 1
                            FROM "unshackled-word"."RevisionVerseAudit" a
                            WHERE a."HebRefId" = rv."HebRefId"
                                AND a."RevisionId" = rv."RevisionId"
                                AND a."Version" = rv."Version"
                                AND a."Action" = 'text');

        IF missing_live > 0 THEN
            RAISE EXCEPTION 'RevisionVerseAudit is incomplete: % live verses without an audit text row for their version. Nothing was changed.', missing_live;
        END IF;
    END
$$;

-- 1. New columns, filled for the live rows: parent revision and version come from the referenced parent row (which may be an older version of the
--    parent), UpdatedBy/At from the live row's own CreatedBy/At (= the last text save), CreatedBy/At become the first creation of the verse.
ALTER TABLE "unshackled-word"."RevisionVerses"
    ADD COLUMN IF NOT EXISTS "BasedOnRevisionId" int4        NULL,
    ADD COLUMN IF NOT EXISTS "BasedOnVersion"    int4        NULL,
    ADD COLUMN IF NOT EXISTS "UpdatedByUserId"   int4        NULL,
    ADD COLUMN IF NOT EXISTS "UpdatedAt"         timestamptz NULL;

UPDATE "unshackled-word"."RevisionVerses" rv
SET "BasedOnRevisionId" = p."RevisionId"
  , "BasedOnVersion"    = p."Version"
FROM "unshackled-word"."RevisionVerses" p
WHERE rv."IsActive"
    AND p."Id" = rv."BasedOnRevisionVerseId";

UPDATE "unshackled-word"."RevisionVerses" rv
SET "UpdatedByUserId" = rv."CreatedByUserId"
  , "UpdatedAt"       = rv."CreatedAt"
WHERE rv."IsActive";

UPDATE "unshackled-word"."RevisionVerses" rv
SET "CreatedByUserId" = f."CreatedByUserId"
  , "CreatedAt"       = f."CreatedAt"
FROM (SELECT DISTINCT ON (o."HebRefId", o."RevisionId") o."HebRefId", o."RevisionId", o."CreatedByUserId", o."CreatedAt"
      FROM "unshackled-word"."RevisionVerses" o
      ORDER BY o."HebRefId"
             , o."RevisionId"
             , o."Id") f
WHERE rv."IsActive"
    AND f."HebRefId" = rv."HebRefId"
    AND f."RevisionId" = rv."RevisionId";

-- 2. The parent link no longer points at a (historic) row.
ALTER TABLE "unshackled-word"."RevisionVerses" DROP CONSTRAINT IF EXISTS "RevisionVerses_BasedOn_FK";
ALTER TABLE "unshackled-word"."RevisionVerses" DROP COLUMN IF EXISTS "BasedOnRevisionVerseId";

-- 3. Delete the ended versions: mapping, then words, then the verse rows.
DELETE
FROM "unshackled-word"."RevisionMapping" rm
USING "unshackled-word"."RevisionWords" rw
    , "unshackled-word"."RevisionVerses" rv
WHERE rw."Id" = rm."RevisionWordId"
    AND rv."Id" = rw."RevisionVerseId"
    AND NOT rv."IsActive";

DELETE
FROM "unshackled-word"."RevisionWords" rw
USING "unshackled-word"."RevisionVerses" rv
WHERE rv."Id" = rw."RevisionVerseId"
    AND NOT rv."IsActive";

DELETE
FROM "unshackled-word"."RevisionVerses"
WHERE NOT "IsActive";

-- 4. Lifecycle columns and the constraints/indexes that only existed for them.
DROP INDEX IF EXISTS "unshackled-word"."RevisionVerses_Search_idx";
DROP INDEX IF EXISTS "unshackled-word"."RevisionVerses_Unreviewed_idx";
DROP INDEX IF EXISTS "unshackled-word"."RevisionVerses_Active_UQ";

ALTER TABLE "unshackled-word"."RevisionVerses" DROP CONSTRAINT IF EXISTS "RevisionVerses_Lifecycle_CK";
ALTER TABLE "unshackled-word"."RevisionVerses" DROP CONSTRAINT IF EXISTS "RevisionVerses_Version_UQ";
ALTER TABLE "unshackled-word"."RevisionVerses" DROP CONSTRAINT IF EXISTS "RevisionVerses_EndedBy_FK";
ALTER TABLE "unshackled-word"."RevisionVerses"
    DROP COLUMN IF EXISTS "IsActive",
    DROP COLUMN IF EXISTS "EndedAt",
    DROP COLUMN IF EXISTS "EndedByUserId";

UPDATE "unshackled-word"."RevisionVerses"
SET "UpdatedAt" = "CreatedAt"
WHERE "UpdatedAt" IS NULL;

ALTER TABLE "unshackled-word"."RevisionVerses"
    ALTER COLUMN "UpdatedAt" SET NOT NULL,
    ALTER COLUMN "UpdatedAt" SET DEFAULT now();

ALTER TABLE "unshackled-word"."RevisionVerses"
    ADD CONSTRAINT "RevisionVerses_UpdatedBy_FK" FOREIGN KEY ("UpdatedByUserId") REFERENCES "unshackled-word"."Users" ("Id"),
    ADD CONSTRAINT "RevisionVerses_BasedOn_FK" FOREIGN KEY ("BasedOnRevisionId") REFERENCES "unshackled-word"."Revisions" ("Id"),
    ADD CONSTRAINT "RevisionVerses_BasedOn_CK" CHECK (("BasedOnRevisionId" IS NULL) = ("BasedOnVersion" IS NULL)),
    ADD CONSTRAINT "RevisionVerses_Verse_UQ" UNIQUE ("HebRefId", "RevisionId");

CREATE INDEX "RevisionVerses_Search_idx" ON "unshackled-word"."RevisionVerses" USING gin ("SearchVector");
CREATE INDEX "RevisionVerses_Unreviewed_idx" ON "unshackled-word"."RevisionVerses" ("RevisionId", "HebRefId") WHERE "MappingReviewedAt" IS NULL;

-- 5. A rewrite of the words (text save) and a withdraw delete the verse's words and mapping with one DELETE.
ALTER TABLE "unshackled-word"."RevisionWords" DROP CONSTRAINT "RevisionWords_Verse_FK";
ALTER TABLE "unshackled-word"."RevisionWords"
    ADD CONSTRAINT "RevisionWords_Verse_FK" FOREIGN KEY ("RevisionVerseId") REFERENCES "unshackled-word"."RevisionVerses" ("Id") ON DELETE CASCADE;

ALTER TABLE "unshackled-word"."RevisionMapping" DROP CONSTRAINT "RevisionMapping_Word_FK";
ALTER TABLE "unshackled-word"."RevisionMapping" DROP CONSTRAINT "RevisionMapping_Parent_FK";
ALTER TABLE "unshackled-word"."RevisionMapping"
    ADD CONSTRAINT "RevisionMapping_Word_FK" FOREIGN KEY ("RevisionWordId") REFERENCES "unshackled-word"."RevisionWords" ("Id") ON DELETE CASCADE,
    ADD CONSTRAINT "RevisionMapping_Parent_FK" FOREIGN KEY ("ParentRevisionWordId") REFERENCES "unshackled-word"."RevisionWords" ("Id") ON DELETE CASCADE;

COMMENT ON TABLE "unshackled-word"."RevisionVerses" IS 'The live version of one Elb1871 verse at one revision (literalness level): one row per (HebRefId, RevisionId). A text save updates it in place and rewrites its words and mapping, a withdraw deletes it. The history of every save lives in RevisionVerseAudit.';
COMMENT ON COLUMN "unshackled-word"."RevisionVerses"."Id" IS 'Stable across text saves; RevisionWords.Id changes on every text save.';
COMMENT ON COLUMN "unshackled-word"."RevisionVerses"."Version" IS 'Current text version, >= 1; +1 per text save (optimistic concurrency), continues after a withdraw from the highest audited version. A mapping save keeps it.';
COMMENT ON COLUMN "unshackled-word"."RevisionVerses"."BasedOnRevisionId" IS 'The revision this text was started from (revision of a revision); NULL = started from Elb1871. Together with BasedOnVersion the text of the parent when this one was written.';
COMMENT ON COLUMN "unshackled-word"."RevisionVerses"."BasedOnVersion" IS 'Version of the parent revision''s verse when this text was written; NULL exactly when BasedOnRevisionId is NULL.';
COMMENT ON COLUMN "unshackled-word"."RevisionVerses"."CreatedByUserId" IS 'First creation of the verse at this revision; NULL = imported from the repo seed.';
COMMENT ON COLUMN "unshackled-word"."RevisionVerses"."CreatedAt" IS 'First creation of the verse at this revision (a re-created verse after a withdraw starts again).';
COMMENT ON COLUMN "unshackled-word"."RevisionVerses"."UpdatedByUserId" IS 'Author of the last text save; NULL = imported from the repo seed.';
COMMENT ON COLUMN "unshackled-word"."RevisionVerses"."UpdatedAt" IS 'Time of the last text save.';
COMMENT ON COLUMN "unshackled-word"."RevisionVerses"."MappingReviewedAt" IS 'Set when a writer saved the mapping form of this text; reset by a text save; NULL = the mapping is only what the carry-over algorithm produced.';
COMMENT ON TABLE "unshackled-word"."RevisionWords" IS 'The words of one RevisionVerses row, split by spaces; rewritten (new ids) on every text save. Same columns as Elb1871Words where they mean the same.';
COMMENT ON TABLE "unshackled-word"."RevisionMapping" IS 'Revision word x original word, Greek and Hebrew in one table. Lang G joins StepGreekWords, Lang H joins StepHebrewWords; both id ranges start at 1, so every join needs the Lang condition. Deleted with its word (cascade).';
COMMENT ON COLUMN "unshackled-word"."RevisionVerseAudit"."RevisionVerseId" IS 'The live row at the time of the action; no FK because the live row may be withdrawn. Stable across text saves of one verse.';

-- Final check: one live row per (verse, revision), every live verse has its audit text row, no orphans left behind by the deletes.
DO
$$
    DECLARE
        live_rows    int8;
        live_distinct int8;
        missing_live int8;
        orphans      int8;
    BEGIN
        SELECT count(*), count(DISTINCT ("HebRefId", "RevisionId")) INTO live_rows, live_distinct FROM "unshackled-word"."RevisionVerses";

        IF live_rows <> live_distinct THEN
            RAISE EXCEPTION 'RevisionVerses has % rows for % distinct (verse, revision) pairs', live_rows, live_distinct;
        END IF;

        SELECT count(*)
        INTO missing_live
        FROM "unshackled-word"."RevisionVerses" rv
        WHERE NOT EXISTS (SELECT 1
                          FROM "unshackled-word"."RevisionVerseAudit" a
                          WHERE a."HebRefId" = rv."HebRefId"
                              AND a."RevisionId" = rv."RevisionId"
                              AND a."Version" = rv."Version"
                              AND a."Action" = 'text');

        IF missing_live > 0 THEN
            RAISE EXCEPTION '% live verses have no audit text row for their version', missing_live;
        END IF;

        SELECT count(*)
        INTO orphans
        FROM "unshackled-word"."RevisionWords" rw
        WHERE NOT EXISTS (SELECT 1 FROM "unshackled-word"."RevisionVerses" rv WHERE rv."Id" = rw."RevisionVerseId");

        IF orphans > 0 THEN
            RAISE EXCEPTION '% RevisionWords rows without a verse', orphans;
        END IF;
    END
$$;
