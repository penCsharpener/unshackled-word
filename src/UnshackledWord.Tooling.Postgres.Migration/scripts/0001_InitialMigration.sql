CREATE SCHEMA IF NOT EXISTS "unshackled-word";

CREATE TABLE "unshackled-word"."Elb1871Words"
(
    "Id"              serial4                     NOT NULL,
    "BibleBookId"     integer                     NOT NULL,
    "Chapter"         integer                     NOT NULL,
    "Verse"           integer                     NOT NULL,
    "HebRefId"        integer                     NOT NULL,
    "PositionInVerse" integer                     NOT NULL,
    "WordInContext"   varchar COLLATE "und-x-icu" NOT NULL,
    "PlainWord"       varchar COLLATE "und-x-icu" NULL,
    "Lemma"           varchar COLLATE "und-x-icu" NULL,
    "PartOfSpeech"    varchar COLLATE "und-x-icu" NULL,
    "GrammaticalKey"  varchar COLLATE "und-x-icu" NULL,
    CONSTRAINT "Elb1871Words_PK" PRIMARY KEY ("Id")
);

COMMENT
ON COLUMN "unshackled-word"."Elb1871Words"."PlainWord" IS 'Is the same as WordInContext but clean of all special characters.';

CREATE INDEX "Elb1871Words_reference_idx" ON "unshackled-word"."Elb1871Words" ("BibleBookId", "Chapter", "Verse");
CREATE INDEX "Elb1871Words_HebRefId_idx" ON "unshackled-word"."Elb1871Words" ("HebRefId");
ALTER TABLE "unshackled-word"."Elb1871Words" ADD CONSTRAINT "Elb1871RefAndPositions_unique" UNIQUE ("BibleBookId","Chapter","Verse","PositionInVerse");


CREATE TABLE "unshackled-word"."Elb1871Verses"
(
    "Id"           serial4                                                                  NOT NULL,
    "HebRefId"     int4                                                                     NOT NULL,
    "LxxRefId"     int4                                                                     NOT NULL,
    "VerseText"    text COLLATE "und-x-icu"                                                 NOT NULL,
    "SearchVector" tsvector GENERATED ALWAYS AS (to_tsvector('german', "VerseText")) STORED NULL,
    CONSTRAINT "Elb1871Verses_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "Elb1871Verses_unique" UNIQUE ("HebRefId")
);
CREATE INDEX "Elb1871Verses_HebRefId_idx" ON "unshackled-word"."Elb1871Verses" USING btree ("HebRefId");
CREATE INDEX "Elb1871Verses_LxxRefId_idx" ON "unshackled-word"."Elb1871Verses" USING btree ("LxxRefId");
CREATE INDEX "Elb1871Verses_Search_idx" ON "unshackled-word"."Elb1871Verses" USING gin ("SearchVector");


CREATE TABLE "unshackled-word"."Tsk"
(
    "Id"                      serial4                  NOT NULL,
    "LxxRefId"                integer                  NOT NULL,
    "Scope"                   text COLLATE "und-x-icu" NOT NULL,
    "RelatedStartLxxRefId"    integer                  NOT NULL,
    "RelatedEndLxxRefId"      integer                  NULL,
    CONSTRAINT "Tsk_PK" PRIMARY KEY ("Id")
);

CREATE INDEX "Tsk_reference_idx" ON "unshackled-word"."Tsk" ("LxxRefId");
CREATE INDEX "Tsk_relatedStartReference_idx" ON "unshackled-word"."Tsk" ("RelatedStartLxxRefId");
CREATE INDEX "Tsk_relatedEndReference_idx" ON "unshackled-word"."Tsk" ("RelatedEndLxxRefId");

CREATE TABLE "unshackled-word"."SblText"
(
    "Id"          serial4                  NOT NULL,
    "LxxRefId"    integer                  NOT NULL,
    "VerseText"   text COLLATE "und-x-icu" NOT NULL,
    CONSTRAINT "SblText_PK" PRIMARY KEY ("Id")
);

CREATE INDEX "SblText_reference_idx" ON "unshackled-word"."SblText" ("LxxRefId");

