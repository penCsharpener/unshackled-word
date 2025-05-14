CREATE TABLE SrWordInfo
(
    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
    BibleBookId    INTEGER NOT NULL,
    Chapter        INTEGER NOT NULL,
    Verse          INTEGER NOT NULL,
    WordInContext  TEXT    NOT NULL,
    Koine          TEXT    NOT NULL,
    Lemma          TEXT    NOT NULL,
    Strongs        TEXT    NOT NULL,
    PartOfSpeech   TEXT    NOT NULL,
    ConjugationKey TEXT    NOT NULL
);

CREATE TABLE ElberfelderVerseInfo
(
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    BibleBookId INTEGER NOT NULL,
    Chapter     INTEGER NOT NULL,
    Verse       INTEGER NOT NULL,
    Text        TEXT    NOT NULL
);

CREATE TABLE ElberfelderWordInfo
(
    Id                     INTEGER PRIMARY KEY AUTOINCREMENT,
    BibleBookId            INTEGER NOT NULL,
    ElberfelderVerseInfoId INTEGER NOT NULL,
    Chapter                INTEGER NOT NULL,
    Verse                  INTEGER NOT NULL,
    Word                   TEXT    NOT NULL,
    CleanedWord            TEXT    NOT NULL,
    Strongs                TEXT    NOT NULL
);


