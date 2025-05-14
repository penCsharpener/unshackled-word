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

CREATE INDEX gnthotwords_reference_idx ON "unshackled-word"."GntHotWords" ("BibleBookId", "Chapter", "Verse");
CREATE INDEX gnthotwords_strongs_idx ON "unshackled-word"."GntHotWords" ("Strongs");

CREATE TABLE "unshackled-word"."Elb1871Words"
(
    "Id"              serial4                      NOT NULL,
    "BibleBookId"     integer                      NOT NULL,
    "Chapter"         integer                      NOT NULL,
    "Verse"           integer                      NOT NULL,
    "WordInContext"   varchar COLLATE "en_US.utf8" NOT NULL,
    "German"          varchar COLLATE "en_US.utf8" NOT NULL,
    "Lemma"           varchar COLLATE "en_US.utf8" NULL,
    "PositionInVerse" integer                      NOT NULL,
    "Strongs"         varchar NULL,
    "PartOfSpeech"    varchar                      NULL,
    "GrammaticalKey"  varchar                      NULL,
    CONSTRAINT "Elb1871Words_pk" PRIMARY KEY ("Id")
);

CREATE INDEX elb1871words_reference_idx ON "unshackled-word"."Elb1871Words" ("BibleBookId", "Chapter", "Verse");
CREATE INDEX elb1871words_strongs_idx ON "unshackled-word"."Elb1871Words" ("Strongs");