CREATE TABLE "unshackled-word"."SblApparatus"
(
    "Id"          serial4                  NOT NULL,
    "LxxRefId"    integer                  NOT NULL,
    "Text"        text COLLATE "und-x-icu" NOT NULL,
    CONSTRAINT "SblApparatus_PK" PRIMARY KEY ("Id")
);

CREATE INDEX "SblApparatus_reference_idx" ON "unshackled-word"."SblApparatus" ("LxxRefId");

CREATE TABLE "unshackled-word"."SrGntWords"
(
    "Id"              serial4                     NOT NULL,
    "LxxRefId"        integer                     NOT NULL,
    "WordInContext"   varchar COLLATE "und-x-icu" NOT NULL,
    "Koine"           varchar COLLATE "und-x-icu" NOT NULL,
    "Lemma"           varchar COLLATE "und-x-icu" NOT NULL,
    "PositionInVerse" integer                     NOT NULL,
    "Strongs"         varchar COLLATE "und-x-icu" NOT NULL,
    "PartOfSpeech"    varchar COLLATE "und-x-icu" NOT NULL,
    "GrammaticalKey"  varchar COLLATE "und-x-icu" NOT NULL,
    "Mood"            integer                     NULL,
    "Tense"           integer                     NULL,
    "Voice"           integer                     NULL,
    "Person"          integer                     NULL,
    "Case"            integer                     NULL,
    "Gender"          integer                     NULL,
    "Number"          integer                     NULL,
    CONSTRAINT "SrGntWords_PK" PRIMARY KEY ("Id")
);

CREATE INDEX "SrGntWords_reference_idx" ON "unshackled-word"."SrGntWords" ("LxxRefId");
CREATE INDEX "SrGntWords_strongs_idx" ON "unshackled-word"."SrGntWords" ("Strongs");

CREATE TABLE "unshackled-word"."Languages"
(
    "Id"   serial4                     NOT NULL,
    "Name" varchar COLLATE "und-x-icu" NOT NULL,
    CONSTRAINT "Languages_PK" PRIMARY KEY ("Id")
);

INSERT INTO "unshackled-word"."Languages" ("Name")
VALUES ('english'),
       ('german');

CREATE TABLE "unshackled-word"."BibleBooks"
(
    "Id"            serial4                     NOT NULL,
    "Name"          varchar COLLATE "und-x-icu" NOT NULL,
    "Abbreviations" varchar COLLATE "und-x-icu" NOT NULL,
    "LanguageId"    integer                     NOT NULL,
    CONSTRAINT "BibleBooks_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "BibleBooks_Languages_FK" FOREIGN KEY ("LanguageId") REFERENCES "unshackled-word"."Languages" ("Id")
);

