-- Greek editions per word (roadmap 0025.1, docs/roadmaps/0025-greek-editions/03-model.md).
--   GreekEditions          catalog of the edition tokens in "StepGreekWords"."Editions" ('NA28', 'TR', 'P66*', ...). "IsMain" marks the
--                          eight printed editions that count for "in all editions" and the reader filter; the rest (KJV, NIV,
--                          manuscripts, versions) are shown but never count. "Name" is German; English names come from the frontend i18n.
--   StepGreekWordEditions  one row per word and edition, derived from the raw "Editions" string (which stays as STEP delivered it).
--                          "Shift" = +n for '»n', -n for '«n' (word order differs in that edition), NULL = same order. Any other suffix
--                          ('«14.24', '»1»1') gives "Shift" NULL and the suffix in "ShiftRaw".
-- "RefreshStepGreekWordEditions"() rebuilds the join table from the strings: unknown codes become catalog rows with "Kind" = 'unknown'
-- (shown on the data-quality page), then TRUNCATE + INSERT. Returns the number of rows written. Called once at the end of this script
-- (existing databases) and by SeedDb after the Greek STEP import (fresh databases). Derived data: not part of the repo seed, no audit.
-- "StepGreekWordEditionTokens"() is the token parser shared by both steps of the refresh.
--
-- Idempotent: safe to run again.

CREATE TABLE IF NOT EXISTS "unshackled-word"."GreekEditions"
(
    "Id"     int2         GENERATED ALWAYS AS IDENTITY,
    "Code"   varchar(20)  NOT NULL,
    "Name"   varchar(200) NOT NULL,
    "Year"   int2         NULL,
    "Kind"   varchar(20)  NOT NULL,
    "IsMain" bool         NOT NULL DEFAULT false,
    "Sort"   int2         NOT NULL,
    CONSTRAINT "GreekEditions_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "GreekEditions_Code_UQ" UNIQUE ("Code"),
    CONSTRAINT "GreekEditions_Kind_CK" CHECK ("Kind" IN ('edition', 'translation', 'manuscript', 'version', 'unknown'))
);

CREATE TABLE IF NOT EXISTS "unshackled-word"."StepGreekWordEditions"
(
    "StepWordId" int4        NOT NULL,
    "EditionId"  int2        NOT NULL,
    "Shift"      int2        NULL,
    "ShiftRaw"   varchar(20) NULL,
    CONSTRAINT "StepGreekWordEditions_PK" PRIMARY KEY ("StepWordId", "EditionId"),
    CONSTRAINT "StepGreekWordEditions_StepWord_FK" FOREIGN KEY ("StepWordId") REFERENCES "unshackled-word"."StepGreekWords" ("Id") ON DELETE CASCADE,
    CONSTRAINT "StepGreekWordEditions_Edition_FK" FOREIGN KEY ("EditionId") REFERENCES "unshackled-word"."GreekEditions" ("Id"),
    CONSTRAINT "StepGreekWordEditions_Shift_CK" CHECK ("Shift" IS NULL OR "ShiftRaw" IS NULL)
);

-- the words of one edition (reader filter, data quality)
CREATE INDEX IF NOT EXISTS "StepGreekWordEditions_Edition_idx" ON "unshackled-word"."StepGreekWordEditions" ("EditionId");

COMMENT ON TABLE "unshackled-word"."GreekEditions" IS 'Catalog of the Greek edition tokens in StepGreekWords.Editions (roadmap 0025). Kind unknown = created by RefreshStepGreekWordEditions for a token not in the catalog.';
COMMENT ON COLUMN "unshackled-word"."GreekEditions"."Code" IS 'Token as in STEP without order suffix: NA28, TR, P66*.';
COMMENT ON COLUMN "unshackled-word"."GreekEditions"."IsMain" IS 'One of the eight printed editions; only these count for "in all editions" and the reader filter.';
COMMENT ON TABLE "unshackled-word"."StepGreekWordEditions" IS 'Word to edition, derived from StepGreekWords.Editions by RefreshStepGreekWordEditions() (roadmap 0025). Not part of the repo seed.';
COMMENT ON COLUMN "unshackled-word"."StepGreekWordEditions"."Shift" IS '+n for »n, -n for «n (word order differs in this edition), NULL = same order or a non-numeric suffix (see ShiftRaw).';
COMMENT ON COLUMN "unshackled-word"."StepGreekWordEditions"."ShiftRaw" IS 'Original suffix when it is not a plain number: «14.24, »1»1. NULL otherwise.';

