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
    "Id"                               SERIAL PRIMARY KEY,
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

CREATE TABLE "unshackled-word"."StepHebrewWordsNormalized"
(
    "Id"                        SERIAL PRIMARY KEY,
    "IsRoot"                    BOOLEAN     NOT NULL,
    "Grammar"                   VARCHAR(30),
    "SuffixCode"                VARCHAR(30),
    "Hebrew"                    VARCHAR(40) COLLATE "und-x-icu",
    "StrongsNumber"             VARCHAR(20) NOT NULL
);

CREATE TABLE "unshackled-word"."StepHebrewWordsNormalizedToHebrewWords"
(
    "StepHebrewWordsId"              INTEGER     NOT NULL,
    "StepHebrewWordsNormalizedId"    INTEGER     NOT NULL,
    "PositionInWord"                 INTEGER     NOT NULL,

    PRIMARY KEY ("StepHebrewWordsId", "StepHebrewWordsNormalizedId"),

    FOREIGN KEY ("StepHebrewWordsId")
        REFERENCES "unshackled-word"."StepHebrewWords" ("Id"),
    FOREIGN KEY ("StepHebrewWordsNormalizedId")
        REFERENCES "unshackled-word"."StepHebrewWordsNormalized" ("Id")
);

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
    "Id"           INTEGER PRIMARY KEY,
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


CREATE TABLE "unshackled-word"."StepGreekMorphology"
(
    "Id"           INTEGER PRIMARY KEY,
    "Code"         VARCHAR(50)  NOT NULL COLLATE "und-x-icu",
    "PartOfSpeech" VARCHAR(100) NOT NULL COLLATE "und-x-icu",
    "Tense"        VARCHAR(50) COLLATE "und-x-icu",
    "Voice"        VARCHAR(50) COLLATE "und-x-icu",
    "Mood"         VARCHAR(50) COLLATE "und-x-icu",
    "Person"       VARCHAR(20) COLLATE "und-x-icu",
    "Number"       VARCHAR(20) COLLATE "und-x-icu",
    "Case"         VARCHAR(20) COLLATE "und-x-icu",
    "Gender"       VARCHAR(20) COLLATE "und-x-icu",
    "Degree"       VARCHAR(50) COLLATE "und-x-icu",
    "Extras"       VARCHAR(255) COLLATE "und-x-icu",
    "NameType"     VARCHAR(100) COLLATE "und-x-icu"
);

CREATE INDEX "IX_StepGreekMorphology_Code"
    ON "unshackled-word"."StepGreekMorphology" ("Code");


CREATE TABLE "unshackled-word"."StepStrongsToVerses"
(
    "Id"                        SERIAL PRIMARY KEY,
    "BibleBookId"               INTEGER     NOT NULL,
    "Chapter"                   INTEGER     NOT NULL,
    "Verse"                     INTEGER     NOT NULL,
    "IsRoot"                    BOOLEAN     NOT NULL,
    "Grammar"                   VARCHAR(30),
    "Hebrew"                    VARCHAR(40) COLLATE "und-x-icu",
    "Gloss"                     VARCHAR(120),
    "FirstOccuranceBibleBookId" INTEGER,
    "FirstOccuranceChapter"     INTEGER,
    "FirstOccuranceVerse"       INTEGER,
    "LastOccuranceBibleBookId"  INTEGER,
    "LastOccuranceChapter"      INTEGER,
    "LastOccuranceVerse"        INTEGER,
    "StrongsNumber"             VARCHAR(20) NOT NULL,

    -- Unique index on the specified columns
    CONSTRAINT "UQ_StepStrongsToVerses_Book_Ch_Vs_Strong"
        UNIQUE ("BibleBookId", "Chapter", "Verse", "StrongsNumber")
);

CREATE TABLE "unshackled-word"."StepOtherLexicon"
(
    "Id"               SERIAL PRIMARY KEY,
    "Name"             varchar(255)  NOT NULL,
    "BibleBookId"      integer       NOT NULL,
    "Chapter"          integer       NOT NULL,
    "Verse"            integer       NOT NULL,
    "Strongs"          varchar(20)   NOT NULL,
    "Note"             varchar(1000),
    "Type"             varchar(100),
    "OriginalSpelling" varchar(100)  COLLATE "und-x-icu",
    "StepBibleLink"    varchar(500)  NOT NULL,
    "Briefest"         varchar(255),
    "Brief"            varchar(500)  NOT NULL,
    "Short"            varchar(1000) NOT NULL,
    "Article"          text          NOT NULL
);

