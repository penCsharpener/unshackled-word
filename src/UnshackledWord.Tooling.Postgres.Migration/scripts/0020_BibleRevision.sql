-- Bible revision (roadmap 0011): verse variants per literalness level on top of the untouched Elb1871 text, their words and one unified
-- mapping table for Greek and Hebrew. Schema only: level presets come from assets/seed/revision/levels.csv (RevisionLevelSeedRunner),
-- variants from the editor or from assets/seed/revision (RevisionSeedRunner).
--
-- Levels: 1 = extremely wooden, 100 = Elb1871 (the base, never stored as a variant), 1000 = toddler language.
-- No foreign keys to Elb1871Words / Elb1871Verses / Step*Words: those tables are rebuilt by the seed (roadmap 0010 "rebase"), so the revision
-- refers to them by natural keys ("HebRefId", "PositionInVerse") and, like the legacy mapping tables, by "StepWordId".

CREATE TABLE IF NOT EXISTS "unshackled-word"."RevisionLevels"
(
    "Level"         int4         NOT NULL,
    "Code"          varchar(30)  NOT NULL,
    "NameDe"        varchar(100) NOT NULL,
    "NameEn"        varchar(100) NOT NULL,
    "DescriptionDe" text         NULL,
    "DescriptionEn" text         NULL,
    "IsBase"        boolean      NOT NULL DEFAULT false,
    "IsDefault"     boolean      NOT NULL DEFAULT false,
    CONSTRAINT "RevisionLevels_PK" PRIMARY KEY ("Level"),
    CONSTRAINT "RevisionLevels_Code_UQ" UNIQUE ("Code"),
    CONSTRAINT "RevisionLevels_Level_CK" CHECK ("Level" BETWEEN 1 AND 1000),
    CONSTRAINT "RevisionLevels_IsBase_CK" CHECK ("IsBase" = ("Level" = 100))
);

-- At most one preset is the level the public reader starts with.
CREATE UNIQUE INDEX IF NOT EXISTS "RevisionLevels_Default_UQ" ON "unshackled-word"."RevisionLevels" ("IsDefault") WHERE "IsDefault";

COMMENT ON TABLE "unshackled-word"."RevisionLevels" IS 'Literalness presets = stops of the reader slider. 100 is Elb1871 (IsBase); a variant can only be written at a level listed here.';


