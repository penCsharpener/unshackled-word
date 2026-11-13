-- Added-word links no longer count as aligned (roadmap 0008 verification finding). Supersedes the "DqWordFacts" definition of 0012_DataQuality.sql
-- and the Stats* views of 0013_MappingStats.sql; both stay as they were applied, this script drops and recreates all five views.
--
-- Why: Elb1871HebrewMapping / Elb1871GreekMapping rows with "IsAddedWord" = true can still carry a "StepWordId" (49,852 Hebrew, 624 Greek rows:
-- the added German word, e.g. "der", is attached to a neighbouring original word, e.g. the object marker H0853). The live Strong's SQL ignores them
-- (NOT IsAddedWord); 0012 treated every row with a StepWordId as aligned, so snapshot counts and renderings included added-word links.
--
-- New semantics (single place: the "Map" / "MappedSrc" / "Facts" CTEs below):
--   Kind 'aligned'        German word x original word through a REAL link (StepWordId set, IsAddedWord = false). Only these feed Strong's stats and M-checks.
--   Kind 'added'          German word with mapping rows but no real link (added-word flag set or no StepWordId). One row, StepWordId NULL as before.
--   Kind 'none'           German word without any mapping row.
--   Kind 'unmappedSource' original word without a real link; an original word that is only the anchor of added-word links is unmapped (U2 / U3).
--   "IsAddedWord" (new)   true on 'added' rows whose mapping carried the added-word flag; false on all other rows (and on 'added' rows without the flag).
-- NULL "IsAddedWord" in the mapping tables is read as false (a real link); the live SQL's NOT IsAddedWord would drop such rows, but none exist.
--
-- Chain: DROP ... CASCADE on "DqWordFacts" also drops the four Stats* views, so they are recreated here unchanged (they read DqWordFacts only).
-- Fresh install: 0012 and 0013 create the old, empty (WITH NO DATA) objects, this script replaces them; migrated DB: same result. Identical either way.
-- Idempotent: the drop only happens while "DqWordFacts" has no "IsAddedWord" column. All views are WITH NO DATA again: refresh them with
-- POST api/dataQuality/refresh (or REFRESH MATERIALIZED VIEW as the owner: DqWordFacts first, then the four Stats* views).
-- "StatsRefreshedAt" is reset to NULL on the drop so the Strong's page and word stats fall back to the live SQL until the next refresh.

DO
$$
    BEGIN
        IF EXISTS (SELECT 1 FROM pg_matviews WHERE schemaname = 'unshackled-word' AND matviewname = 'DqWordFacts')
            AND NOT EXISTS (SELECT 1
                            FROM pg_attribute at
                            WHERE at.attrelid = '"unshackled-word"."DqWordFacts"'::regclass
                              AND at.attname = 'IsAddedWord'
                              AND NOT at.attisdropped)
        THEN
            DROP MATERIALIZED VIEW "unshackled-word"."DqWordFacts" CASCADE;
            UPDATE "unshackled-word"."DqRefreshState" SET "StatsRefreshedAt" = NULL WHERE "Id" = 1;
        END IF;
    END
