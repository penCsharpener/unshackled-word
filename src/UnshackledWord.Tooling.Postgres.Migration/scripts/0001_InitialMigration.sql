CREATE SCHEMA IF NOT EXISTS "unshackled-word" AUTHORIZATION postgres;

CREATE TABLE "unshackled-word"."GntHotWords"
(
    "Id"              serial4                      NOT NULL,
    "BibleBookId"     integer                      NOT NULL,
    "Chapter"         integer                      NOT NULL,
    "Verse"           integer                      NOT NULL,
    "WordInContext"   varchar COLLATE "en_US.utf8" NOT NULL,
    "Koine"           varchar COLLATE "en_US.utf8" NOT NULL,
    "Lemma"           varchar COLLATE "en_US.utf8" NOT NULL,
    "PositionInVerse" integer                      NOT NULL,
    "Strongs"         varchar                      NOT NULL,
    "PartOfSpeech"    varchar                      NOT NULL,
    "GrammaticalKey"  varchar                      NOT NULL,
    CONSTRAINT "GntHotWords_pk" PRIMARY KEY ("Id")
);

CREATE INDEX "GntHotWords_reference_idx" ON "unshackled-word"."GntHotWords" ("BibleBookId", "Chapter", "Verse");
CREATE INDEX "GntHotWords_strongs_idx" ON "unshackled-word"."GntHotWords" ("Strongs");

CREATE TABLE "unshackled-word"."Elb1871Words"
(
    "Id"              serial4                      NOT NULL,
    "BibleBookId"     integer                      NOT NULL,
    "Chapter"         integer                      NOT NULL,
    "Verse"           integer                      NOT NULL,
    "WordInContext"   varchar COLLATE "en_US.utf8" NOT NULL,
    "PlainWord"       varchar COLLATE "en_US.utf8" NULL,
    "Lemma"           varchar COLLATE "en_US.utf8" NULL,
    "PositionInVerse" integer                      NOT NULL,
    "Strongs"         varchar                      NULL,
    "PartOfSpeech"    varchar                      NULL,
    "GrammaticalKey"  varchar                      NULL,
    CONSTRAINT "Elb1871Words_pk" PRIMARY KEY ("Id")
);

COMMENT ON COLUMN "unshackled-word"."Elb1871Words"."PlainWord" IS 'Is the same as WordInContext but clean of all special characters.';

CREATE INDEX "Elb1871Words_reference_idx" ON "unshackled-word"."Elb1871Words" ("BibleBookId", "Chapter", "Verse");
CREATE INDEX "Elb1871Words_strongs_idx" ON "unshackled-word"."Elb1871Words" ("Strongs");

CREATE TABLE "unshackled-word"."Elb1871Verses"
(
    "Id"          serial4                   NOT NULL,
    "BibleBookId" integer                   NOT NULL,
    "Chapter"     integer                   NOT NULL,
    "Verse"       integer                   NOT NULL,
    "VerseText"   text COLLATE "en_US.utf8" NOT NULL,
    CONSTRAINT "Elb1871Verses_pk" PRIMARY KEY ("Id")
);

CREATE INDEX "Elb1871Verses_reference_idx" ON "unshackled-word"."Elb1871Verses" ("BibleBookId", "Chapter", "Verse");

CREATE TABLE "unshackled-word"."SourceWords"
(
    "Id"          serial4                   NOT NULL,
    "BibleBookId" integer                   NOT NULL,
    "Chapter"     integer                   NOT NULL,
    "Verse"       integer                   NOT NULL,
    "SortNumber"  integer                   NOT NULL,
    "SourceWord"  text COLLATE "en_US.utf8" NOT NULL,
    "GrammarKey"  text COLLATE "en_US.utf8" NOT NULL,
    CONSTRAINT "SourceWords_pk" PRIMARY KEY ("Id")
);

CREATE INDEX "SourceWords_reference_idx" ON "unshackled-word"."SourceWords" ("BibleBookId", "Chapter", "Verse");

