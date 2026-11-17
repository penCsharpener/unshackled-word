-- Link roles in the stats (roadmap 0018.6, docs/roadmaps/0018-german-lemmas/04-roles-and-counting.md "Counting", 05-data-model.md "Stats").
-- Supersedes "DqWordFacts" and the four Stats* views of 0014_AddedWordLinks.sql; 0014 stays as it was applied, this script drops and
-- recreates all five views.
--
-- New pieces:
--   "HebrewMorphemes"(tags)  SQL function, the single SQL parser of StepHebrewWords."ExpandedStrongTags" (mirrors the C# parsers
--                            Application/Features/LinkRoles/SourceMorphemes and VerseMapping/HebrewMorphemes): pieces split at / and \,
--                            first '=' field, braces, trailing '+' and the instance suffix '#n' stripped; punctuation H9014–H9019 dropped;
--                            duplicates keep the first; the braced piece is the main morpheme (no braced piece: first non-prefix, else first).
--   "DqWordFacts"            gains on 'aligned' rows: "Role" (head/unit), "MorphemeStrongs" (Greek: link StrongsNumber, else the word's
--                            Strong's; Hebrew: link StrongsNumber, else the main morpheme), "PartLemma", "RenderLemma" (part lemma, else
--                            lemma), "RenderLemmaLower", "MultiHead"; on every German row "Stts" (snapshot of Elb1871Morphology).
--   "StatsStrongsRenderings" one row per morpheme Strong's ("StrongsKey") and rendering key ("GermanKey" = the head lemmas of one source
--                            morpheme instance in verse order, space-joined; form when there is no lemma). "SourceCount" counts distinct
--                            instances, so the renderings of a Strong's add up to its "MappedCount". Only head rows form the key.
--                            Instances whose links are all units (contraction: "zum" is head of the preposition and unit of the article
--                            ὁ / ה) form one row with "GermanKey" = '' and "NoOwnLemma" = true ("rendered, no own lemma"); every other
--                            headless instance is listed by check R1 as well.
--   "StatsStrongs"           counts source morpheme instances: Greek one per word, Hebrew one per morpheme of ExpandedStrongTags
--                            (prefixes/suffixes like H9001 get their own rows); aligned = at least one real link on that morpheme.
--                            Main-morpheme numbers are unchanged until links name other morphemes (backfill 0018.4).
--   "StatsGermanLemma" / "StatsGermanForms" unchanged: German lemma frequency stays count(*) of German words.
--   "DqRefreshState"."LinkRolesAppliedAt" set by the link-roles backfill (0018.4); NULL = roles incomplete ("Rollen unvollständig").
--
-- Idempotent: the drop only happens while "DqWordFacts" has no "MorphemeStrongs" column. All views are WITH NO DATA again: refresh them
-- with POST api/dataQuality/refresh (or REFRESH MATERIALIZED VIEW as the owner: DqWordFacts first, then the four Stats* views).
-- "StatsRefreshedAt" is reset to NULL on the drop so the Strong's page and word stats fall back to the live SQL until the next refresh.

ALTER TABLE "unshackled-word"."DqRefreshState"
    ADD COLUMN IF NOT EXISTS "LinkRolesAppliedAt" timestamptz NULL;

CREATE OR REPLACE FUNCTION "unshackled-word"."HebrewMorphemes"("Tags" text)
    RETURNS TABLE ("Strongs" varchar, "IsMain" boolean, "Ord" int)
    LANGUAGE sql
    IMMUTABLE
    PARALLEL SAFE
