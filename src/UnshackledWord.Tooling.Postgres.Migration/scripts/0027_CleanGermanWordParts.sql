-- Clean up "GermanWordPart" (roadmap 0016.10): the column was filled by the old AI import with whatever the model sent. Of the 47,674
-- non-NULL values on the maintainer's database only about 550 are real compound parts ("Gersten" of "Gerstenernte"); the rest is the whole
-- word, "--", "1", "null", "*", a Greek/Hebrew word or a word that is not part of the German word at all. Read, save and export now treat the
-- column as a compound split, so the junk is cleared once.
--
-- A value stays only if it is a real part: it contains a letter, is a case-insensitive substring of the German word ("PlainWord", without
-- punctuation), and is neither the whole word nor the whole word without its hyphens. Everything else becomes NULL.
-- DESTRUCTIVE and not reversible without a backup: export the mapping seed (Wartung > Seed-Rundlauf) or dump the database before the first run.
-- Idempotent: a second run finds nothing to clear and only recomputes the same "PartOrder".
--
-- Seed state: a mapping seed update replaces a verse only while its rows still hash to "SeedVerseState"."ContentHash" (nobody edited it). Clearing
-- junk parts changes that hash for no editorial reason, so for every verse that was still unmodified before this script the stored hash is moved
-- to the new content hash (same formula as MappingVerseHash, see MappingSeedRepository). Verses that were already edited keep their old hash.
--
-- The counts are logged with RAISE NOTICE.

-- Parallel workers need shared memory that a default Docker container (64 MB /dev/shm) does not have; the hashes below touch ~700k rows.
SET LOCAL max_parallel_workers_per_gather = 0;

CREATE OR REPLACE FUNCTION pg_temp.mapping_verse_hashes(refs int4[]) RETURNS TABLE ("HebRefId" int4, "ContentHash" text)
    LANGUAGE sql
AS
$$
WITH edges AS (SELECT "ElbWordId", "StepWordId", "GermanWordPart", "IsAddedWord", "ParentGermanWordId", TRUE AS "IsGreek"
               FROM "unshackled-word"."Elb1871GreekMapping"
               UNION ALL
               SELECT "ElbWordId", "StepWordId", "GermanWordPart", "IsAddedWord", "ParentGermanWordId", FALSE
               FROM "unshackled-word"."Elb1871HebrewMapping"),
     lines AS (SELECT w."HebRefId"
                    , concat_ws('|', w."PositionInVerse", coalesce(e."GermanWordPart", ''), coalesce(sg."LxxRefId", sh."LxxRefId", 0), coalesce(sg."PositionInVerse", sh."PositionInVerse", 0),
                                coalesce(e."IsAddedWord", FALSE)::text, coalesce(p."HebRefId", 0), coalesce(p."PositionInVerse", 0)) AS "Line"
               FROM edges e
                   JOIN "unshackled-word"."Elb1871Words" w ON w."Id" = e."ElbWordId"
                   LEFT JOIN "unshackled-word"."StepGreekWords" sg ON e."IsGreek" AND sg."Id" = e."StepWordId"
                   LEFT JOIN "unshackled-word"."StepHebrewWords" sh ON NOT e."IsGreek" AND sh."Id" = e."StepWordId"
                   LEFT JOIN "unshackled-word"."Elb1871Words" p ON p."Id" = e."ParentGermanWordId"
               WHERE w."HebRefId" = ANY (refs))
SELECT l."HebRefId"
     , encode(sha256(convert_to(string_agg(l."Line", E'\n' ORDER BY l."Line" COLLATE "C"), 'UTF8')), 'hex')
FROM lines l
GROUP BY l."HebRefId"
$$;

