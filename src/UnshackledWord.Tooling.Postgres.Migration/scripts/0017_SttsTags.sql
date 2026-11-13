-- STTS tag table (roadmap 0010.5): the source of truth for the 54 STTS 1999 tags in TIGER spelling.
-- Schema only. The rows come from assets/seed/stts-tags.csv, imported by SeedDb (SttsSeedRunner) after all migrations.
--   Upos         default Universal Dependencies tag of the STTS tag
--   AllowedUpos  every UD tag accepted together with the STTS tag (data quality check L7: PartOfSpeech <> ALL(AllowedUpos))
--   Alias        other spelling of the tag (e.g. PAV for PROAV)
-- Idempotent.

CREATE TABLE IF NOT EXISTS "unshackled-word"."SttsTags"
(
    "Tag"           varchar(8)  NOT NULL,
    "LabelDe"       varchar     NOT NULL,
    "LabelEn"       varchar     NOT NULL,
    "DescriptionDe" varchar     NOT NULL DEFAULT '',
    "DescriptionEn" varchar     NOT NULL DEFAULT '',
    "ExampleDe"     varchar     NOT NULL DEFAULT '',
    "Upos"          varchar(10) NOT NULL,
    "AllowedUpos"   text[]      NOT NULL,
    "Alias"         varchar(8)  NULL,
    "IsPunctuation" boolean     NOT NULL DEFAULT false,
    "SortOrder"     integer     NOT NULL,
    CONSTRAINT "PK_SttsTags" PRIMARY KEY ("Tag")
);

COMMENT ON TABLE "unshackled-word"."SttsTags" IS 'STTS 1999 tags (TIGER spelling); seeded from assets/seed/stts-tags.csv. Elb1871Morphology.Stts references Tag.';
COMMENT ON COLUMN "unshackled-word"."SttsTags"."Upos" IS 'Default UD part of speech of the tag.';
COMMENT ON COLUMN "unshackled-word"."SttsTags"."AllowedUpos" IS 'All UD parts of speech accepted together with the tag (includes Upos).';