INSERT INTO "unshackled-word"."BibleBooks" ("Id", "Name", "Abbreviations", "LanguageId")
VALUES
    ( 1, 'Genesis',         'Gen|Ge|Gn'                  , 1),
    ( 2, 'Exodus',          'Exod|Ex'                    , 1),
    ( 3, 'Leviticus',       'Lev|Lv|Le'                  , 1),
    ( 4, 'Numbers',         'Num|Nm|Nu'                  , 1),
    ( 5, 'Deuteronomy',     'Deut|Dt|De'                 , 1),
    ( 6, 'Joshua',          'Josh|Jos|Jo'                , 1),
    ( 7, 'Judges',          'Judg|Jdg|Jgs'               , 1),
    ( 8, 'Ruth',            'Ruth|Ru'                    , 1),
    ( 9, '1 Samuel',        '1 Sam|1Sm|1Sa'              , 1),
    (10, '2 Samuel',        '2 Sam|2Sm|2Sa'              , 1),
    (11, '1 Kings',         '1 Kgs|1Kg|1Ki'              , 1),
    (12, '2 Kings',         '2 Kgs|2Kg|2Ki'              , 1),
    (13, '1 Chronicles',    '1 Chr|1 Chron|1Ch'          , 1),
    (14, '2 Chronicles',    '2 Chr|2 Chron|2Ch'          , 1),
    (15, 'Ezra',            'Ezra|Ezr'                   , 1),
    (16, 'Nehemiah',        'Neh|Ne'                     , 1),
    (17, 'Esther',          'Esth|Est|Es'                , 1),
    (18, 'Job',             'Job|Jb'                     , 1),
    (19, 'Psalms',          'Ps|Pss'                     , 1),
    (20, 'Proverbs',        'Prov|Prv|Pr'                , 1),
    (21, 'Ecclesiastes',    'Eccl|Eccles|Ec|Qoh'         , 1),
    (22, 'Song of Songs',   'Song|SS|So|Sg|Cant|Can'     , 1),
    (23, 'Isaiah',          'Isa|Is'                     , 1),
    (24, 'Jeremiah',        'Jer|Je'                     , 1),
    (25, 'Lamentations',    'Lam|La'                     , 1),
    (26, 'Ezekiel',         'Ezek|Ezk|Ez'                , 1),
    (27, 'Daniel',          'Dan|Dn|Da'                  , 1),
    (28, 'Hosea',           'Hos|Ho'                     , 1),
    (29, 'Joel',            'Joel|Joe|Jl'                , 1),
    (30, 'Amos',            'Amos|Am'                    , 1),
    (31, 'Obadiah',         'Obad|Ob'                    , 1),
    (32, 'Jonah',           'Jonah|Jon'                  , 1),
    (33, 'Micah',           'Mic|Mi'                     , 1),
    (34, 'Nahum',           'Nah|Na'                     , 1),
    (35, 'Habakkuk',        'Hab|Hb'                     , 1),
    (36, 'Zephaniah',       'Zeph|Zep'                   , 1),
    (37, 'Haggai',          'Hag|Hg'                     , 1),
    (38, 'Zechariah',       'Zech|Zec'                   , 1),
    (39, 'Malachi',         'Mal|Ml'                     , 1),
    (40, 'Matthew',         'Matt|Mat|Mt'                , 1),
    (41, 'Mark',            'Mark|Mar|Mk'                , 1),
    (42, 'Luke',            'Luke|Lk|Lu'                 , 1),
    (43, 'John',            'John|Jn|Jo'                 , 1),
    (44, 'Acts',            'Acts|Ac'                    , 1),
    (45, 'Romans',          'Rom|Rm|Ro'                  , 1),
    (46, '1 Corinthians',   '1 Cor|1 Co|1C'              , 1),
    (47, '2 Corinthians',   '2 Cor|2 Co|2C'              , 1),
    (48, 'Galatians',       'Gal|Ga'                     , 1),
    (49, 'Ephesians',       'Eph|Ep'                     , 1),
    (50, 'Philippians',     'Phil|Php'                   , 1),
    (51, 'Colossians',      'Col|Co'                     , 1),
    (52, '1 Thessalonians', '1 Thess|1 Thes|1Th'         , 1),
    (53, '2 Thessalonians', '2 Thess|2 Thes|2Th'         , 1),
    (54, '1 Timothy',       '1 Tim|1 Ti|1 T|1 Tm'        , 1),
    (55, '2 Timothy',       '2 Tim|2 Ti|2 T|2 Tm'        , 1),
    (56, 'Titus',           'Tit|Tt'                     , 1),
    (57, 'Philemon',        'Phm|Phlm|Philem|Phile'      , 1),
    (58, 'Hebrews',         'Heb|Hebr|H'                 , 1),
    (59, 'James',           'Jas|Jam|Ja'                 , 1),
    (60, '1 Peter',         '1 Pet|1 Pt|1 P|1 Pe'        , 1),
    (61, '2 Peter',         '2 Pet|2 Pt|2 P|2 Pe'        , 1),
    (62, '1 John',          '1 Jn|1 Jo|1 J'              , 1),
    (63, '2 John',          '2 Jn|2 Jo|2 J'              , 1),
    (64, '3 John',          '3 Jn|3 Jo|3 J'              , 1),
    (65, 'Jude',            'Jud|Jd'                     , 1),
    (66, 'Revelation',      'Rev'                        , 1);
