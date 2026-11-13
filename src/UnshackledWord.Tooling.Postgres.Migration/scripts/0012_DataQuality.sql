-- Data quality page: snapshot of all German word <-> original word facts (materialized view) plus the refresh bookkeeping.
-- Built on the legacy mapping tables (Elb1871GreekMapping / Elb1871HebrewMapping). German lemma / PoS / morphology come
-- ONLY from Elb1871Morphology (Elb1871Words.Lemma is obsolete and never read). Strong's comes from the STEP word tables,
-- never from Elb1871GreekMapping.StrongsNumber (stale).
--
-- NOTE: 0014_AddedWordLinks.sql drops and recreates this view with the added-word fix (an added-word link is no longer 'aligned').
--
-- Idempotent: safe to run again. The view is created WITH NO DATA; the API fills it (POST api/dataQuality/refresh).
-- The statements run as the API's own DB user (unshackledrw owns the tables), so no extra grants are needed;
-- a read-only user would need: GRANT SELECT ON "unshackled-word"."DqWordFacts", "unshackled-word"."DqRefreshState".
--
-- One row per (German word x aligned original word), plus one row per German word without an original word
-- (Kind added | none), plus one row per original word without a German word (Kind unmappedSource).
--   Kind   aligned | added (only added-word rows / no real link) | none (no mapping row at all) | unmappedSource
--   Lang   G (NT, book >= 40) | H (OT)
--   Strong's base: G0846 / H0430 (Hebrew: the braced main morpheme); OrigClass: Greek morphology prefix (N V A T P R ...),
--   Hebrew grammar letter of the main morpheme (N V A R C D P T S ...).

CREATE TABLE IF NOT EXISTS "unshackled-word"."DqRefreshState"
(
    "Id"                  int4        NOT NULL,
    "RefreshedAt"         timestamptz NULL,
    "PreviousRefreshedAt" timestamptz NULL,
    "Counts"              jsonb       NULL,
    "PreviousCounts"      jsonb       NULL,
    CONSTRAINT "DqRefreshState_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "DqRefreshState_SingleRow_CK" CHECK ("Id" = 1)
);

INSERT INTO "unshackled-word"."DqRefreshState" ("Id")
VALUES (1)
ON CONFLICT ("Id") DO NOTHING;