CREATE TABLE IF NOT EXISTS "unshackled-word"."RevisionVerses"
(
    "Id"                      serial4                  NOT NULL,
    "HebRefId"                int4                     NOT NULL,
    "Level"                   int4                     NOT NULL,
    "Version"                 int4                     NOT NULL,
    "VerseText"               text COLLATE "und-x-icu" NOT NULL,
    "BasedOnRevisionVerseId"  int4                     NULL,
    "BaseTextHash"            varchar(12)              NOT NULL,
    "Note"                    varchar(500)             NULL,
    "IsActive"                boolean                  NOT NULL DEFAULT true,
    "CreatedByUserId"         int4                     NULL,
    "CreatedAt"               timestamptz              NOT NULL DEFAULT now(),
    "EndedByUserId"           int4                     NULL,
    "EndedAt"                 timestamptz              NULL,
    "MappingReviewedByUserId" int4                     NULL,
    "MappingReviewedAt"       timestamptz              NULL,
    "SearchVector"            tsvector GENERATED ALWAYS AS (
        setweight(to_tsvector('"unshackled-word".uw_german'::regconfig, "VerseText"), 'A')
        ) STORED,
    CONSTRAINT "RevisionVerses_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "RevisionVerses_Version_UQ" UNIQUE ("HebRefId", "Level", "Version"),
    CONSTRAINT "RevisionVerses_Level_FK" FOREIGN KEY ("Level") REFERENCES "unshackled-word"."RevisionLevels" ("Level"),
    CONSTRAINT "RevisionVerses_BasedOn_FK" FOREIGN KEY ("BasedOnRevisionVerseId") REFERENCES "unshackled-word"."RevisionVerses" ("Id"),
    CONSTRAINT "RevisionVerses_CreatedBy_FK" FOREIGN KEY ("CreatedByUserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "RevisionVerses_EndedBy_FK" FOREIGN KEY ("EndedByUserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "RevisionVerses_ReviewedBy_FK" FOREIGN KEY ("MappingReviewedByUserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "RevisionVerses_Level_CK" CHECK ("Level" <> 100),
    CONSTRAINT "RevisionVerses_Version_CK" CHECK ("Version" >= 1),
    CONSTRAINT "RevisionVerses_Text_CK" CHECK (btrim("VerseText") <> ''),
    CONSTRAINT "RevisionVerses_Lifecycle_CK" CHECK ("IsActive" = ("EndedAt" IS NULL))
);

-- The invariant of the whole feature: one visible variant per verse and level.
CREATE UNIQUE INDEX IF NOT EXISTS "RevisionVerses_Active_UQ" ON "unshackled-word"."RevisionVerses" ("HebRefId", "Level") WHERE "IsActive";
CREATE INDEX IF NOT EXISTS "RevisionVerses_Search_idx" ON "unshackled-word"."RevisionVerses" USING gin ("SearchVector") WHERE "IsActive";
CREATE INDEX IF NOT EXISTS "RevisionVerses_Unreviewed_idx" ON "unshackled-word"."RevisionVerses" ("Level", "HebRefId") WHERE "IsActive" AND "MappingReviewedAt" IS NULL;

COMMENT ON TABLE "unshackled-word"."RevisionVerses" IS 'A full copy of one Elb1871 verse at one literalness level. Every text save inserts a new Version and ends the previous one; rows are never deleted.';
COMMENT ON COLUMN "unshackled-word"."RevisionVerses"."HebRefId" IS 'The Elb1871 verse (BBCCCVVV) this variant replaces. Natural key, no FK: Elb1871Verses is rebuilt by the seed.';
COMMENT ON COLUMN "unshackled-word"."RevisionVerses"."BasedOnRevisionVerseId" IS 'The variant this one was started from (revision of a revision); NULL = started from Elb1871.';
COMMENT ON COLUMN "unshackled-word"."RevisionVerses"."BaseTextHash" IS '12 hex of SHA-256 over the normalised Elb1871 tokens of the verse when the variant was written (same normalisation as the chapter fingerprint of roadmap 0010). Differs from the current verse = the base text was fixed afterwards.';
COMMENT ON COLUMN "unshackled-word"."RevisionVerses"."CreatedByUserId" IS 'NULL = imported from the repo seed.';
COMMENT ON COLUMN "unshackled-word"."RevisionVerses"."MappingReviewedAt" IS 'Set when a writer saved the mapping form of this version; NULL = the mapping is only what the carry-over algorithm produced.';


CREATE TABLE IF NOT EXISTS "unshackled-word"."RevisionWords"
(
    "Id"                  serial4                     NOT NULL,
    "RevisionVerseId"     int4                        NOT NULL,
    "PositionInVerse"     int4                        NOT NULL,
    "WordInContext"       varchar COLLATE "und-x-icu" NOT NULL,
    "PlainWord"           varchar COLLATE "und-x-icu" NULL,
    "BasePositionInVerse" int4                        NULL,
    "Change"              varchar(10)                 NOT NULL,
    CONSTRAINT "RevisionWords_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "RevisionWords_Position_UQ" UNIQUE ("RevisionVerseId", "PositionInVerse"),
    CONSTRAINT "RevisionWords_Verse_FK" FOREIGN KEY ("RevisionVerseId") REFERENCES "unshackled-word"."RevisionVerses" ("Id"),
    CONSTRAINT "RevisionWords_Position_CK" CHECK ("PositionInVerse" >= 1),
    CONSTRAINT "RevisionWords_Change_CK" CHECK ("Change" IN ('same', 'changed', 'new')),
    CONSTRAINT "RevisionWords_Base_CK" CHECK (("Change" = 'new') = ("BasePositionInVerse" IS NULL))
);

COMMENT ON TABLE "unshackled-word"."RevisionWords" IS 'The words of one RevisionVerses row, split by spaces. Same columns as Elb1871Words where they mean the same.';
COMMENT ON COLUMN "unshackled-word"."RevisionWords"."PlainWord" IS 'Is the same as WordInContext but clean of all special characters (same rule as Elb1871Words.PlainWord).';
COMMENT ON COLUMN "unshackled-word"."RevisionWords"."BasePositionInVerse" IS 'PositionInVerse of the Elb1871 word (same HebRefId) this word continues; NULL for a new word. Derived by the carry-over algorithm, recomputable.';
COMMENT ON COLUMN "unshackled-word"."RevisionWords"."Change" IS 'same = identical PlainWord as the base word, changed = replaces the base word(s), new = no base word.';


CREATE TABLE IF NOT EXISTS "unshackled-word"."RevisionMapping"
(
    "Id"                   serial4     NOT NULL,
    "RevisionWordId"       int4        NOT NULL,
    "Lang"                 char(1)     NOT NULL,
    "StepWordId"           int4        NULL,
    "IsAddedWord"          boolean     NOT NULL DEFAULT false,
    "ParentRevisionWordId" int4        NULL,
    "GermanWordPart"       varchar(30) NULL,
    "Origin"               varchar(10) NOT NULL,
    "CreatedByUserId"      int4        NULL,
    "CreatedAt"            timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "RevisionMapping_PK" PRIMARY KEY ("Id"),
    -- Same rule as the legacy tables: one German word with several original words needs one row per original word.
    CONSTRAINT "RevisionMapping_Word_Step_UQ" UNIQUE NULLS NOT DISTINCT ("RevisionWordId", "StepWordId"),
    CONSTRAINT "RevisionMapping_Word_FK" FOREIGN KEY ("RevisionWordId") REFERENCES "unshackled-word"."RevisionWords" ("Id"),
    CONSTRAINT "RevisionMapping_Parent_FK" FOREIGN KEY ("ParentRevisionWordId") REFERENCES "unshackled-word"."RevisionWords" ("Id"),
    CONSTRAINT "RevisionMapping_CreatedBy_FK" FOREIGN KEY ("CreatedByUserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "RevisionMapping_Lang_CK" CHECK ("Lang" IN ('G', 'H')),
    CONSTRAINT "RevisionMapping_Origin_CK" CHECK ("Origin" IN ('carried', 'manual', 'seed')),
    -- A row without an original word is only meaningful as an added word.
    CONSTRAINT "RevisionMapping_Added_CK" CHECK ("StepWordId" IS NOT NULL OR "IsAddedWord"),
    CONSTRAINT "RevisionMapping_Parent_CK" CHECK ("ParentRevisionWordId" IS DISTINCT FROM "RevisionWordId")
);

CREATE INDEX IF NOT EXISTS "RevisionMapping_Step_idx" ON "unshackled-word"."RevisionMapping" ("Lang", "StepWordId") WHERE "StepWordId" IS NOT NULL;
CREATE INDEX IF NOT EXISTS "RevisionMapping_Parent_idx" ON "unshackled-word"."RevisionMapping" ("ParentRevisionWordId") WHERE "ParentRevisionWordId" IS NOT NULL;

COMMENT ON TABLE "unshackled-word"."RevisionMapping" IS 'Revision word x original word, Greek and Hebrew in one table. Lang G joins StepGreekWords, Lang H joins StepHebrewWords; both id ranges start at 1, so every join needs Lang.';
COMMENT ON COLUMN "unshackled-word"."RevisionMapping"."Lang" IS 'G for books 40-66, H for books 1-39 (Hebrew and Aramaic). Set by the service from the verse, never by the client.';
COMMENT ON COLUMN "unshackled-word"."RevisionMapping"."Origin" IS 'carried = copied from the parent text by the carry-over algorithm and not confirmed, manual = set in the mapping form, seed = imported from the repo seed.';