AS
$$
    WITH "Pieces" AS
    (
        SELECT x."Ord"::int                                                                                                  AS "Ord"
             , position('{' IN x."Piece") > 0                                                                                AS "Braced"
             , btrim(rtrim(regexp_replace(btrim(replace(replace(split_part(x."Piece", '=', 1), '{', ''), '}', '')), '#\d+$', ''), '+')) AS "Strongs"
        FROM regexp_split_to_table(COALESCE("Tags", ''), '[/\\]') WITH ORDINALITY x("Piece", "Ord")
    )
    , "Numbered" AS
    (
        SELECT p."Ord"
             , p."Braced"
             , p."Strongs"
             , substring(p."Strongs" FROM '^[Hh](\d+)')::int AS "Num"
        FROM "Pieces" p
        WHERE 1=1
            AND p."Strongs" <> ''
    )
    , "Kept" AS
    (
        SELECT DISTINCT ON (upper(n."Strongs")) n."Ord", n."Braced", n."Strongs", n."Num"
        FROM "Numbered" n
        WHERE 1=1
            AND (n."Num" IS NULL OR n."Num" NOT BETWEEN 9014 AND 9019)
        ORDER BY upper(n."Strongs"), n."Ord"
    )
    SELECT k."Strongs"::varchar
         , CASE WHEN bool_or(k."Braced") OVER () THEN k."Braced"
                ELSE k."Ord" = COALESCE(min(k."Ord") FILTER (WHERE k."Num" IS NULL OR k."Num" < 9000) OVER (), min(k."Ord") OVER ()) END
         , (row_number() OVER (ORDER BY k."Ord"))::int
    FROM "Kept" k
    ORDER BY k."Ord"
$$;