DO
$$
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM pg_matviews WHERE schemaname = 'unshackled-word' AND matviewname = 'DqWordFacts') THEN
            CREATE MATERIALIZED VIEW "unshackled-word"."DqWordFacts" AS
            WITH "German" AS
            (
                SELECT w."Id"                         AS "ElbWordId"
                     , w."HebRefId"                   AS "HebRefId"
                     , w."BibleBookId"                AS "BibleBookId"
                     , w."Chapter"                    AS "Chapter"
                     , w."Verse"                      AS "Verse"
                     , w."PositionInVerse"            AS "PositionInVerse"
                     , w."PlainWord"                  AS "Plain"
                     , lower(w."PlainWord")           AS "PlainLower"
                     , NULLIF(m."Lemma", '')          AS "Lemma"
                     , lower(NULLIF(m."Lemma", ''))   AS "LemmaLower"
                     , m."PartOfSpeech"               AS "Pos"
                     , m."VerbForm"                   AS "VerbForm"
                     , m."Number"                     AS "Number"
                     , m."Case"                       AS "Case"
                     , m."Gender"                     AS "Gender"
                     , m."Tense"                      AS "Tense"
                     , m."Mood"                       AS "Mood"
                     , m."Person"                     AS "Person"
                     , m."Degree"                     AS "Degree"
                     , (m."Id" IS NOT NULL)           AS "HasMorphology"
                FROM "unshackled-word"."Elb1871Words" w
                    LEFT JOIN "unshackled-word"."Elb1871Morphology" m ON m."HebRefId" = w."HebRefId" AND m."PositionInVerse" = w."PositionInVerse"
            )
            , "Src" AS
            (
                SELECT 'G'::char(1)                                                    AS "Lang"
                     , s."Id"                                                          AS "StepWordId"
                     , s."LxxRefId"                                                    AS "SourceRefId"
                     , s."PositionInVerse"                                             AS "SourcePos"
                     , s."Greek"                                                       AS "SourceWord"
                     , substring(s."DisambiguatedStrongs" FROM 'G\d+')                 AS "StrongsBase"
                     , s."DisambiguatedStrongs"                                        AS "StrongsFull"
                     , NULLIF(split_part(split_part(s."Morphology", '-', 1), ' ', 1), '')                AS "OrigClass"
                     , (s."Morphology" ~ '-[PL]$')                                    AS "ProperNoun"
                     , s."IsInNestleAland"                                             AS "InNa"
                     , s."IsInTextusReceptus"                                          AS "InTr"
                     , NULL::varchar                                                   AS "HebType"
                FROM "unshackled-word"."StepGreekWords" s
                WHERE s."LxxRefId" >= 40000000
                UNION ALL
                SELECT 'H'::char(1)
                     , s."Id"
                     , s."LxxRefId"
                     , s."PositionInVerse"
                     , s."Hebrew"
                     , COALESCE(substring(hb."Braced" FROM 'H\d+'), substring(s."DisambiguatedStrongs" FROM 'H\d+'))
                     , COALESCE(NULLIF(hb."Braced", ''), s."DisambiguatedStrongs")
                     , NULLIF(left(hs."Seg", 1), '')
                     , (left(hs."Seg", 2) = 'Np')
                     , NULL::bool
                     , NULL::bool
                     , s."Type"
                FROM "unshackled-word"."StepHebrewWords" s
                    CROSS JOIN LATERAL (SELECT CASE WHEN position('{' IN s."DisambiguatedStrongs") > 0 THEN split_part(split_part(s."DisambiguatedStrongs", '{', 2), '}', 1) ELSE '' END AS "Braced"
                                             , length(split_part(s."DisambiguatedStrongs", '{', 1)) - length(replace(split_part(s."DisambiguatedStrongs", '{', 1), '/', '')) + 1 AS "Idx") hb
                    CROSS JOIN LATERAL (SELECT CASE WHEN hb."Idx" = 1 AND left(split_part(s."Grammar", '/', hb."Idx"), 1) IN ('H', 'A') THEN substr(split_part(s."Grammar", '/', hb."Idx"), 2)
                                                    ELSE split_part(s."Grammar", '/', hb."Idx") END AS "Seg") hs
                WHERE s."LxxRefId" < 40000000
            )
            , "Map" AS
            (
                SELECT 'G'::char(1) AS "Lang", g."ElbWordId", g."StepWordId", g."ParentGermanWordId", g."GermanWordPart"
                FROM "unshackled-word"."Elb1871GreekMapping" g
                UNION ALL
                SELECT 'H'::char(1), h."ElbWordId", h."StepWordId", h."ParentGermanWordId", h."GermanWordPart"
                FROM "unshackled-word"."Elb1871HebrewMapping" h
            )
            , "MappedSrc" AS
            (
                SELECT DISTINCT "Lang", "StepWordId"
                FROM "Map"
                WHERE "StepWordId" IS NOT NULL
            )
            , "MappedWords" AS
            (
                SELECT DISTINCT "ElbWordId"
                FROM "Map"
            )
            , "VerseMap" AS
            (
                SELECT v."LxxRefId", min(v."HebRefId") AS "HebRefId"
                FROM "unshackled-word"."Elb1871Verses" v
                GROUP BY v."LxxRefId"
            )
            , "Facts" AS
            (
                SELECT g."ElbWordId", g."HebRefId", g."BibleBookId", g."Chapter", g."Verse", g."PositionInVerse", g."Plain", g."PlainLower"
                     , g."Lemma", g."LemmaLower", g."Pos", g."VerbForm", g."Number", g."Case", g."Gender", g."Tense", g."Mood", g."Person", g."Degree", g."HasMorphology"
                     , s."Lang", 'aligned'::varchar(15) AS "Kind", m."ParentGermanWordId", m."GermanWordPart"
                     , s."StepWordId", s."SourceRefId", s."SourcePos", s."SourceWord", s."StrongsBase", s."StrongsFull", s."OrigClass", s."ProperNoun", s."InNa", s."InTr", s."HebType"
                FROM "German" g
                    JOIN "Map" m ON m."ElbWordId" = g."ElbWordId" AND m."StepWordId" IS NOT NULL
                    JOIN "Src" s ON s."Lang" = m."Lang" AND s."StepWordId" = m."StepWordId"
                UNION ALL
                SELECT g."ElbWordId", g."HebRefId", g."BibleBookId", g."Chapter", g."Verse", g."PositionInVerse", g."Plain", g."PlainLower"
                     , g."Lemma", g."LemmaLower", g."Pos", g."VerbForm", g."Number", g."Case", g."Gender", g."Tense", g."Mood", g."Person", g."Degree", g."HasMorphology"
                     , CASE WHEN g."BibleBookId" >= 40 THEN 'G' ELSE 'H' END::char(1)
                     , CASE WHEN mw."ElbWordId" IS NOT NULL THEN 'added' ELSE 'none' END::varchar(15)
                     , NULL::int4, NULL::varchar
                     , NULL::int4, NULL::int4, NULL::int4, NULL::varchar, NULL::varchar, NULL::varchar, NULL::varchar, NULL::bool, NULL::bool, NULL::bool, NULL::varchar
                FROM "German" g
                    LEFT JOIN "MappedWords" mw ON mw."ElbWordId" = g."ElbWordId"
                WHERE NOT EXISTS (SELECT 1
                                  FROM "Map" m
                                  WHERE m."ElbWordId" = g."ElbWordId"
                                    AND m."StepWordId" IS NOT NULL)
                UNION ALL
                SELECT NULL::int4, v."HebRefId", v."HebRefId" / 1000000, (v."HebRefId" / 1000) % 1000, v."HebRefId" % 1000, NULL::int4, NULL::varchar, NULL::varchar
                     , NULL::varchar, NULL::varchar, NULL::varchar, NULL::varchar, NULL::varchar, NULL::varchar, NULL::varchar, NULL::varchar, NULL::varchar, NULL::varchar, NULL::varchar, NULL::bool
                     , s."Lang", 'unmappedSource'::varchar(15), NULL::int4, NULL::varchar
                     , s."StepWordId", s."SourceRefId", s."SourcePos", s."SourceWord", s."StrongsBase", s."StrongsFull", s."OrigClass", s."ProperNoun", s."InNa", s."InTr", s."HebType"
                FROM "Src" s
                    JOIN "VerseMap" v ON v."LxxRefId" = s."SourceRefId"
                WHERE NOT EXISTS (SELECT 1
                                  FROM "MappedSrc" ms
                                  WHERE ms."Lang" = s."Lang"
                                    AND ms."StepWordId" = s."StepWordId")
            )
            SELECT f.*
                 , CASE WHEN f."Kind" = 'aligned' THEN count(*) OVER (PARTITION BY f."Lang", f."StepWordId") END AS "GermanPerSource"
                 , CASE WHEN f."Kind" = 'aligned' THEN count(*) OVER (PARTITION BY f."ElbWordId") END            AS "SourcePerGerman"
                 , CASE WHEN f."ElbWordId" IS NOT NULL THEN row_number() OVER (PARTITION BY f."ElbWordId" ORDER BY f."StepWordId" NULLS LAST) END AS "GermanRowNo"
            FROM "Facts" f
            WITH NO DATA;
        END IF;
    END
