CREATE TABLE "unshackled-word"."StepGreekWords"
(
    "Id"                   SERIAL PRIMARY KEY,
    "BibleBookId"          INTEGER                          NOT NULL,
    "Chapter"              INTEGER                          NOT NULL,
    "Verse"                INTEGER                          NOT NULL,
    "PositionInVerse"      INTEGER                          NOT NULL,
    "AltChapter"           INTEGER,
    "AltVerse"             INTEGER,
    "Type"                 VARCHAR(50) COLLATE "und-x-icu"  NOT NULL,
    "IsInNestleAland"      BOOLEAN                          NOT NULL,
    "IsInTextusReceptus"   BOOLEAN                          NOT NULL,
    "IsInOther"            BOOLEAN                          NOT NULL,
    "Greek"                VARCHAR(100) COLLATE "und-x-icu" NOT NULL,
    "GreekNoDiacritics"    VARCHAR(100) COLLATE "und-x-icu" NOT NULL,
    "Transliteration"      VARCHAR(150) COLLATE "und-x-icu" NOT NULL,
    "English"              TEXT                             NOT NULL,
    "German"               TEXT,
    "Spanish"              TEXT,
    "DisambiguatedStrongs" VARCHAR(50) COLLATE "und-x-icu"  NOT NULL,
    "Morphology"           VARCHAR(50) COLLATE "und-x-icu"  NOT NULL,
    "Lemma"                VARCHAR(100) COLLATE "und-x-icu" NOT NULL,
    "LemmaNoDiacritics"    VARCHAR(100) COLLATE "und-x-icu" NOT NULL,
    "Gloss"                TEXT                             NOT NULL,
    "Editions"             VARCHAR(100) COLLATE "und-x-icu" NOT NULL,
    "MeaningVariants"      TEXT,
    "SpellingVariants"     TEXT,
    "SubMeaning"           TEXT,
    "ConjoinWord"          VARCHAR(100) COLLATE "und-x-icu",
    "StrongInstance"       VARCHAR(50) COLLATE "und-x-icu",
    "AltStrongs"           VARCHAR(100) COLLATE "und-x-icu"
);

-- Index for rapid scripture lookups (Book, Chapter, Verse)
CREATE INDEX "IX_StepGreekWords_Book_Chapter_Verse"
    ON "unshackled-word"."StepGreekWords" ("BibleBookId", "Chapter", "Verse");

-- Index for word-based searches (Greek text)
CREATE INDEX "IX_StepGreekWords_GreekNoDiacritics"
    ON "unshackled-word"."StepGreekWords" ("GreekNoDiacritics");

CREATE TABLE "unshackled-word"."StepHebrewWords"
(
    "Id"                               INTEGER PRIMARY KEY,
    "BibleBookId"                      INTEGER                          NOT NULL,
    "Chapter"                          INTEGER                          NOT NULL,
    "Verse"                            INTEGER                          NOT NULL,
    "PositionInVerse"                  INTEGER                          NOT NULL,
    "AltChapter"                       INTEGER,
    "AltVerse"                         INTEGER,
    "Type"                             VARCHAR(50) COLLATE "und-x-icu"  NOT NULL,
    "HebrewNormalised"                 VARCHAR(100) COLLATE "und-x-icu" NOT NULL,
    "Hebrew"                           VARCHAR(100) COLLATE "und-x-icu" NOT NULL,
    "HebrewNoDiacritics"               VARCHAR(100) COLLATE "und-x-icu" NOT NULL,
    "Transliteration"                  VARCHAR(200) COLLATE "und-x-icu" NOT NULL,
    "Gloss"                            VARCHAR(500) COLLATE "und-x-icu" NOT NULL,
    "DisambiguatedStrongs"             VARCHAR(50) COLLATE "und-x-icu"  NOT NULL,
    "Grammar"                          VARCHAR(100) COLLATE "und-x-icu" NOT NULL,
    "MeaningVariants"                  VARCHAR(1000) COLLATE "und-x-icu",
    "SpellingVariants"                 VARCHAR(500) COLLATE "und-x-icu",
    "RootDisambiguatedStrongsInstance" VARCHAR(50) COLLATE "und-x-icu",
    "AlternativeStrongs"               VARCHAR(100) COLLATE "und-x-icu",
    "ConjoinWord"                      VARCHAR(100) COLLATE "und-x-icu",
    "ExpandedStrongTags"               VARCHAR(500) COLLATE "und-x-icu"
);