CREATE TABLE "unshackled-word"."StepPersonLexicon"
(
    "Id"               SERIAL PRIMARY KEY,
    "Name"             varchar(255) NOT NULL,
    "BibleBookId"      integer      NOT NULL,
    "Chapter"          integer      NOT NULL,
    "Verse"            integer      NOT NULL,
    "Strongs"          varchar(20),
    "Note"             varchar(2000),
    "OriginalSpelling" varchar(100) COLLATE "und-x-icu",
    "Tribe"            varchar(100),
    "Gender"           varchar(20),
    "Briefest"         varchar(255),
    "Brief"            varchar(500),
    "Short"            varchar(1000),
    "Article"          text
);

CREATE TABLE "unshackled-word"."StepPersonLexiconRelations"
(
    "Id"              SERIAL PRIMARY KEY,
    "PersonLexiconId" integer      NOT NULL,
    "Name"            varchar(255) NOT NULL,
    "BibleBookId"     integer      NOT NULL,
    "Chapter"         integer      NOT NULL,
    "Verse"           integer      NOT NULL,
    "Strongs"         varchar(20),
    "RelationType"    varchar(100) NOT NULL
);

CREATE TABLE "unshackled-word"."StepPlaceLexicon"
(
    "Id"               integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    "Name"             varchar(255)  NOT NULL,
    "BibleBookId"      integer       NOT NULL,
    "Chapter"          integer       NOT NULL,
    "Verse"            integer       NOT NULL,
    "Strongs"          varchar(20)   NOT NULL,
    "Note"             varchar(2000),
    "Type"             varchar(100),
    "GoogleMapsLinks"  varchar(1000),
    "PalOpenMapsLink"  varchar(1000),
    "OriginalSpelling" varchar(100) COLLATE "und-x-icu",
    "StepBibleLink"    varchar(1000) NOT NULL,
    "Briefest"         varchar(255),
    "Brief"            varchar(500),
    "Short"            varchar(1000) NOT NULL,
    "Article"          text          NOT NULL
);

CREATE TABLE "unshackled-word"."Elb1871GreekMapping"
(
    "Id"                 SERIAL PRIMARY KEY,
    "ElbWordId"          INT NOT NULL UNIQUE,
    "StepGreekId"        INT NULL,
    "BookId"             INT NOT NULL,
    "Chapter"            INT NOT NULL,
    "Verse"              INT NOT NULL,
    "StrongsNumber"      VARCHAR(10) NULL,
    "IsAddedWord"        BOOLEAN DEFAULT FALSE,
    "ParentGermanWordId" INT NULL,
    "WordOrderInVerse"   INT NOT NULL,
    "GermanWordPart"     varchar(30) NULL,
    -- Composite Unique Key for ElbWordId and StepGreekId
    -- Note: NULLS NOT DISTINCT requires PostgreSQL 15+
    -- when there is one ElbWordId with multiple StepGreekId then each StepGreekId must have a unique GermanWordPart
    CONSTRAINT "UqElbStepGreek" UNIQUE NULLS NOT DISTINCT ("ElbWordId", "StepGreekId")
);

CREATE INDEX "IdxElbGreekWordId" ON "unshackled-word"."Elb1871GreekMapping" ("ElbWordId");
CREATE INDEX "IdxElbGreekVerse" ON "unshackled-word"."Elb1871GreekMapping" ("BookId", "Chapter", "Verse");
CREATE INDEX "IdxElbGreekStrongs" ON "unshackled-word"."Elb1871GreekMapping" ("StrongsNumber");


CREATE TABLE "unshackled-word"."Elb1871HebrewMapping"
(
    "Id"                            SERIAL PRIMARY KEY,
    "ElbWordId"                     INT NOT NULL UNIQUE,
    "StepHebrewNormalizedId"        INT NULL,
    "BookId"                        INT NOT NULL,
    "Chapter"                       INT NOT NULL,
    "Verse"                         INT NOT NULL,
    "StrongsNumber"                 VARCHAR(10) NULL,
    "IsAddedWord"                   BOOLEAN DEFAULT FALSE,
    "ParentGermanWordId"            INT NULL,
    "WordOrderInVerse"              INT NOT NULL,
    "GermanWordPart"                varchar(30) NULL,
    -- Composite Unique Key for ElbWordId and StepGreekId
    -- Note: NULLS NOT DISTINCT requires PostgreSQL 15+
    -- when there is one ElbWordId with multiple StepHebrewNormalizedIds then each StepHebrewNormalizedId must have a unique GermanWordPart
    CONSTRAINT "UqElbStepHebrew" UNIQUE NULLS NOT DISTINCT ("ElbWordId", "StepHebrewNormalizedId")
);

CREATE INDEX "IdxElbHebrewWordId" ON "unshackled-word"."Elb1871HebrewMapping" ("ElbWordId");
CREATE INDEX "IdxElbHebrewVerse" ON "unshackled-word"."Elb1871HebrewMapping" ("BookId", "Chapter", "Verse");
CREATE INDEX "IdxElbHebrewStrongs" ON "unshackled-word"."Elb1871HebrewMapping" ("StrongsNumber");