CREATE TABLE "unshackled-word"."Tsk"
(
    "Id"                      serial4                   NOT NULL,
    "BibleBookId"             integer                   NOT NULL,
    "Chapter"                 integer                   NOT NULL,
    "Verse"                   integer                   NOT NULL,
    "Scope"                   text COLLATE "en_US.utf8" NOT NULL,
    "RelatedStartBibleBookId" integer                   NOT NULL,
    "RelatedStartChapter"     integer                   NOT NULL,
    "RelatedStartVerse"       integer                   NOT NULL,
    "RelatedEndBibleBookId"   integer                   NULL,
    "RelatedEndChapter"       integer                   NULL,
    "RelatedEndVerse"         integer                   NULL,
    CONSTRAINT "Tsk_pk" PRIMARY KEY ("Id")
);

CREATE INDEX "Tsk_reference_idx" ON "unshackled-word"."Tsk" ("BibleBookId", "Chapter", "Verse");
CREATE INDEX "Tsk_relatedStartReference_idx" ON "unshackled-word"."Tsk" ("RelatedStartBibleBookId", "RelatedStartChapter", "RelatedStartVerse");
CREATE INDEX "Tsk_relatedEndReference_idx" ON "unshackled-word"."Tsk" ("RelatedEndBibleBookId", "RelatedEndChapter", "RelatedEndVerse");

CREATE TABLE "unshackled-word"."SblText"
(
    "Id"          serial4                   NOT NULL,
    "BibleBookId" integer                   NOT NULL,
    "Chapter"     integer                   NOT NULL,
    "Verse"       integer                   NOT NULL,
    "VerseText"   text COLLATE "en_US.utf8" NOT NULL,
    CONSTRAINT "SblText_pk" PRIMARY KEY ("Id")
);

CREATE INDEX "SblText_reference_idx" ON "unshackled-word"."SblText" ("BibleBookId", "Chapter", "Verse");

CREATE TABLE "unshackled-word"."SblApparatus"
(
    "Id"          serial4                   NOT NULL,
    "BibleBookId" integer                   NOT NULL,
    "Chapter"     integer                   NOT NULL,
    "Verse"       integer                   NOT NULL,
    "Text"        text COLLATE "en_US.utf8" NOT NULL,
    CONSTRAINT "SblApparatus_pk" PRIMARY KEY ("Id")
);

CREATE INDEX "SblApparatus_reference_idx" ON "unshackled-word"."SblApparatus" ("BibleBookId", "Chapter", "Verse");

CREATE TABLE "unshackled-word"."ByzTxtWords"
(
    "Id"           serial4                   NOT NULL,
    "BibleBookId"  integer                   NOT NULL,
    "Chapter"      integer                   NOT NULL,
    "Verse"        integer                   NOT NULL,
    "SortNumber"   integer                   NOT NULL,
    "Word"         text COLLATE "en_US.utf8" NOT NULL,
    "StrongNumber" text COLLATE "en_US.utf8" NOT NULL,
    "Morphology"   text COLLATE "en_US.utf8" NOT NULL,
    CONSTRAINT "ByzTxtWords_pk" PRIMARY KEY ("Id")
);

CREATE INDEX "ByzTxtWords_reference_idx" ON "unshackled-word"."ByzTxtWords" ("BibleBookId", "Chapter", "Verse");

CREATE TABLE "unshackled-word"."SrGntWords"
(
    "Id"              serial4                      NOT NULL,
    "BibleBookId"     integer                      NOT NULL,
    "Chapter"         integer                      NOT NULL,
    "Verse"           integer                      NOT NULL,
    "WordInContext"   varchar COLLATE "en_US.utf8" NOT NULL,
    "Koine"           varchar COLLATE "en_US.utf8" NOT NULL,
    "Lemma"           varchar COLLATE "en_US.utf8" NOT NULL,
    "PositionInVerse" integer                      NOT NULL,
    "Strongs"         varchar                      NOT NULL,
    "PartOfSpeech"    varchar                      NOT NULL,
    "GrammaticalKey"  varchar                      NOT NULL,
    "Mood"            integer                      NULL,
    "Tense"           integer                      NULL,
    "Voice"           integer                      NULL,
    "Person"          integer                      NULL,
    "Case"            integer                      NULL,
    "Gender"          integer                      NULL,
    "Number"          integer                      NULL,
    CONSTRAINT "SrGntWords_pk" PRIMARY KEY ("Id")
);

CREATE INDEX "SrGntWords_reference_idx" ON "unshackled-word"."SrGntWords" ("BibleBookId", "Chapter", "Verse");
CREATE INDEX "SrGntWords_strongs_idx" ON "unshackled-word"."SrGntWords" ("Strongs");
