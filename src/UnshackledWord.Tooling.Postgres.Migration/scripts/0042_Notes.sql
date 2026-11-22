-- Notes on chapters, verses and words (roadmap 0023, docs/roadmaps/0023-notes/03-model-and-anchoring.md).
--   Notes  one row per note; "Kind" decides which anchor columns are filled (chapter | verses | verse | words)
-- Anchors use natural keys ("HebRefId" = book * 1,000,000 + chapter * 1,000 + verse, Elb1871 "PositionInVerse"), never "Elb1871Words"."Id":
-- the rebase deletes and re-inserts the word rows. "Id" is a uuid so export/import across databases needs no id remapping.
-- "Notes_Anchor_CK" spells out IS NOT NULL: a CHECK that evaluates to NULL passes, so "NULL = NULL" alone would let a verse note without a verse in.
-- No ON DELETE CASCADE on "Users": users are deactivated, not deleted (roadmap 0020).
-- Notes are user data and never part of the repo seed (export/import goes through temp/notes, roadmap 0023.6).
--
-- Idempotent: safe to run again.

CREATE TABLE IF NOT EXISTS "unshackled-word"."Notes"
(
    "Id"                   uuid         NOT NULL DEFAULT gen_random_uuid(),
    "UserId"               int4         NOT NULL,
    "Kind"                 varchar(10)  NOT NULL,
    "BibleBookId"          int4         NOT NULL,
    "Chapter"              int4         NOT NULL,
    "StartHebRefId"        int4         NULL,
    "EndHebRefId"          int4         NULL,
    "StartPosition"        int4         NULL,
    "EndPosition"          int4         NULL,
    "QuotedText"           varchar(500) NULL,
    "BodyMarkdown"         text         NOT NULL,
    "Visibility"           varchar(10)  NOT NULL DEFAULT 'private',
    "PublicState"          varchar(10)  NOT NULL DEFAULT 'none',
    "ApprovedBodyMarkdown" text         NULL,
    "ApprovedAt"           timestamptz  NULL,
    "ApprovedByUserId"     int4         NULL,
    "ReviewNote"           varchar(500) NULL,
    "HiddenAt"             timestamptz  NULL,
    "HiddenByUserId"       int4         NULL,
    "CreatedAt"            timestamptz  NOT NULL DEFAULT now(),
    "UpdatedAt"            timestamptz  NULL,
    CONSTRAINT "Notes_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "Notes_Users_FK" FOREIGN KEY ("UserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "Notes_ApprovedBy_FK" FOREIGN KEY ("ApprovedByUserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "Notes_HiddenBy_FK" FOREIGN KEY ("HiddenByUserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "Notes_Kind_CK" CHECK ("Kind" IN ('chapter', 'verses', 'verse', 'words')),
    CONSTRAINT "Notes_Anchor_CK" CHECK (
           ("Kind" = 'chapter' AND "StartHebRefId" IS NULL AND "EndHebRefId" IS NULL AND "StartPosition" IS NULL AND "EndPosition" IS NULL AND "QuotedText" IS NULL)
        OR ("Kind" = 'verses'  AND "StartHebRefId" IS NOT NULL AND "EndHebRefId" IS NOT NULL AND "StartHebRefId" < "EndHebRefId" AND "StartHebRefId" / 1000 = "EndHebRefId" / 1000 AND "StartPosition" IS NULL AND "EndPosition" IS NULL AND "QuotedText" IS NULL)
        OR ("Kind" = 'verse'   AND "StartHebRefId" IS NOT NULL AND "EndHebRefId" IS NOT NULL AND "StartHebRefId" = "EndHebRefId" AND "StartPosition" IS NULL AND "EndPosition" IS NULL AND "QuotedText" IS NULL)
        OR ("Kind" = 'words'   AND "StartHebRefId" IS NOT NULL AND "EndHebRefId" IS NOT NULL AND "StartHebRefId" = "EndHebRefId" AND "StartPosition" IS NOT NULL AND "EndPosition" IS NOT NULL AND "StartPosition" BETWEEN 1 AND "EndPosition" AND "QuotedText" IS NOT NULL)),
    CONSTRAINT "Notes_AnchorChapter_CK" CHECK ("StartHebRefId" IS NULL OR "StartHebRefId" / 1000 = "BibleBookId" * 1000 + "Chapter"),
    CONSTRAINT "Notes_Visibility_CK" CHECK ("Visibility" IN ('private', 'internal', 'public')),
    CONSTRAINT "Notes_PublicState_CK" CHECK ("PublicState" IN ('none', 'pending', 'approved', 'rejected')
        AND ("Visibility" = 'public') = ("PublicState" <> 'none')),
    CONSTRAINT "Notes_Body_CK" CHECK (char_length("BodyMarkdown") BETWEEN 1 AND 20000)
);

-- chapter endpoint (reader), "Meine Notizen" (newest first), maintainer review queue
CREATE INDEX IF NOT EXISTS "Notes_Chapter_idx" ON "unshackled-word"."Notes" ("BibleBookId", "Chapter");
CREATE INDEX IF NOT EXISTS "Notes_User_idx" ON "unshackled-word"."Notes" ("UserId", "UpdatedAt" DESC);
CREATE INDEX IF NOT EXISTS "Notes_Pending_idx" ON "unshackled-word"."Notes" ("UpdatedAt") WHERE "PublicState" = 'pending';

COMMENT ON TABLE "unshackled-word"."Notes" IS 'User notes on a chapter, verse range, verse or word range. User data, not part of the repo seed.';
COMMENT ON COLUMN "unshackled-word"."Notes"."Kind" IS 'chapter | verses (range within one chapter) | verse | words (range within one verse).';
COMMENT ON COLUMN "unshackled-word"."Notes"."StartHebRefId" IS 'NULL for chapter notes; EndHebRefId = StartHebRefId for verse and words notes.';
COMMENT ON COLUMN "unshackled-word"."Notes"."StartPosition" IS 'Elb1871Words.PositionInVerse of the first anchored word; words notes only.';
COMMENT ON COLUMN "unshackled-word"."Notes"."QuotedText" IS 'Elb1871 words at save time; compared on read to detect a drifted word anchor.';
COMMENT ON COLUMN "unshackled-word"."Notes"."BodyMarkdown" IS 'Raw Markdown as typed (1..20000 chars); scripture links are computed on read.';
COMMENT ON COLUMN "unshackled-word"."Notes"."Visibility" IS 'private (author only) | internal (logged-in users) | public (everyone once approved).';
COMMENT ON COLUMN "unshackled-word"."Notes"."PublicState" IS 'none unless Visibility = public; then pending | approved | rejected.';
COMMENT ON COLUMN "unshackled-word"."Notes"."ApprovedBodyMarkdown" IS 'Snapshot the maintainer approved; anonymous readers see this text, not BodyMarkdown.';
COMMENT ON COLUMN "unshackled-word"."Notes"."ReviewNote" IS 'Maintainer reason for a rejection or a hide.';
COMMENT ON COLUMN "unshackled-word"."Notes"."HiddenAt" IS 'Set by a maintainer: the note disappears for everyone but the author.';
