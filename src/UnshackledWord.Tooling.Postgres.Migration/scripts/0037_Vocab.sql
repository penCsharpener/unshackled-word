-- Vocab trainer, content side (roadmap 0022.1, docs/roadmaps/0022-vocab-trainer/03-vocab-model.md).
--   VocabCourses       the two system courses (frequency order over one track); curated courses come later
--   VocabEntries       one lemma to learn (Greek, or Hebrew + Aramaic); several Strong's keys may share it (H1254A/H1254B -> בָּרָא)
--   VocabStrongs       Strong's key -> entry (a key belongs to exactly one entry)
--   VocabMeanings      German meanings of an entry, derived from "StatsVocabRenderings" (0038) or written by the maintainer
--   VocabEntryChanges  append-only log of learning-relevant changes; what a learner with an older card sees as "geändert"
-- The lexicon (entries, keys, meanings) is repo seed data (assets/seed/vocab); learner progress (0039) never is.
-- User ids are local to an install: the seed import writes NULL into the *UserId columns.
--
-- Idempotent: safe to run again.

-- ---- VocabCourses -------------------------------------------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS "unshackled-word"."VocabCourses"
(
    "Id"     serial4      NOT NULL,
    "Code"   varchar(30)  NOT NULL,
    "Track"  char(1)      NOT NULL,
    "Kind"   varchar(10)  NOT NULL DEFAULT 'frequency',
    "NameDe" varchar(100) NOT NULL,
    "NameEn" varchar(100) NOT NULL,
    CONSTRAINT "VocabCourses_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "VocabCourses_Code_UQ" UNIQUE ("Code"),
    CONSTRAINT "VocabCourses_Track_CK" CHECK ("Track" IN ('G', 'H')),
    CONSTRAINT "VocabCourses_Kind_CK" CHECK ("Kind" IN ('frequency'))
);

INSERT INTO "unshackled-word"."VocabCourses" ("Code", "Track", "Kind", "NameDe", "NameEn")
VALUES ('nt-greek', 'G', 'frequency', 'Neues Testament (Griechisch)', 'New Testament (Greek)')
     , ('ot-hebrew', 'H', 'frequency', 'Altes Testament (Hebräisch und Aramäisch)', 'Old Testament (Hebrew and Aramaic)')
ON CONFLICT ("Code") DO NOTHING;

COMMENT ON TABLE "unshackled-word"."VocabCourses" IS 'Vocab courses. Kind frequency = every entry of the track that has a FrequencyRank, in rank order.';
COMMENT ON COLUMN "unshackled-word"."VocabCourses"."Track" IS 'G = Greek (NT), H = Hebrew including Aramaic (OT).';

