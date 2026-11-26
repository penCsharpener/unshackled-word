-- Septuagint text: Swete's text in Rahlfs numbering (roadmap 0028.3, docs/roadmaps/0028-lxx-rahlfs/04-model.md).
--   LxxVerses  one row per mapped Swete unit (a verse, a marker segment, a word range or a join) of the 39 protocanonical books, Daniel twice
--              ("TextForm" og | th). Key = "RahlfsRefId" (book·10⁸ + chapter·10⁵ + verse·10² + subverse, TVTMS Greek column) per "TextForm";
--              "LxxRefId" (STEP, English counting) and "HebRefId" (Elberfelder) come from TVTMS. Reading order is Swete's order ("SortOrder"),
--              not "RahlfsRefId": Esther addition A (1:1.1-17) precedes 1:1. Additions have no "LxxRefId"/"HebRefId" and are shown after
--              "AfterHebRefId". "absent" rows (Swete text without Rahlfs counterpart, none in the
--              current data) have no "RahlfsRefId"; their Chapter/Verse are Swete's. Empty Swete verses are not imported.
--   LxxWords   the Greek words of a verse, positions 1..n after Swete's marker tokens, the editorial marks ⸂ ⸆ ⸃ and the acrostic letter names
--              of Lamentations are removed. "SrcHash" = SourceKey hash of the word, the anchor of tags in the repo seed.
-- Filled by SeedDb (LxxTextImporter) only while "LxxVerses" is empty. The Swete text is GPL-3.0 and downloaded, never part of the repo seed.
--
-- Idempotent: safe to run again.

CREATE TABLE IF NOT EXISTS "unshackled-word"."LxxVerses"
(
    "Id"            int4        GENERATED ALWAYS AS IDENTITY,
    "BibleBookId"   int4        NOT NULL,
    "TextForm"      varchar(4)  NOT NULL DEFAULT '',
    "Chapter"       int4        NOT NULL,
    "Verse"         int4        NOT NULL,
    "Subverse"      int4        NOT NULL DEFAULT 0,
    "RahlfsRefId"   int8        NULL,
    "SortOrder"     int4        NOT NULL,
    "SweteRef"      varchar(60) NOT NULL,
    "LxxRefId"      int4        NULL,
    "HebRefId"      int4        NULL,
    "AfterHebRefId" int4        NULL,
    "MapKind"       varchar(10) NOT NULL,
    CONSTRAINT "LxxVerses_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "LxxVerses_BibleBook_FK" FOREIGN KEY ("BibleBookId") REFERENCES "unshackled-word"."BibleBooks" ("Id"),
    CONSTRAINT "LxxVerses_Rahlfs_UQ" UNIQUE ("RahlfsRefId", "TextForm"),
    CONSTRAINT "LxxVerses_SortOrder_UQ" UNIQUE ("SortOrder"),
    CONSTRAINT "LxxVerses_BibleBook_CK" CHECK ("BibleBookId" BETWEEN 1 AND 39),
    CONSTRAINT "LxxVerses_TextForm_CK" CHECK ("TextForm" IN ('', 'og', 'th')),
    CONSTRAINT "LxxVerses_MapKind_CK" CHECK ("MapKind" IN ('one', 'merged', 'split', 'addition', 'absent')),
    CONSTRAINT "LxxVerses_RahlfsRefId_CK" CHECK ("RahlfsRefId" IS NOT NULL OR "MapKind" = 'absent'),
    CONSTRAINT "LxxVerses_Addition_CK" CHECK ("MapKind" NOT IN ('addition', 'absent') OR ("LxxRefId" IS NULL AND "HebRefId" IS NULL))
);

CREATE TABLE IF NOT EXISTS "unshackled-word"."LxxWords"
(
    "Id"                int4         GENERATED ALWAYS AS IDENTITY,
    "LxxVerseId"        int4         NOT NULL,
    "Position"          int4         NOT NULL,
    "Greek"             varchar(100) NOT NULL,
    "GreekNoDiacritics" varchar(100) NOT NULL,
    "SrcHash"           varchar(8)   NOT NULL,
    CONSTRAINT "LxxWords_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "LxxWords_LxxVerse_FK" FOREIGN KEY ("LxxVerseId") REFERENCES "unshackled-word"."LxxVerses" ("Id") ON DELETE CASCADE,
    CONSTRAINT "LxxWords_Position_UQ" UNIQUE ("LxxVerseId", "Position"),
    CONSTRAINT "LxxWords_Position_CK" CHECK ("Position" >= 1)
);