DO
$$
    DECLARE
        v_hebrew_before int4;
        v_greek_before  int4;
        v_hebrew_junk   int4;
        v_greek_junk    int4;
        v_hebrew_kept   int4;
        v_greek_kept    int4;
        v_state_moved   int4 := 0;
    BEGIN
        SELECT count(*) INTO v_hebrew_before FROM "unshackled-word"."Elb1871HebrewMapping" WHERE "GermanWordPart" IS NOT NULL;
        SELECT count(*) INTO v_greek_before FROM "unshackled-word"."Elb1871GreekMapping" WHERE "GermanWordPart" IS NOT NULL;

        -- The rows to clear, with their verse.
        CREATE TEMP TABLE "CleanParts_Junk"
        (
            "Greek"    bool NOT NULL,
            "Id"       int4 NOT NULL,
            "HebRefId" int4 NOT NULL
        ) ON COMMIT DROP;

        INSERT INTO "CleanParts_Junk" ("Greek", "Id", "HebRefId")
        SELECT FALSE, m."Id", w."HebRefId"
        FROM "unshackled-word"."Elb1871HebrewMapping" m
            JOIN "unshackled-word"."Elb1871Words" w ON w."Id" = m."ElbWordId"
        WHERE m."GermanWordPart" IS NOT NULL
            AND NOT (m."GermanWordPart" ~ '[[:alpha:]]'
                AND position(lower(m."GermanWordPart") IN lower(coalesce(w."PlainWord", w."WordInContext"))) > 0
                AND replace(lower(m."GermanWordPart"), '-', '') <> replace(lower(coalesce(w."PlainWord", w."WordInContext")), '-', ''));

        INSERT INTO "CleanParts_Junk" ("Greek", "Id", "HebRefId")
        SELECT TRUE, m."Id", w."HebRefId"
        FROM "unshackled-word"."Elb1871GreekMapping" m
            JOIN "unshackled-word"."Elb1871Words" w ON w."Id" = m."ElbWordId"
        WHERE m."GermanWordPart" IS NOT NULL
            AND NOT (m."GermanWordPart" ~ '[[:alpha:]]'
                AND position(lower(m."GermanWordPart") IN lower(coalesce(w."PlainWord", w."WordInContext"))) > 0
                AND replace(lower(m."GermanWordPart"), '-', '') <> replace(lower(coalesce(w."PlainWord", w."WordInContext")), '-', ''));

        SELECT count(*) INTO v_hebrew_junk FROM "CleanParts_Junk" WHERE NOT "Greek";
        SELECT count(*) INTO v_greek_junk FROM "CleanParts_Junk" WHERE "Greek";

        -- Verses that were still equal to the last seed import before the change.
        CREATE TEMP TABLE "CleanParts_Unmodified"
        (
            "HebRefId" int4 PRIMARY KEY
        ) ON COMMIT DROP;

        INSERT INTO "CleanParts_Unmodified" ("HebRefId")
        SELECT s."HebRefId"
        FROM "unshackled-word"."SeedVerseState" s
            JOIN pg_temp.mapping_verse_hashes((SELECT coalesce(array_agg(DISTINCT j."HebRefId"), '{}'::int4[]) FROM "CleanParts_Junk" j)) h ON h."HebRefId" = s."HebRefId"
        WHERE s."Kind" = 'mappings'
            AND s."ContentHash" = h."ContentHash";

        UPDATE "unshackled-word"."Elb1871HebrewMapping" m
        SET "GermanWordPart" = NULL
          , "PartOrder"      = NULL
        FROM "CleanParts_Junk" j
        WHERE NOT j."Greek"
            AND j."Id" = m."Id";

        UPDATE "unshackled-word"."Elb1871GreekMapping" m
        SET "GermanWordPart" = NULL
          , "PartOrder"      = NULL
        FROM "CleanParts_Junk" j
        WHERE j."Greek"
            AND j."Id" = m."Id";

        WITH moved AS (UPDATE "unshackled-word"."SeedVerseState" s
                       SET "ContentHash" = h."ContentHash"
                       FROM pg_temp.mapping_verse_hashes((SELECT coalesce(array_agg(u."HebRefId"), '{}'::int4[]) FROM "CleanParts_Unmodified" u)) h
                       WHERE s."Kind" = 'mappings'
                           AND s."HebRefId" = h."HebRefId"
                           AND s."ContentHash" <> h."ContentHash"
                       RETURNING 1)
        SELECT count(*) INTO v_state_moved FROM moved;

        -- Order of the remaining parts inside their word: by the position of the part in the word, equal part texts (one part linked to several
        -- original words) share the order. Hebrew and Greek the same way.
        UPDATE "unshackled-word"."Elb1871HebrewMapping" m
        SET "PartOrder" = o."PartOrder"
        FROM (SELECT x."Id"
                   , dense_rank() OVER (PARTITION BY x."ElbWordId" ORDER BY position(lower(x."GermanWordPart") IN lower(coalesce(w."PlainWord", w."WordInContext"))), lower(x."GermanWordPart"))::int4 AS "PartOrder"
              FROM "unshackled-word"."Elb1871HebrewMapping" x
                  JOIN "unshackled-word"."Elb1871Words" w ON w."Id" = x."ElbWordId"
              WHERE x."GermanWordPart" IS NOT NULL) o
        WHERE o."Id" = m."Id"
            AND m."PartOrder" IS DISTINCT FROM o."PartOrder";

        UPDATE "unshackled-word"."Elb1871GreekMapping" m
        SET "PartOrder" = o."PartOrder"
        FROM (SELECT x."Id"
                   , dense_rank() OVER (PARTITION BY x."ElbWordId" ORDER BY position(lower(x."GermanWordPart") IN lower(coalesce(w."PlainWord", w."WordInContext"))), lower(x."GermanWordPart"))::int4 AS "PartOrder"
              FROM "unshackled-word"."Elb1871GreekMapping" x
                  JOIN "unshackled-word"."Elb1871Words" w ON w."Id" = x."ElbWordId"
              WHERE x."GermanWordPart" IS NOT NULL) o
        WHERE o."Id" = m."Id"
            AND m."PartOrder" IS DISTINCT FROM o."PartOrder";

        SELECT count(*) INTO v_hebrew_kept FROM "unshackled-word"."Elb1871HebrewMapping" WHERE "GermanWordPart" IS NOT NULL;
        SELECT count(*) INTO v_greek_kept FROM "unshackled-word"."Elb1871GreekMapping" WHERE "GermanWordPart" IS NOT NULL;

        RAISE NOTICE 'GermanWordPart cleanup: Hebrew % -> % (cleared %), Greek % -> % (cleared %), seed verse hashes moved: %',
            v_hebrew_before, v_hebrew_kept, v_hebrew_junk, v_greek_before, v_greek_kept, v_greek_junk, v_state_moved;
    END
$$;
