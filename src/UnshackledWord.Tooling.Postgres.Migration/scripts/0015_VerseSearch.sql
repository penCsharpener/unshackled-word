-- Verse full text search (roadmap 0009.1): a German text search configuration WITHOUT stop words, so "ich bin", "der Herr" and "daß" are searchable.
--   "unshackled-word".uw_german_stem   snowball dictionary, language german, no StopWords option (created in SQL, no files in the Postgres share directory)
--   "unshackled-word".uw_german        copy of the stock german configuration; all word tokens go through uw_german_stem
-- Elb1871Verses."SearchVector" is rebuilt as a generated column on uw_german (same stemmer as 'german', so queries using 'german' still find the same stems).
-- 0016_VerseLemmaText.sql recreates the column once more (adds the lemma part).
--
-- Idempotent: the dictionary and the configuration are created only when missing, the column is rebuilt only while it does not use uw_german.

DO
$$
    BEGIN
        IF NOT EXISTS (SELECT 1
                       FROM pg_ts_dict d
                           JOIN pg_namespace n ON n.oid = d.dictnamespace
                       WHERE n.nspname = 'unshackled-word'
                           AND d.dictname = 'uw_german_stem') THEN
            CREATE TEXT SEARCH DICTIONARY "unshackled-word".uw_german_stem (TEMPLATE = snowball, Language = german);
        END IF;

        IF NOT EXISTS (SELECT 1
                       FROM pg_ts_config c
                           JOIN pg_namespace n ON n.oid = c.cfgnamespace
                       WHERE n.nspname = 'unshackled-word'
                           AND c.cfgname = 'uw_german') THEN
            CREATE TEXT SEARCH CONFIGURATION "unshackled-word".uw_german (COPY = pg_catalog.german);
            ALTER TEXT SEARCH CONFIGURATION "unshackled-word".uw_german
                ALTER MAPPING FOR asciiword, word, hword, hword_part, hword_asciipart, asciihword
                WITH "unshackled-word".uw_german_stem;
        END IF;

        IF NOT EXISTS (SELECT 1
                       FROM pg_attrdef ad
                           JOIN pg_attribute a ON a.attrelid = ad.adrelid AND a.attnum = ad.adnum
                       WHERE ad.adrelid = '"unshackled-word"."Elb1871Verses"'::regclass
                           AND a.attname = 'SearchVector'
                           AND pg_get_expr(ad.adbin, ad.adrelid) LIKE '%uw_german%') THEN
            DROP INDEX IF EXISTS "unshackled-word"."Elb1871Verses_Search_idx";
            ALTER TABLE "unshackled-word"."Elb1871Verses" DROP COLUMN IF EXISTS "SearchVector";
            ALTER TABLE "unshackled-word"."Elb1871Verses"
                ADD COLUMN "SearchVector" tsvector GENERATED ALWAYS AS (to_tsvector('"unshackled-word".uw_german'::regconfig, "VerseText")) STORED;
        END IF;
    END
$$;

CREATE INDEX IF NOT EXISTS "Elb1871Verses_Search_idx" ON "unshackled-word"."Elb1871Verses" USING gin ("SearchVector");
