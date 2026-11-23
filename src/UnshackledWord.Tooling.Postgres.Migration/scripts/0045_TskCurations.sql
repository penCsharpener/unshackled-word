-- Curated TSK cross references (roadmap 0024.3, docs/roadmaps/0024-tsk-curation/03-model.md).
--   TskCurations      current curation state per link; only links that differ from plain TSK have a row (a row that turns neutral is deleted)
--   TskCurationAudit  append-only history of every applied change (before/after state), local data like "RevisionVerseAudit"
-- A link is keyed by ("SourceHebRefId", "TargetStartHebRefId", "TargetEndHebRefId") in Elberfelder numbering, normalised like the stored "Tsk" keys
-- (migration 0044, TskKeySql): "TargetEndHebRefId" NULL = single verse, otherwise after the start and in the same chapter. Never "Tsk"."Id".
-- The word-link columns ("StartPosition", "EndPosition", "QuotedText") are filled from 0024.5 on, "Origin" = 'added' from 0024.4 on.
-- "TskCurations_Word_CK" spells out IS NOT NULL: a CHECK that evaluates to NULL passes.
-- No ON DELETE CASCADE on "Users": users are deactivated, not deleted (roadmap 0020).
-- The audit is not part of the repo seed (the seed change file of 0024.7 carries no personal data).
--
-- Idempotent: safe to run again.

CREATE TABLE IF NOT EXISTS "unshackled-word"."TskCurations"
(
    "Id"                  int4         GENERATED ALWAYS AS IDENTITY,
    "SourceHebRefId"      int4         NOT NULL,
    "TargetStartHebRefId" int4         NOT NULL,
    "TargetEndHebRefId"   int4         NULL,
    "Origin"              varchar(5)   NOT NULL DEFAULT 'tsk',
    "Rating"              varchar(9)   NULL,
    "Comment"             varchar(500) NULL,
    "StartPosition"       int4         NULL,
    "EndPosition"         int4         NULL,
    "QuotedText"          varchar(500) NULL,
    "UpdatedAt"           timestamptz  NOT NULL DEFAULT now(),
    "UpdatedByUserId"     int4         NULL,
    CONSTRAINT "TskCurations_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "TskCurations_UpdatedBy_FK" FOREIGN KEY ("UpdatedByUserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "TskCurations_Origin_CK" CHECK ("Origin" IN ('tsk', 'added')),
    CONSTRAINT "TskCurations_Rating_CK" CHECK ("Rating" IS NULL OR "Rating" IN ('highlight', 'hide')),
    CONSTRAINT "TskCurations_Comment_CK" CHECK ("Comment" IS NULL OR char_length(btrim("Comment")) > 0),
    CONSTRAINT "TskCurations_Word_CK" CHECK (
           ("StartPosition" IS NULL AND "EndPosition" IS NULL AND "QuotedText" IS NULL)
        OR ("StartPosition" IS NOT NULL AND "EndPosition" IS NOT NULL AND "QuotedText" IS NOT NULL AND "StartPosition" BETWEEN 1 AND "EndPosition")),
    CONSTRAINT "TskCurations_Range_CK" CHECK ("TargetEndHebRefId" IS NULL
        OR ("TargetEndHebRefId" > "TargetStartHebRefId" AND "TargetEndHebRefId" / 1000 = "TargetStartHebRefId" / 1000))
);

-- one row per link; the chapter read joins on it (source verse range first)
CREATE UNIQUE INDEX IF NOT EXISTS "TskCurations_Key_UX" ON "unshackled-word"."TskCurations"
    ("SourceHebRefId", "TargetStartHebRefId", COALESCE("TargetEndHebRefId", 0));

COMMENT ON TABLE "unshackled-word"."TskCurations" IS 'Curation state of TSK cross references (roadmap 0024): one row per link that differs from plain TSK. Default rows are deleted.';
COMMENT ON COLUMN "unshackled-word"."TskCurations"."SourceHebRefId" IS 'Source verse (Elberfelder numbering), same key as Tsk.SourceHebRefId.';
COMMENT ON COLUMN "unshackled-word"."TskCurations"."TargetEndHebRefId" IS 'NULL = single verse; otherwise the last verse of a range within the chapter of TargetStartHebRefId.';
COMMENT ON COLUMN "unshackled-word"."TskCurations"."Origin" IS 'tsk (the link exists in Tsk) | added (a link TSK lacks, roadmap 0024.4).';
COMMENT ON COLUMN "unshackled-word"."TskCurations"."Rating" IS 'highlight (marked and sorted first) | hide (not shown to readers) | NULL (neutral).';
COMMENT ON COLUMN "unshackled-word"."TskCurations"."Comment" IS 'Short plain-text comment (max 500 chars, trimmed, never empty); scripture references are linked on read.';
COMMENT ON COLUMN "unshackled-word"."TskCurations"."StartPosition" IS 'Elb1871Words.PositionInVerse of the first linked word in the source verse (roadmap 0024.5).';
COMMENT ON COLUMN "unshackled-word"."TskCurations"."QuotedText" IS 'Elb1871 words at link time; compared on read to detect a drifted word link.';
COMMENT ON COLUMN "unshackled-word"."TskCurations"."UpdatedByUserId" IS 'Last editor; NULL after a seed import.';

CREATE TABLE IF NOT EXISTS "unshackled-word"."TskCurationAudit"
(
    "Id"                  int8         GENERATED ALWAYS AS IDENTITY,
    "SourceHebRefId"      int4         NOT NULL,
    "TargetStartHebRefId" int4         NOT NULL,
    "TargetEndHebRefId"   int4         NULL,
    "Before"              jsonb        NULL,
    "After"               jsonb        NULL,
    "UserId"              int4         NULL,
    "At"                  timestamptz  NOT NULL DEFAULT now(),
    "Source"              varchar(8)   NOT NULL,
    CONSTRAINT "TskCurationAudit_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "TskCurationAudit_Users_FK" FOREIGN KEY ("UserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "TskCurationAudit_Source_CK" CHECK ("Source" IN ('edit', 'proposal', 'import'))
);

-- history of one link, newest first
CREATE INDEX IF NOT EXISTS "TskCurationAudit_Key_idx" ON "unshackled-word"."TskCurationAudit"
    ("SourceHebRefId", "TargetStartHebRefId", "TargetEndHebRefId", "At" DESC);

COMMENT ON TABLE "unshackled-word"."TskCurationAudit" IS 'Append-only history of TskCurations: one row per applied change. Local data, not part of the repo seed.';
COMMENT ON COLUMN "unshackled-word"."TskCurationAudit"."Before" IS 'Curation state before the change ({origin, rating, comment, startPosition, endPosition, quotedText}); NULL = no row (plain TSK).';
COMMENT ON COLUMN "unshackled-word"."TskCurationAudit"."After" IS 'Curation state after the change, same shape as Before; NULL = the row was deleted (back to plain TSK).';
COMMENT ON COLUMN "unshackled-word"."TskCurationAudit"."UserId" IS 'Who applied the change (for proposals: the accepting maintainer); NULL for seed imports.';
COMMENT ON COLUMN "unshackled-word"."TskCurationAudit"."Source" IS 'edit (direct maintainer edit) | proposal (accepted suggestion, 0024.6) | import (seed change file, 0024.7).';