$$;

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
                SELECT 'G'::char(1) AS "Lang", g."ElbWordId", g."StepWordId", g."ParentGermanWordId", g."GermanWordPart", COALESCE(g."IsAddedWord", false) AS "IsAddedWord"
                FROM "unshackled-word"."Elb1871GreekMapping" g
                UNION ALL
                SELECT 'H'::char(1), h."ElbWordId", h."StepWordId", h."ParentGermanWordId", h."GermanWordPart", COALESCE(h."IsAddedWord", false)
                FROM "unshackled-word"."Elb1871HebrewMapping" h
            )
            , "MappedSrc" AS
            (
                -- an added-word link attaches a German word to a neighbouring original word; it does not align that original word
                SELECT DISTINCT "Lang", "StepWordId"
                FROM "Map"
                WHERE "StepWordId" IS NOT NULL
                  AND NOT "IsAddedWord"
            )
            , "MappedWords" AS
            (
                SELECT DISTINCT "ElbWordId"
                FROM "Map"
            )
            , "AddedFlagWords" AS
            (
                SELECT DISTINCT "ElbWordId"
                FROM "Map"
                WHERE "IsAddedWord"
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
                     , s."Lang", 'aligned'::varchar(15) AS "Kind", m."ParentGermanWordId", m."GermanWordPart", false AS "IsAddedWord"
                     , s."StepWordId", s."SourceRefId", s."SourcePos", s."SourceWord", s."StrongsBase", s."StrongsFull", s."OrigClass", s."ProperNoun", s."InNa", s."InTr", s."HebType"
                FROM "German" g
                    JOIN "Map" m ON m."ElbWordId" = g."ElbWordId" AND m."StepWordId" IS NOT NULL AND NOT m."IsAddedWord"
                    JOIN "Src" s ON s."Lang" = m."Lang" AND s."StepWordId" = m."StepWordId"
                UNION ALL
                SELECT g."ElbWordId", g."HebRefId", g."BibleBookId", g."Chapter", g."Verse", g."PositionInVerse", g."Plain", g."PlainLower"
                     , g."Lemma", g."LemmaLower", g."Pos", g."VerbForm", g."Number", g."Case", g."Gender", g."Tense", g."Mood", g."Person", g."Degree", g."HasMorphology"
                     , CASE WHEN g."BibleBookId" >= 40 THEN 'G' ELSE 'H' END::char(1)
                     , CASE WHEN mw."ElbWordId" IS NOT NULL THEN 'added' ELSE 'none' END::varchar(15)
                     , NULL::int4, NULL::varchar, (afw."ElbWordId" IS NOT NULL)
                     , NULL::int4, NULL::int4, NULL::int4, NULL::varchar, NULL::varchar, NULL::varchar, NULL::varchar, NULL::bool, NULL::bool, NULL::bool, NULL::varchar
                FROM "German" g
                    LEFT JOIN "MappedWords" mw ON mw."ElbWordId" = g."ElbWordId"
                    LEFT JOIN "AddedFlagWords" afw ON afw."ElbWordId" = g."ElbWordId"
                WHERE NOT EXISTS (SELECT 1
                                  FROM "Map" m
                                  WHERE m."ElbWordId" = g."ElbWordId"
                                    AND m."StepWordId" IS NOT NULL
                                    AND NOT m."IsAddedWord")
                UNION ALL
                SELECT NULL::int4, v."HebRefId", v."HebRefId" / 1000000, (v."HebRefId" / 1000) % 1000, v."HebRefId" % 1000, NULL::int4, NULL::varchar, NULL::varchar
                     , NULL::varchar, NULL::varchar, NULL::varchar, NULL::varchar, NULL::varchar, NULL::varchar, NULL::varchar, NULL::varchar, NULL::varchar, NULL::varchar, NULL::varchar, NULL::bool
                     , s."Lang", 'unmappedSource'::varchar(15), NULL::int4, NULL::varchar, false
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

-- Stats* views (unchanged from 0013_MappingStats.sql)
DO
$$
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM pg_matviews WHERE schemaname = 'unshackled-word' AND matviewname = 'StatsStrongsRenderings') THEN
            CREATE MATERIALIZED VIEW "unshackled-word"."StatsStrongsRenderings" AS
            SELECT f."Lang"                                                      AS "Lang"
                 , f."StrongsFull"                                               AS "StrongsKey"
                 , min(f."StrongsBase")                                          AS "StrongsBase"
                 , COALESCE(f."LemmaLower", f."PlainLower")                      AS "GermanKey"
                 , f."Pos"                                                       AS "Pos"
                 , bool_and(f."LemmaLower" IS NULL)                              AS "KeyIsForm"
                 , mode() WITHIN GROUP (ORDER BY COALESCE(f."Lemma", f."Plain")) AS "GermanDisplay"
                 , count(DISTINCT f."StepWordId")::int                           AS "SourceCount"
                 , count(DISTINCT f."ElbWordId")::int                            AS "GermanCount"
                 , count(DISTINCT f."HebRefId")::int                             AS "VerseCount"
            FROM "unshackled-word"."DqWordFacts" f
            WHERE 1=1
                AND f."Kind" = 'aligned'
                AND f."Plain" <> ''
                AND COALESCE(f."StrongsFull", '') <> ''
            GROUP BY f."Lang", f."StrongsFull", COALESCE(f."LemmaLower", f."PlainLower"), f."Pos"
            WITH NO DATA;
        END IF;

        IF NOT EXISTS (SELECT 1 FROM pg_matviews WHERE schemaname = 'unshackled-word' AND matviewname = 'StatsStrongs') THEN
            CREATE MATERIALIZED VIEW "unshackled-word"."StatsStrongs" AS
            WITH "Words" AS
            (
                -- one row per original word: aligned if it has at least one German word; its book/verse is the German side (min)
                SELECT f."Lang"                                  AS "Lang"
                     , f."StepWordId"                            AS "StepWordId"
                     , min(f."StrongsFull")                      AS "StrongsKey"
                     , min(f."StrongsBase")                      AS "StrongsBase"
                     , bool_or(f."Kind" = 'aligned')             AS "Aligned"
                     , min(f."BibleBookId")                      AS "Book"
                     , min(f."HebRefId")                         AS "Verse"
                FROM "unshackled-word"."DqWordFacts" f
                WHERE 1=1
                    AND f."StepWordId" IS NOT NULL
                    AND COALESCE(f."StrongsFull", '') <> ''
                GROUP BY f."Lang", f."StepWordId"
            )
            , "PerBook" AS
            (
                SELECT w."Lang"
                     , w."StrongsKey"
                     , min(w."StrongsBase")                      AS "StrongsBase"
                     , w."Book"
                     , count(*)::int                             AS "C"
                     , count(*) FILTER (WHERE w."Aligned")::int  AS "Mapped"
                     , count(DISTINCT w."Verse")::int            AS "V"
                FROM "Words" w
                GROUP BY w."Lang", w."StrongsKey", w."Book"
            )
            SELECT b."Lang"                                                                AS "Lang"
                 , b."StrongsKey"                                                          AS "StrongsKey"
                 , min(b."StrongsBase")                                                    AS "StrongsBase"
                 , sum(b."C")::int                                                         AS "Occurrences"
                 , sum(b."Mapped")::int                                                    AS "MappedCount"
                 , sum(b."V")::int                                                         AS "VerseCount"
                 , jsonb_agg(jsonb_build_object('b', b."Book", 'c', b."C", 'v', b."V") ORDER BY b."Book") AS "BookCounts"
            FROM "PerBook" b
            GROUP BY b."Lang", b."StrongsKey"
            WITH NO DATA;
        END IF;

        IF NOT EXISTS (SELECT 1 FROM pg_matviews WHERE schemaname = 'unshackled-word' AND matviewname = 'StatsGermanLemma') THEN
            CREATE MATERIALIZED VIEW "unshackled-word"."StatsGermanLemma" AS
            WITH "PerBook" AS
            (
                SELECT COALESCE(f."LemmaLower", f."PlainLower")                                  AS "GermanKey"
                     , f."Pos"                                                                   AS "Pos"
                     , f."BibleBookId"                                                           AS "Book"
                     , bool_and(f."LemmaLower" IS NULL)                                          AS "KeyIsForm"
                     , mode() WITHIN GROUP (ORDER BY COALESCE(f."Lemma", f."Plain"))             AS "GermanDisplay"
                     , count(*)::int                                                             AS "C"
                     , count(*) FILTER (WHERE f."Kind" IN ('added', 'none'))::int                AS "U"
                     , count(DISTINCT f."HebRefId")::int                                         AS "V"
                FROM "unshackled-word"."DqWordFacts" f
                WHERE 1=1
                    AND f."GermanRowNo" = 1
                    AND f."Plain" <> ''
                GROUP BY COALESCE(f."LemmaLower", f."PlainLower"), f."Pos", f."BibleBookId"
            )
            SELECT b."GermanKey"                                                                                               AS "GermanKey"
                 , b."Pos"                                                                                                     AS "Pos"
                 , bool_and(b."KeyIsForm")                                                                                     AS "KeyIsForm"
                 , (array_agg(b."GermanDisplay" ORDER BY b."C" DESC))[1]                                                       AS "GermanDisplay"
                 , sum(b."C")::int                                                                                             AS "Occurrences"
                 , sum(b."U")::int                                                                                             AS "UnmappedCount"
                 , sum(b."V")::int                                                                                             AS "VerseCount"
                 , jsonb_agg(jsonb_build_object('b', b."Book", 'c', b."C", 'v', b."V", 'u', b."U") ORDER BY b."Book")          AS "BookCounts"
            FROM "PerBook" b
            GROUP BY b."GermanKey", b."Pos"
            WITH NO DATA;
        END IF;

        IF NOT EXISTS (SELECT 1 FROM pg_matviews WHERE schemaname = 'unshackled-word' AND matviewname = 'StatsGermanForms') THEN
            CREATE MATERIALIZED VIEW "unshackled-word"."StatsGermanForms" AS
            SELECT f."PlainLower"                               AS "PlainLower"
                 , COALESCE(f."LemmaLower", f."PlainLower")     AS "GermanKey"
                 , f."Pos"                                      AS "Pos"
                 , count(*)::int                                AS "Count"
            FROM "unshackled-word"."DqWordFacts" f
            WHERE 1=1
                AND f."GermanRowNo" = 1
                AND f."Plain" <> ''
            GROUP BY f."PlainLower", COALESCE(f."LemmaLower", f."PlainLower"), f."Pos"
            WITH NO DATA;
        END IF;
    END
