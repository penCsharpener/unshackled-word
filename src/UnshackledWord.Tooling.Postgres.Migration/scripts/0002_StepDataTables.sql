CREATE TABLE "unshackled-word"."StepGreekWords"
(
    "Id"                   SERIAL PRIMARY KEY,
    "LxxRefId"             INTEGER                          NOT NULL,
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
    ON "unshackled-word"."StepGreekWords" ("LxxRefId");

-- Index for word-based searches (Greek text)
CREATE INDEX "IX_StepGreekWords_GreekNoDiacritics"
    ON "unshackled-word"."StepGreekWords" ("GreekNoDiacritics");

CREATE TABLE "unshackled-word"."StepHebrewWords"
(
    "Id"                               SERIAL PRIMARY KEY,
    "LxxRefId"                         INTEGER                          NOT NULL,
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
    ON "unshackled-word"."StepHebrewWords" ("LxxRefId");

-- Index for word-based searches (Hebrew text)
CREATE INDEX "IX_StepHebrewWords_HebrewNoDiacritics"
    ON "unshackled-word"."StepHebrewWords" ("HebrewNoDiacritics");

CREATE TABLE "unshackled-word"."StepHebrewWordsNormalized"
(
    "Id"               SERIAL PRIMARY KEY,
    "IsRoot"           BOOLEAN     NOT NULL,
    "Grammar"          VARCHAR(30),
    "SuffixCode"       VARCHAR(30),
    "Hebrew"           VARCHAR(40) COLLATE "und-x-icu",
    "StrongsNumber"    VARCHAR(20) NOT NULL
);

CREATE TABLE "unshackled-word"."StepHebrewWordsNormalizedToHebrewWords"
(
    "StepHebrewWordsId"              INTEGER     NOT NULL,
    "StepHebrewWordsNormalizedId"    INTEGER     NOT NULL,
    "PositionInWord"                 INTEGER     NOT NULL,

    PRIMARY KEY ("StepHebrewWordsId", "StepHebrewWordsNormalizedId"),

    CONSTRAINT "StepHebrewWordsNormalizedToHebrewWords_StepHebrewWordsId_fkey"
        FOREIGN KEY ("StepHebrewWordsId") REFERENCES "unshackled-word"."StepHebrewWords" ("Id"),
    CONSTRAINT "StepHebrewWordsNormalizedToHeb_StepHebrewWordsNormalizedId_fkey"
        FOREIGN KEY ("StepHebrewWordsNormalizedId") REFERENCES "unshackled-word"."StepHebrewWordsNormalized" ("Id")
);

CREATE TABLE "unshackled-word"."StepStrongsLexicon"
(
    "Id"                       SERIAL PRIMARY KEY,
    "LanguageId"               INTEGER                         NOT NULL,
    "Number"                   INTEGER                         NOT NULL,
    "Extra"                    VARCHAR(5),
    "DisambiguatedExtra"       VARCHAR(75),
    "OriginalWord"             VARCHAR(50) COLLATE "und-x-icu" NOT NULL,
    "OriginalWordNoDiacritics" VARCHAR(50) COLLATE "und-x-icu" NOT NULL,
    "Transliteration"          VARCHAR(50) COLLATE "und-x-icu" NOT NULL,
    "Morphology"               VARCHAR(65) COLLATE "und-x-icu" NOT NULL,
    "Gloss"                    VARCHAR(65) COLLATE "und-x-icu" NOT NULL,
    "Lexicon"                  TEXT COLLATE "und-x-icu"
);

-- Index for searching by Morphology codes (useful for grammatical analysis queries)
CREATE INDEX "IX_StepStrongsLexicon_Morphology" ON "unshackled-word"."StepStrongsLexicon" ("Morphology");

-- Index for Gloss (useful for prefix searches or exact matches of English meanings)
-- Note: If you plan to do full-text search on long Glosses, consider a GIN index instead.
CREATE INDEX "IX_StepStrongsLexicon_Gloss" ON "unshackled-word"."StepStrongsLexicon" ("Gloss");


CREATE TABLE "unshackled-word"."StepUnifiedStrongs"
(
    "Id"                   SERIAL PRIMARY KEY,
    "StepStrongsLexiconId" INT NOT NULL,
    "LanguageId"           INT NOT NULL,
    "Number"               INT NOT NULL,
    "Extra"                VARCHAR(5)
);

CREATE TABLE "unshackled-word"."StepStrongsToText"
(
    "Id"               SERIAL PRIMARY KEY,
    "LanguageId"       INT     NOT NULL,
    "Number"           INT     NOT NULL,
    "Extra"            VARCHAR(5),
    "IsRoot"           BOOLEAN NOT NULL,
    "CoversNextWord"   BOOLEAN NOT NULL,
    "StepGreekWordId"  INT,
    "StepHebrewWordId" INT,
    "Order"            INT     NOT NULL
);


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



CREATE TABLE "unshackled-word"."StepOtherLexicon"
(
    "Id"               SERIAL PRIMARY KEY,
    "Name"             varchar(255)  NOT NULL,
    "LxxRefId"         integer       NOT NULL,
    "Strongs"          varchar(20)   NOT NULL,
    "Note"             varchar(1000),
    "Type"             varchar(100),
    "OriginalSpelling" varchar(100) COLLATE "und-x-icu",
    "StepBibleLink"    varchar(500)  NOT NULL,
    "Briefest"         varchar(255),
    "Brief"            varchar(500)  NOT NULL,
    "Short"            varchar(1000) NOT NULL,
    "Article"          text          NOT NULL
);

CREATE INDEX "IdxStepOtherLexiconRefId" ON "unshackled-word"."StepOtherLexicon" ("LxxRefId");

CREATE TABLE "unshackled-word"."StepPersonLexicon"
(
    "Id"               SERIAL PRIMARY KEY,
    "Name"             varchar(255) NOT NULL,
    "LxxRefId"         integer      NOT NULL,
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

CREATE INDEX "IdxStepPersonLexiconLxxRefId" ON "unshackled-word"."StepPersonLexicon" ("LxxRefId");

CREATE TABLE "unshackled-word"."StepPersonLexiconRelations"
(
    "Id"              SERIAL PRIMARY KEY,
    "PersonLexiconId" integer      NOT NULL,
    "Name"            varchar(255) NOT NULL,
    "LxxRefId"        INTEGER       NOT NULL,
    "Strongs"         varchar(20),
    "RelationType"    varchar(100) NOT NULL
);

CREATE INDEX "IdxStepPersonLexiconRelationsLxxRefId" ON "unshackled-word"."StepPersonLexiconRelations" ("LxxRefId");

CREATE TABLE "unshackled-word"."StepPlaceLexicon"
(
    "Id"               integer GENERATED BY DEFAULT AS IDENTITY PRIMARY KEY,
    "Name"             varchar(255)  NOT NULL,
    "LxxRefId"         INTEGER       NOT NULL,
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

CREATE INDEX "IdxStepPlaceLexiconLxxRefId" ON "unshackled-word"."StepPlaceLexicon" ("LxxRefId");


CREATE TABLE "unshackled-word"."Elb1871GreekMapping"
(
    "Id"                 SERIAL PRIMARY KEY,
    "ElbWordId"          INT     NOT NULL UNIQUE,
    "StepWordId"         INT     NULL,
    "HebRefId"           INT     NOT NULL,
    "StrongsNumber"      VARCHAR(10) NULL,
    "IsAddedWord"        BOOLEAN DEFAULT FALSE,
    "ParentGermanWordId" INT     NULL,
    "PositionInVerse"    INT     NOT NULL,
    "GermanWordPart"     varchar(30) NULL,
    -- Composite Unique Key for ElbWordId and StepWordId
    -- Note: NULLS NOT DISTINCT requires PostgreSQL 15+
    -- when there is one ElbWordId with multiple StepWordId then each StepWordId must have a unique GermanWordPart
    CONSTRAINT "UqElbStepGreekMapping" UNIQUE NULLS NOT DISTINCT ("ElbWordId", "StepWordId")
);

CREATE INDEX "IdxElbGreekMappingWordId" ON "unshackled-word"."Elb1871GreekMapping" ("ElbWordId");
CREATE INDEX "IdxElbGreekMappingHebRefId" ON "unshackled-word"."Elb1871GreekMapping" ("HebRefId");
CREATE INDEX "IdxElbGreekMappingStepWordId" ON "unshackled-word"."Elb1871GreekMapping" ("StepWordId");
CREATE INDEX "IdxElbGreekMappingStrongs" ON "unshackled-word"."Elb1871GreekMapping" ("StrongsNumber");


CREATE TABLE "unshackled-word"."Elb1871HebrewMapping"
(
    "Id"                        SERIAL PRIMARY KEY,
    "ElbWordId"                 INT NOT NULL UNIQUE,
    "StepWordId"                INT NULL,
    "HebRefId"                  INT NOT NULL,
    "IsAddedWord"               BOOLEAN DEFAULT FALSE,
    "ParentGermanWordId"        INT NULL,
    "PositionInVerse"           INT NOT NULL,
    "GermanWordPart"            varchar(30) NULL,
    -- when there is one ElbWordId with multiple StepWordId then each StepWordId must have a unique GermanWordPart
    CONSTRAINT "UqElbStepHebrew" UNIQUE NULLS NOT DISTINCT ("ElbWordId", "StepWordId")
);

CREATE INDEX "IdxElbHebrewMappingWordId" ON "unshackled-word"."Elb1871HebrewMapping" ("ElbWordId");
CREATE INDEX "IdxElbHebrewMappingHebRefId" ON "unshackled-word"."Elb1871HebrewMapping" ("HebRefId");
CREATE INDEX "IdxElbHebrewMappingHebStepWordId" ON "unshackled-word"."Elb1871HebrewMapping" ("StepWordId");


CREATE TABLE "unshackled-word"."StrongsNumbers"
(
    "Id"               SERIAL PRIMARY KEY,
    "LanguageId"       INTEGER NOT NULL, -- Enum: Hebrew=0, Aramaic=1, Greek=2
    "Number"           INTEGER NOT NULL,
    "Extra"            VARCHAR(1),
    "IsRoot"           BOOLEAN NOT NULL DEFAULT FALSE,
    "CoversNextWord"   BOOLEAN NOT NULL DEFAULT FALSE,
    "StepHebrewWordId" INTEGER,
    "StepGreekWordId"  INTEGER,
    "Order"            INTEGER NOT NULL
);

CREATE INDEX "IX_StrongsNumbers_LanguageId_Number" ON "unshackled-word"."StrongsNumbers" ("LanguageId", "Number");
