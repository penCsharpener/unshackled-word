CREATE TABLE "unshackled-word"."BibleStructureVerses"
(
    "BibleBookId"  INTEGER NOT NULL,
    "Chapter"      INTEGER NOT NULL,
    "LastVerse"    INTEGER NOT NULL,
    "AltChapter"   INTEGER NULL,
    "AltLastVerse" INTEGER NULL
);

CREATE TABLE "unshackled-word"."BibleStructureChapters"
(
    "BibleBookId"    INTEGER NOT NULL,
    "LastChapter"    INTEGER NOT NULL,
    "AltLastChapter" INTEGER NULL
);