$$;

CREATE UNIQUE INDEX IF NOT EXISTS "DqWordFacts_Row_UQ" ON "unshackled-word"."DqWordFacts" ("Lang", "ElbWordId", "StepWordId") NULLS NOT DISTINCT;
CREATE INDEX IF NOT EXISTS "DqWordFacts_LemmaLower_idx" ON "unshackled-word"."DqWordFacts" ("LemmaLower" text_pattern_ops) WHERE "GermanRowNo" = 1;
CREATE INDEX IF NOT EXISTS "DqWordFacts_PlainLower_idx" ON "unshackled-word"."DqWordFacts" ("PlainLower" text_pattern_ops) WHERE "GermanRowNo" = 1;
CREATE INDEX IF NOT EXISTS "DqWordFacts_Strongs_idx" ON "unshackled-word"."DqWordFacts" ("Lang", "StrongsBase");
CREATE INDEX IF NOT EXISTS "DqWordFacts_Chapter_idx" ON "unshackled-word"."DqWordFacts" ("BibleBookId", "Chapter");
CREATE INDEX IF NOT EXISTS "DqWordFacts_NotAligned_idx" ON "unshackled-word"."DqWordFacts" ("Kind", "BibleBookId") WHERE "Kind" <> 'aligned';
CREATE INDEX IF NOT EXISTS "DqWordFacts_LemmaPos_idx" ON "unshackled-word"."DqWordFacts" ("LemmaLower", "Pos", "Lang");
