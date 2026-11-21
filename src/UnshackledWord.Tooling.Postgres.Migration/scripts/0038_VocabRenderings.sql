-- Vocab renderings (roadmap 0022.2, docs/roadmaps/0022-vocab-trainer/03-vocab-model.md "StatsVocabRenderings").
-- "StatsVocabRenderings": how often a Strong's key is rendered by a German word in the LIVE text, counted over head links.
--   Live text per verse: the Elberfelder 2027 variant ("RevisionVerses" of the revision with "Code" = 'elb2027') when one exists,
--   otherwise the Elb1871 mapping. A verse with a variant counts only the variant's links, never its Elb1871 links.
--   Links: "Role" = 'head', real (not added-word) links to an original word, German word not empty.
--   "StrongsKey": the morpheme Strong's of the link, built like "DqWordFacts"."MorphemeStrongs" (0032): the link's "StrongsNumber"
--     (Hebrew '#n' suffix stripped), else Greek "DisambiguatedStrongs" / Hebrew main morpheme of "ExpandedStrongTags", else the braced
--     part of "DisambiguatedStrongs". Same format as "StatsStrongsRenderings"."StrongsKey" (G3056, G0846, G3004G, H1254A).
--   "GermanKey": lower-cased lemma: the link's "PartLemma" (compound part), else the Elb1871Morphology lemma of the Elb1871 word, or for a
--     revision word with "Change" = 'same' of its base word ("BasePositionInVerse"). Otherwise the lower-cased plain form ("KeyIsForm").
--     One row per head link: a link with two head words counts twice (unlike "StatsStrongsRenderings", which joins them into one key).
--   "GermanPos": STTS tag ("Elb1871Morphology"."Stts") of the head German word, for revision words with "Change" = 'same' of the base
--     word; NULL for 'changed'/'new' revision words and words without morphology. Lets the meaning derivation drop function words
--     (VAFIN, ART, PPER, KON, ...). Note: "StatsStrongsRenderings"."Pos" is the UPOS "PartOfSpeech" of the same row, not STTS.
--   One row per (Lang, StrongsKey, GermanKey, KeyIsForm, GermanPos, FromRevision). A verse comes either from a variant or from Elb1871, so
--     "LinkCount" and "VerseCount" of the FromRevision = true/false rows of a key add up.
--
-- Reads the raw tables only, never "DqWordFacts": the documented DROP ... CASCADE of "DqWordFacts" (0013, 0032) does not reach it.
-- Only the SQL function "HebrewMorphemes" (0032) is shared.
--
-- Idempotent: safe to run again. Created WITH NO DATA; filled by POST api/maintainer/vocab/renderings/refresh (and at the end of a seed
-- rebase), which writes "DqRefreshState"."VocabRenderingsRefreshedAt". REFRESH ... CONCURRENTLY works through "StatsVocabRenderings_Row_UQ".
-- Statements run as the API's own DB user (unshackledrw owns the objects and can refresh them).

ALTER TABLE "unshackled-word"."DqRefreshState" ADD COLUMN IF NOT EXISTS "VocabRenderingsRefreshedAt" timestamptz NULL;

COMMENT ON COLUMN "unshackled-word"."DqRefreshState"."VocabRenderingsRefreshedAt" IS 'Last refresh of StatsVocabRenderings; NULL = never refreshed (the view has no data).';