-- the reader (0028.4): LXX verses of an Elberfelder chapter, additions after their verse; Greek chapters for the Rahlfs label
CREATE INDEX IF NOT EXISTS "LxxVerses_HebRefId_idx" ON "unshackled-word"."LxxVerses" ("HebRefId") WHERE "HebRefId" IS NOT NULL;
CREATE INDEX IF NOT EXISTS "LxxVerses_AfterHebRefId_idx" ON "unshackled-word"."LxxVerses" ("AfterHebRefId") WHERE "AfterHebRefId" IS NOT NULL;
CREATE INDEX IF NOT EXISTS "LxxVerses_BookChapter_idx" ON "unshackled-word"."LxxVerses" ("BibleBookId", "Chapter");

COMMENT ON TABLE "unshackled-word"."LxxVerses" IS 'Septuagint verses: Swete''s text in Rahlfs numbering (roadmap 0028), 39 protocanonical books, Daniel as og and th. Filled by SeedDb only while empty; not part of the repo seed.';
COMMENT ON COLUMN "unshackled-word"."LxxVerses"."TextForm" IS 'empty = the only text; og = Daniel Old Greek, th = Daniel Theodotion.';
COMMENT ON COLUMN "unshackled-word"."LxxVerses"."Chapter" IS 'Rahlfs chapter (TVTMS Greek column); Swete''s chapter for absent rows.';
COMMENT ON COLUMN "unshackled-word"."LxxVerses"."Verse" IS 'Rahlfs verse; a Psalm title is verse 1 (Greek counting). Swete''s verse for absent rows.';
COMMENT ON COLUMN "unshackled-word"."LxxVerses"."Subverse" IS 'Rahlfs subverse (Esther A-F, 3 Kgdms 2:35a-o, Swete''s a, b, c pluses), 0 = the verse itself.';
COMMENT ON COLUMN "unshackled-word"."LxxVerses"."RahlfsRefId" IS 'book·10⁸ + chapter·10⁵ + verse·10² + subverse in the TVTMS Greek column; NULL only for absent rows without Rahlfs counterpart.';
COMMENT ON COLUMN "unshackled-word"."LxxVerses"."SortOrder" IS 'Reading order = Swete''s order (Esther addition A before 1:1, Prov 30:1-14 before 24:23); gaps where empty Swete verses were skipped.';
COMMENT ON COLUMN "unshackled-word"."LxxVerses"."SweteRef" IS 'Swete''s reference of the unit: Jer.32:1, Est.1:1[18], Gen.3:1#1-15; joined units as A + B.';
COMMENT ON COLUMN "unshackled-word"."LxxVerses"."LxxRefId" IS 'STEP key, English counting (book·10⁶ + chapter·10³ + verse; a Psalm title is verse 0). NULL for additions and absent rows.';
COMMENT ON COLUMN "unshackled-word"."LxxVerses"."HebRefId" IS 'Elberfelder verse (Hebrew counting, Elb1871Verses.HebRefId). NULL for additions and absent rows.';
COMMENT ON COLUMN "unshackled-word"."LxxVerses"."AfterHebRefId" IS 'Additions: the Elberfelder verse they are shown after.';
COMMENT ON COLUMN "unshackled-word"."LxxVerses"."MapKind" IS 'one | merged (covers several Hebrew verses, HebRefId = first) | split (several Greek verses share one Hebrew verse) | addition | absent.';
COMMENT ON TABLE "unshackled-word"."LxxWords" IS 'Greek words of a Septuagint verse (Swete, roadmap 0028), with punctuation; marker tokens, editorial marks and Lamentations'' acrostic letter names removed.';
COMMENT ON COLUMN "unshackled-word"."LxxWords"."Position" IS '1..n within the verse after filtering.';
COMMENT ON COLUMN "unshackled-word"."LxxWords"."GreekNoDiacritics" IS 'Greek without accents, breathings and punctuation marks (StringExtensions.RemoveGreekDiacritics, as StepGreekWords) and without Swete''s brackets for supplied text.';
COMMENT ON COLUMN "unshackled-word"."LxxWords"."SrcHash" IS 'SourceKey hash (8 hex digits) of the word: the anchor of tags and links in the repo seed, which never carries the Greek text.';
