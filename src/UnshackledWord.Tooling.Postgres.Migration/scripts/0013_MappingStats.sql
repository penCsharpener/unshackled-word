-- Mapping stats snapshot (roadmap 0008): pre-aggregated German lemma + PoS <-> Strong's frequencies.
-- Four materialized views chained on "DqWordFacts" (0012); they read ONLY that view, never the raw tables.
--   StatsStrongsRenderings  (Lang, StrongsKey, GermanKey, Pos)  how often a Strong's number is rendered by a German lemma + PoS
--   StatsStrongs            (Lang, StrongsKey)                  occurrences, mapped count, verses, per-book counts of a Strong's number
--   StatsGermanLemma        (GermanKey, Pos)                    occurrences, unmapped count, verses, per-book counts of a German lemma + PoS
--   StatsGermanForms        (PlainLower, GermanKey, Pos)        inflected form -> lemma + PoS resolution
-- GermanKey = lower-cased Elb1871Morphology lemma (via DqWordFacts."LemmaLower"); without a morphology row the lower-cased form
-- (KeyIsForm = true, Pos null). Elb1871Words."Lemma" is obsolete and never read.
-- BookCounts (jsonb): [{"b": book, "c": count, "v": verses, "u": unmapped (German lemma view only)}].
--
-- NOTE: 0014_AddedWordLinks.sql recreated "DqWordFacts" and these four views (same definitions, facts without added-word links as 'aligned').
--
-- IMPORTANT: a later script that drops/recreates "DqWordFacts" (DROP ... CASCADE) drops these views as well and must recreate them.
--
-- Idempotent: safe to run again. Views are created WITH NO DATA; the API fills them in the same pipeline as DqWordFacts
-- (POST api/dataQuality/refresh). Statements run as the API's own DB user (unshackledrw owns the objects).
-- A read-only user would need SELECT on the four Stats* views and "DqRefreshState".
-- Also adds an index on StepStrongsLexicon (Number, LanguageId) for the lexicon joins of the readers (end of script).

ALTER TABLE "unshackled-word"."DqRefreshState" ADD COLUMN IF NOT EXISTS "StatsRefreshedAt" timestamptz NULL;

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

-- The readers join the lexicon entry of every listed Strong's number (word stats lists thousands for "und"); without this index each join row scans the whole lexicon.
CREATE INDEX IF NOT EXISTS "IX_StepStrongsLexicon_Number_Language" ON "unshackled-word"."StepStrongsLexicon" ("Number", "LanguageId");
