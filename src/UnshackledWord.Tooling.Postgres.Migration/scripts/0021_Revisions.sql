-- Revisions (roadmap 0011 follow-up): "RevisionLevels" becomes "Revisions" with its own surrogate key. A revision's "Level" is now a plain,
-- editable attribute (several revisions may share one, e.g. Elb1871 and Elberfelder 2027 at 200; the reader prefers the revision that
-- has a variant), and "RevisionVerses" refer to the revision by "RevisionId" instead of by level value.
-- The base (Elb1871, never stored as a variant) moves from level 100 to 200; 100 is free for "Elberfelder 2027 Extra".

ALTER TABLE "unshackled-word"."RevisionVerses" DROP CONSTRAINT IF EXISTS "RevisionVerses_Level_FK";
ALTER TABLE "unshackled-word"."RevisionVerses" DROP CONSTRAINT IF EXISTS "RevisionVerses_Level_CK";
ALTER TABLE "unshackled-word"."RevisionVerses" DROP CONSTRAINT IF EXISTS "RevisionVerses_Version_UQ";
DROP INDEX IF EXISTS "unshackled-word"."RevisionVerses_Active_UQ";
DROP INDEX IF EXISTS "unshackled-word"."RevisionVerses_Unreviewed_idx";

ALTER TABLE "unshackled-word"."RevisionLevels" RENAME TO "Revisions";
ALTER TABLE "unshackled-word"."Revisions" ADD COLUMN "Id" serial4 NOT NULL;
ALTER TABLE "unshackled-word"."Revisions" DROP CONSTRAINT "RevisionLevels_PK";
ALTER TABLE "unshackled-word"."Revisions" DROP CONSTRAINT "RevisionLevels_IsBase_CK";
ALTER TABLE "unshackled-word"."Revisions" ADD CONSTRAINT "Revisions_PK" PRIMARY KEY ("Id");
ALTER TABLE "unshackled-word"."Revisions" RENAME CONSTRAINT "RevisionLevels_Code_UQ" TO "Revisions_Code_UQ";
ALTER TABLE "unshackled-word"."Revisions" RENAME CONSTRAINT "RevisionLevels_Level_CK" TO "Revisions_Level_CK";
ALTER INDEX "unshackled-word"."RevisionLevels_Default_UQ" RENAME TO "Revisions_Default_UQ";

ALTER TABLE "unshackled-word"."RevisionVerses" ADD COLUMN "RevisionId" int4 NULL;
UPDATE "unshackled-word"."RevisionVerses" rv
SET "RevisionId" = r."Id"
FROM "unshackled-word"."Revisions" r
WHERE r."Level" = rv."Level";
ALTER TABLE "unshackled-word"."RevisionVerses" ALTER COLUMN "RevisionId" SET NOT NULL;
ALTER TABLE "unshackled-word"."RevisionVerses" DROP COLUMN "Level";

-- The base moves to 200 (variants stay at the level they were written at).
UPDATE "unshackled-word"."Revisions" SET "Level" = 200 WHERE "IsBase";
CREATE UNIQUE INDEX "Revisions_Base_UQ" ON "unshackled-word"."Revisions" ("IsBase") WHERE "IsBase";

ALTER TABLE "unshackled-word"."RevisionVerses" ADD CONSTRAINT "RevisionVerses_Revision_FK" FOREIGN KEY ("RevisionId") REFERENCES "unshackled-word"."Revisions" ("Id");
ALTER TABLE "unshackled-word"."RevisionVerses" ADD CONSTRAINT "RevisionVerses_Version_UQ" UNIQUE ("HebRefId", "RevisionId", "Version");
-- The invariant of the whole feature: one visible variant per verse and revision.
CREATE UNIQUE INDEX "RevisionVerses_Active_UQ" ON "unshackled-word"."RevisionVerses" ("HebRefId", "RevisionId") WHERE "IsActive";
CREATE INDEX "RevisionVerses_Unreviewed_idx" ON "unshackled-word"."RevisionVerses" ("RevisionId", "HebRefId") WHERE "IsActive" AND "MappingReviewedAt" IS NULL;

COMMENT ON TABLE "unshackled-word"."Revisions" IS 'Translation revisions = stops of the reader slider. Level 1 = extremely wooden .. 1000 = toddler language; the base (Elb1871, IsBase) is never stored as a variant. A variant can only be written for a revision listed here.';
COMMENT ON COLUMN "unshackled-word"."Revisions"."Level" IS 'Position on the literalness slider; not unique: at equal level the revision with a variant wins over the base.';