$$;

-- Unique indexes are required for REFRESH MATERIALIZED VIEW CONCURRENTLY; Pos can be null, hence NULLS NOT DISTINCT.
CREATE UNIQUE INDEX IF NOT EXISTS "StatsStrongsRenderings_UQ" ON "unshackled-word"."StatsStrongsRenderings" ("Lang", "StrongsKey", "GermanKey", "Pos") NULLS NOT DISTINCT;
CREATE INDEX IF NOT EXISTS "StatsStrongsRenderings_GermanKey_idx" ON "unshackled-word"."StatsStrongsRenderings" ("GermanKey", "SourceCount" DESC);

CREATE UNIQUE INDEX IF NOT EXISTS "StatsStrongs_UQ" ON "unshackled-word"."StatsStrongs" ("Lang", "StrongsKey");

CREATE UNIQUE INDEX IF NOT EXISTS "StatsGermanLemma_UQ" ON "unshackled-word"."StatsGermanLemma" ("GermanKey", "Pos") NULLS NOT DISTINCT;

CREATE UNIQUE INDEX IF NOT EXISTS "StatsGermanForms_UQ" ON "unshackled-word"."StatsGermanForms" ("PlainLower", "GermanKey", "Pos") NULLS NOT DISTINCT;
CREATE INDEX IF NOT EXISTS "StatsGermanForms_PlainLower_idx" ON "unshackled-word"."StatsGermanForms" ("PlainLower" text_pattern_ops);

-- 0013 also created "IX_StepStrongsLexicon_Number_Language" (a table index, not affected by the drop).
