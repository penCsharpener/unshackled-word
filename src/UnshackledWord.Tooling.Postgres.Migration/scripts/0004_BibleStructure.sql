CREATE TABLE "unshackled-word"."BibleStructureVerses"
(
    "BibleBookId"  INT NOT NULL,
    "Chapter"      INT NOT NULL,
    "LastVerse"    INT NOT NULL,
    "AltChapter"   INT NULL,
    "AltLastVerse" INT NULL
);

CREATE TABLE "unshackled-word"."BibleStructureChapters"
(
    "BibleBookId"    INT NOT NULL,
    "LastChapter"    INT NOT NULL,
    "AltLastChapter" INT NULL
);

CREATE TABLE "unshackled-word"."BibleVerseCountingMapping"
(
    "Id"       serial4 NOT NULL,
    "HebRefId" INT     NOT NULL,
    "LxxRefId" INT     NOT NULL,
    CONSTRAINT "BibleVerseCountingMapping_PK" PRIMARY KEY ("Id")
);