-- Index for rapid scripture lookups (Book, Chapter, Verse)
CREATE INDEX "IX_StepHebrewWords_Book_Chapter_Verse"
    ON "unshackled-word"."StepHebrewWords" ("BibleBookId", "Chapter", "Verse");

-- Index for word-based searches (Hebrew text)
CREATE INDEX "IX_StepHebrewWords_HebrewNoDiacritics"
    ON "unshackled-word"."StepHebrewWords" ("HebrewNoDiacritics");


CREATE TABLE "unshackled-word"."StepStrongs"
(
    "Id"                       SERIAL PRIMARY KEY,
    "ExtendedStrongs"          VARCHAR(10) COLLATE "und-x-icu" NOT NULL,
    "DisambiguatedStrongs"     VARCHAR(30) COLLATE "und-x-icu" NOT NULL,
    "UnifiedStrongs"           VARCHAR(60) COLLATE "und-x-icu" NOT NULL,
    "OriginalWord"             VARCHAR(50) COLLATE "und-x-icu" NOT NULL,
    "OriginalWordNoDiacritics" VARCHAR(50) COLLATE "und-x-icu" NOT NULL,
    "Transliteration"          VARCHAR(50) COLLATE "und-x-icu" NOT NULL,
    "Morphology"               VARCHAR(65) COLLATE "und-x-icu" NOT NULL,
    "Gloss"                    VARCHAR(65) COLLATE "und-x-icu" NOT NULL,
    "Lexicon"                  TEXT COLLATE "und-x-icu",
);

-- Index for Strongs lookups (high selectivity, frequently used for joins/filtering)
CREATE INDEX "IX_StepStrongs_DisambiguatedStrongs"
    ON "unshackled-word"."StepStrongs" ("DisambiguatedStrongs");

-- Index for searching by Morphology codes (useful for grammatical analysis queries)
CREATE INDEX "IX_StepStrongs_Morphology"
    ON "unshackled-word"."StepStrongs" ("Morphology");

-- Index for Gloss (useful for prefix searches or exact matches of English meanings)
-- Note: If you plan to do full-text search on long Glosses, consider a GIN index instead.
CREATE INDEX "IX_StepStrongs_Gloss"
    ON "unshackled-word"."StepStrongs" ("Gloss");



CREATE TABLE "unshackled-word"."StepHebrewMorphology"
(
    "Id"           INTEGER PRIMARY KEY GENERATED BY DEFAULT AS IDENTITY,
    "Code"         VARCHAR(50) COLLATE "und-x-icu"  NOT NULL,
    "PartOfSpeech" VARCHAR(100) COLLATE "und-x-icu" NOT NULL,
    "Form"         VARCHAR(100) COLLATE "und-x-icu",
    "Tense"        VARCHAR(50) COLLATE "und-x-icu",
    "Mood"         VARCHAR(50) COLLATE "und-x-icu",
    "Person"       VARCHAR(50) COLLATE "und-x-icu",
    "Number"       VARCHAR(50) COLLATE "und-x-icu",
    "Gender"       VARCHAR(50) COLLATE "und-x-icu",
    "State"        VARCHAR(50) COLLATE "und-x-icu",
    "Stem"         VARCHAR(50) COLLATE "und-x-icu",
    "Action"       VARCHAR(50) COLLATE "und-x-icu",
    "Voice"        VARCHAR(50) COLLATE "und-x-icu"
);

CREATE INDEX "IX_StepHebrewMorphology_Code"
    ON "unshackled-word"."StepHebrewMorphology" ("Code");