DO
$$
    BEGIN
        IF EXISTS (SELECT 1 FROM pg_matviews WHERE schemaname = 'unshackled-word' AND matviewname = 'DqWordFacts')
            AND NOT EXISTS (SELECT 1
                            FROM pg_attribute at
                            WHERE at.attrelid = '"unshackled-word"."DqWordFacts"'::regclass
                              AND at.attname = 'MorphemeStrongs'
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
                     , NULLIF(m."Stts", '')           AS "Stts"
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
                     , NULLIF(s."DisambiguatedStrongs", '')::varchar                   AS "MainStrongs"
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
                     , (SELECT hm."Strongs" FROM "unshackled-word"."HebrewMorphemes"(s."ExpandedStrongTags") hm WHERE hm."IsMain" ORDER BY hm."Ord" LIMIT 1)
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
                     , g."Role", NULLIF(g."StrongsNumber", '') AS "StrongsNumber", NULLIF(g."PartLemma", '') AS "PartLemma", g."MultiHead"
                FROM "unshackled-word"."Elb1871GreekMapping" g
                UNION ALL
                SELECT 'H'::char(1), h."ElbWordId", h."StepWordId", h."ParentGermanWordId", h."GermanWordPart", COALESCE(h."IsAddedWord", false)
                     , h."Role", NULLIF(regexp_replace(h."StrongsNumber", '#\d+$', ''), ''), NULLIF(h."PartLemma", ''), h."MultiHead"
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
                     , g."Stts"
                     , m."Role"::varchar(4)                                              AS "Role"
                     , COALESCE(m."StrongsNumber", s."MainStrongs", s."StrongsFull")::varchar AS "MorphemeStrongs"
                     , m."PartLemma"::varchar                                            AS "PartLemma"
                     , COALESCE(m."PartLemma", g."Lemma")::varchar                       AS "RenderLemma"
                     , lower(COALESCE(m."PartLemma", g."Lemma"))::varchar                AS "RenderLemmaLower"
                     , m."MultiHead"                                                     AS "MultiHead"
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
                     , g."Stts"
                     , NULL::varchar(4), NULL::varchar, NULL::varchar, NULL::varchar, NULL::varchar, NULL::bool
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
                     , NULL::varchar
                     , NULL::varchar(4), NULL::varchar, NULL::varchar, NULL::varchar, NULL::varchar, NULL::bool
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
CREATE INDEX IF NOT EXISTS "DqWordFacts_Morpheme_idx" ON "unshackled-word"."DqWordFacts" ("Lang", "StepWordId", "MorphemeStrongs") WHERE "Kind" = 'aligned';

DO
$$
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM pg_matviews WHERE schemaname = 'unshackled-word' AND matviewname = 'StatsStrongsRenderings') THEN
            CREATE MATERIALIZED VIEW "unshackled-word"."StatsStrongsRenderings" AS
            WITH "Links" AS
            (
                -- real links per source morpheme instance (Lang, StepWordId, MorphemeStrongs); heads with an empty German word do not form a key
                SELECT f."Lang"                                                  AS "Lang"
                     , f."StepWordId"                                            AS "StepWordId"
                     , f."MorphemeStrongs"                                       AS "MorphemeStrongs"
                     , f."ElbWordId"                                             AS "ElbWordId"
                     , f."HebRefId"                                              AS "HebRefId"
                     , f."PositionInVerse"                                       AS "PositionInVerse"
                     , f."Pos"                                                   AS "Pos"
                     , COALESCE(f."RenderLemmaLower", f."PlainLower")            AS "KeyPart"
                     , COALESCE(f."RenderLemma", f."Plain")                      AS "DisplayPart"
                     , (f."RenderLemmaLower" IS NULL)                            AS "IsForm"
                     , (f."Role" = 'head' AND f."Plain" <> '')                   AS "IsHead"
                FROM "unshackled-word"."DqWordFacts" f
                WHERE 1=1
                    AND f."Kind" = 'aligned'
                    AND COALESCE(f."MorphemeStrongs", '') <> ''
            )
            , "Instances" AS
            (
                SELECT l."Lang"                                                                                                       AS "Lang"
                     , l."StepWordId"                                                                                                 AS "StepWordId"
                     , l."MorphemeStrongs"                                                                                            AS "MorphemeStrongs"
                     , COALESCE(string_agg(l."KeyPart", ' ' ORDER BY l."HebRefId", l."PositionInVerse", l."ElbWordId") FILTER (WHERE l."IsHead"), '')     AS "GermanKey"
                     , COALESCE(string_agg(l."DisplayPart", ' ' ORDER BY l."HebRefId", l."PositionInVerse", l."ElbWordId") FILTER (WHERE l."IsHead"), '') AS "GermanDisplay"
                     , CASE WHEN count(*) FILTER (WHERE l."IsHead") = 1 THEN min(l."Pos") FILTER (WHERE l."IsHead")
                            ELSE string_agg(COALESCE(l."Pos", '?'), ' ' ORDER BY l."HebRefId", l."PositionInVerse", l."ElbWordId") FILTER (WHERE l."IsHead") END AS "Pos"
                     , COALESCE(bool_and(l."IsForm") FILTER (WHERE l."IsHead"), false)                                                AS "KeyIsForm"
                     , NOT bool_or(l."IsHead")                                                                                        AS "NoOwnLemma"
                     , min(l."HebRefId")                                                                                              AS "HebRefId"
                FROM "Links" l
                GROUP BY l."Lang", l."StepWordId", l."MorphemeStrongs"
            )
            , "Keyed" AS
            (
                -- one row per instance and head word (headless instances: one row without a German word)
                SELECT i.*
                     , l."ElbWordId" AS "HeadWordId"
                FROM "Instances" i
                    LEFT JOIN "Links" l ON l."Lang" = i."Lang" AND l."StepWordId" = i."StepWordId" AND l."MorphemeStrongs" = i."MorphemeStrongs" AND l."IsHead"
            )
            SELECT k."Lang"                                                      AS "Lang"
                 , k."MorphemeStrongs"                                           AS "StrongsKey"
                 , min(substring(k."MorphemeStrongs" FROM '^[GH]\d+'))           AS "StrongsBase"
                 , k."GermanKey"                                                 AS "GermanKey"
                 , k."Pos"                                                       AS "Pos"
                 , bool_and(k."KeyIsForm")                                       AS "KeyIsForm"
                 , mode() WITHIN GROUP (ORDER BY k."GermanDisplay")              AS "GermanDisplay"
                 , count(DISTINCT k."StepWordId")::int                           AS "SourceCount"
                 , count(DISTINCT k."HeadWordId")::int                           AS "GermanCount"
                 , count(DISTINCT k."HebRefId")::int                             AS "VerseCount"
                 , bool_and(k."NoOwnLemma")                                      AS "NoOwnLemma"
            FROM "Keyed" k
            GROUP BY k."Lang", k."MorphemeStrongs", k."GermanKey", k."Pos"
            WITH NO DATA;
        END IF;

        IF NOT EXISTS (SELECT 1 FROM pg_matviews WHERE schemaname = 'unshackled-word' AND matviewname = 'StatsStrongs') THEN
            CREATE MATERIALIZED VIEW "unshackled-word"."StatsStrongs" AS
            WITH "Words" AS
            (
                -- one row per original word; its book/verse is the German side (min)
                SELECT f."Lang"                                  AS "Lang"
                     , f."StepWordId"                            AS "StepWordId"
                     , min(f."StrongsFull")                      AS "StrongsFull"
                     , min(f."BibleBookId")                      AS "Book"
                     , min(f."HebRefId")                         AS "Verse"
                FROM "unshackled-word"."DqWordFacts" f
                WHERE 1=1
                    AND f."StepWordId" IS NOT NULL
                    AND COALESCE(f."StrongsFull", '') <> ''
                GROUP BY f."Lang", f."StepWordId"
            )
            , "AlignedMorphemes" AS
            (
                SELECT DISTINCT f."Lang", f."StepWordId", f."MorphemeStrongs"
                FROM "unshackled-word"."DqWordFacts" f
                WHERE 1=1
                    AND f."Kind" = 'aligned'
                    AND COALESCE(f."MorphemeStrongs", '') <> ''
            )
            , "Morphemes" AS
            (
                -- source morpheme instances: Greek one per word, Hebrew every morpheme (no parseable tags: the word key), plus any morpheme a link names
                SELECT w."Lang", w."StepWordId", w."StrongsFull" AS "StrongsKey"
                FROM "Words" w
                WHERE w."Lang" = 'G'
                UNION
                SELECT w."Lang", w."StepWordId", COALESCE(hm."Strongs", w."StrongsFull")
                FROM "Words" w
                    JOIN "unshackled-word"."StepHebrewWords" s ON s."Id" = w."StepWordId"
                    LEFT JOIN LATERAL "unshackled-word"."HebrewMorphemes"(s."ExpandedStrongTags") hm ON true
                WHERE w."Lang" = 'H'
                UNION
                SELECT am."Lang", am."StepWordId", am."MorphemeStrongs"
                FROM "AlignedMorphemes" am
                    JOIN "Words" w ON w."Lang" = am."Lang" AND w."StepWordId" = am."StepWordId"
            )
            , "Instances" AS
            (
                SELECT m."Lang"
                     , m."StrongsKey"
                     , w."Book"
                     , w."Verse"
                     , (am."StepWordId" IS NOT NULL) AS "Aligned"
                FROM "Morphemes" m
                    JOIN "Words" w ON w."Lang" = m."Lang" AND w."StepWordId" = m."StepWordId"
                    LEFT JOIN "AlignedMorphemes" am ON am."Lang" = m."Lang" AND am."StepWordId" = m."StepWordId" AND am."MorphemeStrongs" = m."StrongsKey"
            )
            , "PerBook" AS
            (
                SELECT i."Lang"
                     , i."StrongsKey"
                     , min(substring(i."StrongsKey" FROM '^[GH]\d+'))  AS "StrongsBase"
                     , i."Book"
                     , count(*)::int                                   AS "C"
                     , count(*) FILTER (WHERE i."Aligned")::int        AS "Mapped"
                     , count(DISTINCT i."Verse")::int                  AS "V"
                FROM "Instances" i
                GROUP BY i."Lang", i."StrongsKey", i."Book"
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