-- ---- VocabEntries -------------------------------------------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS "unshackled-word"."VocabEntries"
(
    "Id"                   serial4                          NOT NULL,
    "Track"                char(1)                          NOT NULL,
    "LanguageId"           int4                             NOT NULL,
    "Lemma"                varchar(50) COLLATE "und-x-icu"  NOT NULL,
    "LemmaNormalized"      varchar(50) COLLATE "und-x-icu"  NOT NULL,
    "DictionaryForm"       varchar(120) COLLATE "und-x-icu" NULL,
    "DictionaryFormOrigin" varchar(10)                      NULL,
    "Transliteration"      varchar(50)                      NULL,
    "Pos"                  varchar(20)                      NULL,
    "IsProperName"         boolean                          NOT NULL DEFAULT false,
    "IsMorpheme"           boolean                          NOT NULL DEFAULT false,
    "Occurrences"          int4                             NOT NULL DEFAULT 0,
    "FrequencyRank"        int4                             NULL,
    "AudioObjectKey"       varchar(200)                     NULL,
    "ReviewedAt"           timestamptz                      NULL,
    "ReviewedByUserId"     int4                             NULL,
    "RetiredAt"            timestamptz                      NULL,
    "ContentVersion"       int4                             NOT NULL DEFAULT 1,
    "UpdatedAt"            timestamptz                      NOT NULL DEFAULT now(),
    "UpdatedByUserId"      int4                             NULL,
    CONSTRAINT "VocabEntries_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "VocabEntries_ReviewedByUser_FK" FOREIGN KEY ("ReviewedByUserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "VocabEntries_UpdatedByUser_FK" FOREIGN KEY ("UpdatedByUserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "VocabEntries_Track_CK" CHECK ("Track" IN ('G', 'H')),
    -- StepStrongsLexicon."LanguageId": Hebrew = 0, Aramaic = 1, Greek = 2; Aramaic belongs to track H
    CONSTRAINT "VocabEntries_Language_CK" CHECK (("Track" = 'G' AND "LanguageId" = 2) OR ("Track" = 'H' AND "LanguageId" IN (0, 1))),
    CONSTRAINT "VocabEntries_Lemma_CK" CHECK ("Lemma" <> '' AND "LemmaNormalized" <> ''),
    -- a form always says where it came from, an origin never stands alone
    CONSTRAINT "VocabEntries_DictionaryForm_CK" CHECK (("DictionaryForm" IS NULL) = ("DictionaryFormOrigin" IS NULL)),
    CONSTRAINT "VocabEntries_DictionaryFormOrigin_CK" CHECK ("DictionaryFormOrigin" IN ('generated', 'agent', 'manual')),
    CONSTRAINT "VocabEntries_Occurrences_CK" CHECK ("Occurrences" >= 0),
    -- morphemes (H9001–H9049) and retired entries stay out of the course order
    CONSTRAINT "VocabEntries_FrequencyRank_CK" CHECK ("FrequencyRank" IS NULL OR ("FrequencyRank" >= 1 AND NOT "IsMorpheme" AND "RetiredAt" IS NULL)),
    CONSTRAINT "VocabEntries_Reviewed_CK" CHECK ("ReviewedByUserId" IS NULL OR "ReviewedAt" IS NOT NULL),
    CONSTRAINT "VocabEntries_ContentVersion_CK" CHECK ("ContentVersion" >= 1),
    -- one rank per track; deferred so the generator can renumber all ranks in one UPDATE (rank swaps)
    CONSTRAINT "VocabEntries_TrackRank_UQ" UNIQUE ("Track", "FrequencyRank") DEFERRABLE INITIALLY DEFERRED
);

-- frequency list and "next new entry" read by (Track, FrequencyRank): served by the index of "VocabEntries_TrackRank_UQ"
CREATE INDEX IF NOT EXISTS "VocabEntries_LemmaNormalized_idx" ON "unshackled-word"."VocabEntries" ("Track", "LemmaNormalized" varchar_pattern_ops);

COMMENT ON TABLE "unshackled-word"."VocabEntries" IS 'One lemma to learn. Repo seed data (assets/seed/vocab), generated by VocabLexiconGenerator, edited by the maintainer.';
COMMENT ON COLUMN "unshackled-word"."VocabEntries"."Lemma" IS 'STEP OriginalWord as written (accents, niqqud).';
COMMENT ON COLUMN "unshackled-word"."VocabEntries"."LemmaNormalized" IS 'Search and typing key (VocabText.Normalize: NFD, combining marks dropped, final sigma folded, Greek lower-case).';
COMMENT ON COLUMN "unshackled-word"."VocabEntries"."DictionaryForm" IS 'Learning form such as μέσος, -η, -ον or λόγος, -ου, ὁ; the typing target when set, otherwise the lemma.';
COMMENT ON COLUMN "unshackled-word"."VocabEntries"."DictionaryFormOrigin" IS 'generated (code, STEP lexicon), agent (agent run) or manual (maintainer); a rerun of the generator never touches agent/manual.';
COMMENT ON COLUMN "unshackled-word"."VocabEntries"."IsMorpheme" IS 'Hebrew prefix/suffix morphemes H9001–H9049: listed, never ranked.';
COMMENT ON COLUMN "unshackled-word"."VocabEntries"."Occurrences" IS 'Original-text occurrences (STEP word rows), summed over the keys in VocabStrongs.';
COMMENT ON COLUMN "unshackled-word"."VocabEntries"."FrequencyRank" IS 'Course order per track (1 = most frequent); NULL for morphemes and retired entries. Recomputed by every generator run without a ContentVersion bump.';
COMMENT ON COLUMN "unshackled-word"."VocabEntries"."AudioObjectKey" IS 'Object key of the pronunciation in Garage (roadmap 0022.5).';
COMMENT ON COLUMN "unshackled-word"."VocabEntries"."ReviewedAt" IS 'Set when the maintainer checked the meanings; NULL = shown as "ungeprüft".';
COMMENT ON COLUMN "unshackled-word"."VocabEntries"."RetiredAt" IS 'Retired entries are not studied; cards on them are flagged as "entfernt".';
COMMENT ON COLUMN "unshackled-word"."VocabEntries"."ContentVersion" IS 'Bumped by every learning-relevant change (meanings, form, lemma, keys, merge, split, retire), each with a VocabEntryChanges row.';

-- ---- VocabStrongs -------------------------------------------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS "unshackled-word"."VocabStrongs"
(
    "Lang"         char(1)     NOT NULL,
    "StrongsKey"   varchar(10) NOT NULL,
    "VocabEntryId" int4        NOT NULL,
    "Occurrences"  int4        NOT NULL DEFAULT 0,
    CONSTRAINT "VocabStrongs_PK" PRIMARY KEY ("Lang", "StrongsKey"),
    -- no cascade: an entry can be deleted only after its keys were moved away (merge/split), see 04-study-engine.md
    CONSTRAINT "VocabStrongs_Entry_FK" FOREIGN KEY ("VocabEntryId") REFERENCES "unshackled-word"."VocabEntries" ("Id"),
    CONSTRAINT "VocabStrongs_Lang_CK" CHECK ("Lang" IN ('G', 'H')),
    CONSTRAINT "VocabStrongs_Key_CK" CHECK (left("StrongsKey", 1) = "Lang" AND "StrongsKey" ~ '^[GH]\d+[A-Za-z]?$'),
    CONSTRAINT "VocabStrongs_Occurrences_CK" CHECK ("Occurrences" >= 0)
);

CREATE INDEX IF NOT EXISTS "VocabStrongs_Entry_idx" ON "unshackled-word"."VocabStrongs" ("VocabEntryId");

COMMENT ON TABLE "unshackled-word"."VocabStrongs" IS 'Strong''s key -> vocab entry. A key belongs to exactly one entry; one entry may have several keys.';
COMMENT ON COLUMN "unshackled-word"."VocabStrongs"."StrongsKey" IS 'Same format as StatsStrongsRenderings/StatsVocabRenderings."StrongsKey" (STEP disambiguated: G3056, G0846, G3004G, H1254A).';
COMMENT ON COLUMN "unshackled-word"."VocabStrongs"."Occurrences" IS 'Original-text occurrences of this key (STEP word rows).';

-- ---- VocabMeanings ------------------------------------------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS "unshackled-word"."VocabMeanings"
(
    "Id"           serial4       NOT NULL,
    "VocabEntryId" int4          NOT NULL,
    "German"       varchar(80)   NOT NULL,
    "GermanKey"    varchar(80)   NULL,
    "Rank"         int4          NOT NULL,
    "Count"        int4          NULL,
    "Share"        numeric(5, 2) NULL,
    "IsAccepted"   boolean       NOT NULL DEFAULT true,
    "Origin"       varchar(10)   NOT NULL,
    "IsPinned"     boolean       NOT NULL DEFAULT false,
    CONSTRAINT "VocabMeanings_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "VocabMeanings_Entry_FK" FOREIGN KEY ("VocabEntryId") REFERENCES "unshackled-word"."VocabEntries" ("Id") ON DELETE CASCADE,
    CONSTRAINT "VocabMeanings_Entry_German_UQ" UNIQUE ("VocabEntryId", "German"),
    CONSTRAINT "VocabMeanings_German_CK" CHECK ("German" <> ''),
    CONSTRAINT "VocabMeanings_Rank_CK" CHECK ("Rank" >= 1),
    CONSTRAINT "VocabMeanings_Count_CK" CHECK ("Count" IS NULL OR "Count" >= 0),
    CONSTRAINT "VocabMeanings_Share_CK" CHECK ("Share" IS NULL OR "Share" BETWEEN 0 AND 100),
    CONSTRAINT "VocabMeanings_Origin_CK" CHECK ("Origin" IN ('derived', 'manual'))
);

CREATE INDEX IF NOT EXISTS "VocabMeanings_EntryRank_idx" ON "unshackled-word"."VocabMeanings" ("VocabEntryId", "Rank");

COMMENT ON TABLE "unshackled-word"."VocabMeanings" IS 'German meanings of a vocab entry. Every accepted meaning counts as a correct answer.';
COMMENT ON COLUMN "unshackled-word"."VocabMeanings"."GermanKey" IS 'StatsVocabRenderings."GermanKey" the meaning was derived from; NULL for manual meanings.';
COMMENT ON COLUMN "unshackled-word"."VocabMeanings"."Count" IS 'Head links with this rendering at derivation time.';
COMMENT ON COLUMN "unshackled-word"."VocabMeanings"."Share" IS 'Percent of the entry''s head links at derivation time.';
COMMENT ON COLUMN "unshackled-word"."VocabMeanings"."Origin" IS 'derived (meaning derivation, replaced on the next run unless pinned) or manual (maintainer).';
COMMENT ON COLUMN "unshackled-word"."VocabMeanings"."IsPinned" IS 'A pinned derived meaning survives re-derivation.';

-- ---- VocabEntryChanges --------------------------------------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS "unshackled-word"."VocabEntryChanges"
(
    "Id"              serial4     NOT NULL,
    "VocabEntryId"    int4        NOT NULL,
    "ContentVersion"  int4        NOT NULL,
    "Kind"            varchar(12) NOT NULL,
    "Before"          jsonb       NULL,
    "After"           jsonb       NULL,
    "TargetEntryId"   int4        NULL,
    "ChangedByUserId" int4        NULL,
    "ChangedAt"       timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "VocabEntryChanges_PK" PRIMARY KEY ("Id"),
    -- "VocabEntryId" and "TargetEntryId" have no FK on purpose: the log outlives a merged or deleted entry
    CONSTRAINT "VocabEntryChanges_ChangedByUser_FK" FOREIGN KEY ("ChangedByUserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "VocabEntryChanges_Kind_CK" CHECK ("Kind" IN ('meanings', 'form', 'lemma', 'keys', 'merge', 'split', 'retire', 'delete')),
    CONSTRAINT "VocabEntryChanges_ContentVersion_CK" CHECK ("ContentVersion" >= 1),
    CONSTRAINT "VocabEntryChanges_Target_CK" CHECK ("Kind" NOT IN ('merge', 'split') OR "TargetEntryId" IS NOT NULL)
);

CREATE INDEX IF NOT EXISTS "VocabEntryChanges_Entry_idx" ON "unshackled-word"."VocabEntryChanges" ("VocabEntryId", "ContentVersion");

COMMENT ON TABLE "unshackled-word"."VocabEntryChanges" IS 'Append-only. One row per learning-relevant change; a card with SeenContentVersion below ContentVersion shows these rows as before/after.';
COMMENT ON COLUMN "unshackled-word"."VocabEntryChanges"."ContentVersion" IS 'The entry version this change produced.';
COMMENT ON COLUMN "unshackled-word"."VocabEntryChanges"."Kind" IS 'meanings | form | lemma | keys | merge | split | retire | delete.';
COMMENT ON COLUMN "unshackled-word"."VocabEntryChanges"."TargetEntryId" IS 'merge: the entry this one was merged into; split: the entry the keys moved to.';
