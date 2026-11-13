-- German morphology per Elb1871 word (roadmap 0010.1, prerequisite 0008.0). Schema only: the rows come from the morphology seed
-- (assets/seed/morphology/*.csv, imported by the SeedDb MorphologySeedRunner after all migrations), never from this script.
--
-- Why "0009a": the table existed on the maintainer's DB long before it had a CREATE TABLE, and 0010 (indexes), 0012 and 0014 (joins)
-- already depend on it. DbUp orders scripts by name, so this script has to sort between 0009_LemmaEdits.sql and 0010_LemmaBulkEdit.sql:
-- fresh installs then have the table before 0010 runs; databases that already ran 0010+ simply run this script late (it is not in their
-- journal yet) and it changes nothing there because every statement is guarded.
--
-- The shape below is the exact shape of the maintainer's table (pg_dump -s, 2026-11-13): the lemma editor and the data-quality views
-- read these columns. The two lemma indexes are created by 0010 and are intentionally not repeated here.

CREATE TABLE IF NOT EXISTS "unshackled-word"."Elb1871Morphology"
(
    "Id"              serial4      NOT NULL,
    "HebRefId"        int4         NOT NULL,
    "PositionInVerse" int4         NOT NULL,
    "Lemma"           varchar(255) NOT NULL,
    "PartOfSpeech"    varchar(10)  NOT NULL,
    "Stts"            varchar(10)  NULL,
    "Degree"          varchar(5)   NULL,
    "VerbForm"        varchar(5)   NULL,
    "Tense"           varchar(5)   NULL,
    "Person"          varchar(2)   NULL,
    "Number"          varchar(2)   NULL,
    "Mood"            varchar(5)   NULL,
    "Case"            varchar(5)   NULL,
    "Gender"          varchar(2)   NULL,
    CONSTRAINT "Elb1871Morphology_pkey" PRIMARY KEY ("Id"),
    CONSTRAINT "UQ_Elb1871Morphology_Verse_Position" UNIQUE ("HebRefId", "PositionInVerse")
);

COMMENT ON TABLE "unshackled-word"."Elb1871Morphology" IS 'German lemma and morphology of an Elb1871Words row, keyed by (HebRefId, PositionInVerse). At most one row per word; words without a row are listed by data-quality check L1.';
COMMENT ON COLUMN "unshackled-word"."Elb1871Morphology"."PartOfSpeech" IS 'Universal Dependencies part of speech (UPOS), e.g. NOUN, VERB, ADP.';
COMMENT ON COLUMN "unshackled-word"."Elb1871Morphology"."Stts" IS 'STTS tag (Stuttgart-Tuebingen tagset), e.g. NN, VVFIN.';

-- ElbMorphologyRaw was an empty leftover of the morphology tagger run; nothing references it. Dropped only while it is empty, so a
-- database that unexpectedly holds rows in it keeps them.
DO
$$
    DECLARE
        row_count bigint;
    BEGIN
        -- Dynamic SQL: a plain SELECT against the table would fail at parse time on installs where it does not exist.
        IF to_regclass('"unshackled-word"."ElbMorphologyRaw"') IS NOT NULL THEN
            EXECUTE 'SELECT count(*) FROM "unshackled-word"."ElbMorphologyRaw"' INTO row_count;

            IF row_count = 0 THEN
                DROP TABLE "unshackled-word"."ElbMorphologyRaw";
            END IF;
        END IF;
    END
$$;