INSERT INTO "unshackled-word"."GreekEditions" ("Code", "Name", "Year", "Kind", "IsMain", "Sort")
VALUES ('NA28',   'Nestle-Aland, 28. Auflage',                2012, 'edition',     true,  1)
     , ('NA27',   'Nestle-Aland, 27. Auflage',                1993, 'edition',     true,  2)
     , ('Tyn',    'Tyndale House GNT',                        2017, 'edition',     true,  3)
     , ('SBL',    'SBLGNT (Holmes)',                          2010, 'edition',     true,  4)
     , ('WH',     'Westcott-Hort',                            1881, 'edition',     true,  5)
     , ('Treg',   'Tregelles',                                1879, 'edition',     true,  6)
     , ('TR',     'Textus Receptus (Scrivener)',              1894, 'edition',     true,  7)
     , ('Byz',    'Byzantinischer Text (Robinson-Pierpont)',  2005, 'edition',     true,  8)
     , ('KJV',    'Grundtext der King James Version',         NULL, 'translation', false, 20)
     , ('NIV',    'Grundtext der NIV',                        NULL, 'translation', false, 21)
     , ('01',     'Sinaiticus',                               NULL, 'manuscript',  false, 30)
     , ('02',     'Alexandrinus',                             NULL, 'manuscript',  false, 31)
     , ('03',     'Vaticanus',                                NULL, 'manuscript',  false, 32)
     , ('04',     'Ephraemi rescriptus',                      NULL, 'manuscript',  false, 33)
     , ('05',     'Bezae',                                    NULL, 'manuscript',  false, 34)
     , ('06',     'Claromontanus',                            NULL, 'manuscript',  false, 35)
     , ('032',    'Washingtonianus',                          NULL, 'manuscript',  false, 36)
     , ('P66',    'Papyrus 66',                               NULL, 'manuscript',  false, 37)
     , ('P66*',   'Papyrus 66 (Erstschreiber)',               NULL, 'manuscript',  false, 38)
     , ('Coptic', 'Koptisch',                                 NULL, 'version',     false, 40)
     , ('Latin',  'Lateinisch',                               NULL, 'version',     false, 41)
     , ('Syriac', 'Syrisch',                                  NULL, 'version',     false, 42)
ON CONFLICT ("Code") DO UPDATE
    SET "Name"   = EXCLUDED."Name"
      , "Year"   = EXCLUDED."Year"
      , "Kind"   = EXCLUDED."Kind"
      , "IsMain" = EXCLUDED."IsMain"
      , "Sort"   = EXCLUDED."Sort";

-- the parsed tokens of all words: split on '+', trimmed, empty tokens and tokens without a code skipped;
-- "Code" = part before the first » or «, "Suffix" = the rest (NULL if none)
CREATE OR REPLACE FUNCTION "unshackled-word"."StepGreekWordEditionTokens"()
    RETURNS TABLE ("StepWordId" int4, "Ord" int4, "Code" varchar, "Suffix" varchar)
    LANGUAGE sql
    STABLE
AS
$$
    SELECT x."StepWordId"
         , x."Ord"
         , x."Code"
         , x."Suffix"
    FROM
    (
        SELECT w."Id"                                                                  AS "StepWordId"
             , t."Ord"::int4                                                           AS "Ord"
             , substring(btrim(t."Token") FROM '^[^»«]*')::varchar COLLATE "default"   AS "Code"
             , substring(btrim(t."Token") FROM '[»«].*$')::varchar COLLATE "default"   AS "Suffix"
        FROM "unshackled-word"."StepGreekWords" w
            CROSS JOIN LATERAL unnest(string_to_array(w."Editions", '+')) WITH ORDINALITY t("Token", "Ord")
        WHERE 1=1
            AND btrim(t."Token") <> ''
    ) x
    WHERE 1=1
        AND x."Code" <> '';
$$;

CREATE OR REPLACE FUNCTION "unshackled-word"."RefreshStepGreekWordEditions"()
    RETURNS int
    LANGUAGE plpgsql
AS
$$
DECLARE
    "Written" int;
BEGIN
    -- unknown codes are kept as catalog rows, never dropped
    INSERT INTO "unshackled-word"."GreekEditions" ("Code", "Name", "Year", "Kind", "IsMain", "Sort")
    SELECT DISTINCT t."Code"
         , t."Code"
         , NULL::int2
         , 'unknown'
         , false
         , 999
    FROM "unshackled-word"."StepGreekWordEditionTokens"() t
    WHERE 1=1
        AND NOT EXISTS (SELECT 1 FROM "unshackled-word"."GreekEditions" ge WHERE ge."Code" = t."Code")
    ON CONFLICT ("Code") DO NOTHING;

    TRUNCATE "unshackled-word"."StepGreekWordEditions";

    -- one row per word and edition: a code listed twice for one word keeps its first occurrence
    INSERT INTO "unshackled-word"."StepGreekWordEditions" ("StepWordId", "EditionId", "Shift", "ShiftRaw")
    SELECT DISTINCT ON (t."StepWordId", ge."Id")
           t."StepWordId"
         , ge."Id"
         , CASE
               WHEN t."Suffix" ~ '^»[0-9]+$' THEN substring(t."Suffix" FROM 2)::int2
               WHEN t."Suffix" ~ '^«[0-9]+$' THEN -substring(t."Suffix" FROM 2)::int2
           END                                                    AS "Shift"
         , CASE
               WHEN t."Suffix" !~ '^[»«][0-9]+$' THEN t."Suffix"
           END                                                    AS "ShiftRaw"
    FROM "unshackled-word"."StepGreekWordEditionTokens"() t
        JOIN "unshackled-word"."GreekEditions" ge ON ge."Code" = t."Code"
    ORDER BY t."StepWordId", ge."Id", t."Ord";

    GET DIAGNOSTICS "Written" = ROW_COUNT;

    RETURN "Written";
END;
$$;

COMMENT ON FUNCTION "unshackled-word"."RefreshStepGreekWordEditions"() IS 'Rebuilds StepGreekWordEditions from StepGreekWords.Editions (roadmap 0025); adds unknown codes to GreekEditions as Kind unknown. Returns the rows written.';

SELECT "unshackled-word"."RefreshStepGreekWordEditions"();