DO
$$
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM pg_matviews WHERE schemaname = 'unshackled-word' AND matviewname = 'StatsVocabRenderings') THEN
            CREATE MATERIALIZED VIEW "unshackled-word"."StatsVocabRenderings" AS
            WITH "Variants" AS
            (
                -- the live Elberfelder 2027 variant of a verse (RevisionVerses holds only live rows, one per verse and revision)
                SELECT rv."Id"       AS "RevisionVerseId"
                     , rv."HebRefId" AS "HebRefId"
                FROM "unshackled-word"."RevisionVerses" rv
                    JOIN "unshackled-word"."Revisions" r ON r."Id" = rv."RevisionId"
                WHERE 1=1
                    AND r."Code" = 'elb2027'
            )
            , "Links" AS
            (
                SELECT 'G'::char(1)                                                      AS "Lang"
                     , g."StepWordId"                                                    AS "StepWordId"
                     , NULLIF(g."StrongsNumber", '')                                     AS "LinkStrongs"
                     , w."HebRefId"                                                      AS "HebRefId"
                     , w."PlainWord"                                                     AS "Plain"
                     , COALESCE(NULLIF(g."PartLemma", ''), NULLIF(m."Lemma", ''))        AS "Lemma"
                     , NULLIF(m."Stts", '')                                              AS "Pos"
                     , false                                                             AS "FromRevision"
                FROM "unshackled-word"."Elb1871GreekMapping" g
                    JOIN "unshackled-word"."Elb1871Words" w ON w."Id" = g."ElbWordId"
                    LEFT JOIN "unshackled-word"."Elb1871Morphology" m ON m."HebRefId" = w."HebRefId" AND m."PositionInVerse" = w."PositionInVerse"
                WHERE 1=1
                    AND g."Role" = 'head'
                    AND g."StepWordId" IS NOT NULL
                    AND NOT COALESCE(g."IsAddedWord", false)
                    AND NOT EXISTS (SELECT 1 FROM "Variants" v WHERE v."HebRefId" = w."HebRefId")
                UNION ALL
                SELECT 'H'::char(1)
                     , h."StepWordId"
                     , NULLIF(regexp_replace(h."StrongsNumber", '#\d+$', ''), '')
                     , w."HebRefId"
                     , w."PlainWord"
                     , COALESCE(NULLIF(h."PartLemma", ''), NULLIF(m."Lemma", ''))
                     , NULLIF(m."Stts", '')
                     , false
                FROM "unshackled-word"."Elb1871HebrewMapping" h
                    JOIN "unshackled-word"."Elb1871Words" w ON w."Id" = h."ElbWordId"
                    LEFT JOIN "unshackled-word"."Elb1871Morphology" m ON m."HebRefId" = w."HebRefId" AND m."PositionInVerse" = w."PositionInVerse"
                WHERE 1=1
                    AND h."Role" = 'head'
                    AND h."StepWordId" IS NOT NULL
                    AND NOT COALESCE(h."IsAddedWord", false)
                    AND NOT EXISTS (SELECT 1 FROM "Variants" v WHERE v."HebRefId" = w."HebRefId")
                UNION ALL
                -- revision words have no lemma of their own: 'same' words borrow the lemma of their base word, 'changed'/'new' keep the form
                SELECT rm."Lang"
                     , rm."StepWordId"
                     , NULLIF(regexp_replace(rm."StrongsNumber", '#\d+$', ''), '')
                     , v."HebRefId"
                     , rw."PlainWord"
                     , COALESCE(NULLIF(rm."PartLemma", ''), NULLIF(m."Lemma", ''))
                     , NULLIF(m."Stts", '')
                     , true
                FROM "Variants" v
                    JOIN "unshackled-word"."RevisionWords" rw ON rw."RevisionVerseId" = v."RevisionVerseId"
                    JOIN "unshackled-word"."RevisionMapping" rm ON rm."RevisionWordId" = rw."Id"
                    LEFT JOIN "unshackled-word"."Elb1871Morphology" m ON m."HebRefId" = v."HebRefId" AND m."PositionInVerse" = rw."BasePositionInVerse" AND rw."Change" = 'same'
                WHERE 1=1
                    AND rm."Role" = 'head'
                    AND rm."StepWordId" IS NOT NULL
                    AND NOT COALESCE(rm."IsAddedWord", false)
            )
            , "Keyed" AS
            (
                SELECT l."Lang"                                                                AS "Lang"
                     , COALESCE(l."LinkStrongs", gs."DisambiguatedStrongs",
                                (SELECT hm."Strongs"
                                 FROM "unshackled-word"."HebrewMorphemes"(hs."ExpandedStrongTags") hm
                                 WHERE hm."IsMain"
                                 ORDER BY hm."Ord"
                                 LIMIT 1),
                                CASE WHEN position('{' IN hs."DisambiguatedStrongs") > 0
                                     THEN NULLIF(split_part(split_part(hs."DisambiguatedStrongs", '{', 2), '}', 1), '')
                                     ELSE hs."DisambiguatedStrongs" END)::varchar                 AS "StrongsKey"
                     , lower(COALESCE(l."Lemma", l."Plain"))::varchar                          AS "GermanKey"
                     , COALESCE(l."Lemma", l."Plain")::varchar                                 AS "GermanDisplay"
                     , (l."Lemma" IS NULL)                                                     AS "KeyIsForm"
                     , l."Pos"::varchar                                                        AS "GermanPos"
                     , l."HebRefId"                                                            AS "HebRefId"
                     , l."FromRevision"                                                        AS "FromRevision"
                FROM "Links" l
                    LEFT JOIN "unshackled-word"."StepGreekWords" gs ON l."Lang" = 'G' AND gs."Id" = l."StepWordId"
                    LEFT JOIN "unshackled-word"."StepHebrewWords" hs ON l."Lang" = 'H' AND hs."Id" = l."StepWordId"
                WHERE 1=1
                    AND COALESCE(l."Plain", '') <> ''
            )
            SELECT k."Lang"                                              AS "Lang"
                 , k."StrongsKey"                                        AS "StrongsKey"
                 , k."GermanKey"                                         AS "GermanKey"
                 , mode() WITHIN GROUP (ORDER BY k."GermanDisplay")      AS "GermanDisplay"
                 , k."KeyIsForm"                                         AS "KeyIsForm"
                 , k."GermanPos"                                         AS "GermanPos"
                 , count(*)::int                                         AS "LinkCount"
                 , count(DISTINCT k."HebRefId")::int                     AS "VerseCount"
                 , k."FromRevision"                                      AS "FromRevision"
            FROM "Keyed" k
            WHERE 1=1
                AND COALESCE(k."StrongsKey", '') <> ''
            GROUP BY k."Lang", k."StrongsKey", k."GermanKey", k."KeyIsForm", k."GermanPos", k."FromRevision"
            WITH NO DATA;
        END IF;
    END
$$;

-- one row per key; also serves the lookups by (Lang, StrongsKey) of an entry's keys
CREATE UNIQUE INDEX IF NOT EXISTS "StatsVocabRenderings_Row_UQ"
    ON "unshackled-word"."StatsVocabRenderings" ("Lang", "StrongsKey", "GermanKey", "KeyIsForm", "GermanPos", "FromRevision") NULLS NOT DISTINCT;

COMMENT ON MATERIALIZED VIEW "unshackled-word"."StatsVocabRenderings" IS 'Head links of the live text (Elberfelder 2027 variant, else Elb1871) per Strong''s key and German rendering. Refreshed by the maintainer, see 0038.';
