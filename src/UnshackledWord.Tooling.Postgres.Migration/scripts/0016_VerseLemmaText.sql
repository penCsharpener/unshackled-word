-- Lemma-aware verse search (roadmap 0009.4): Elb1871Verses."LemmaText" = the lower-cased Elb1871Morphology lemmas of the verse in word order.
-- "SearchVector" = stems of the verse text (weight A) || lemmas (weight B), so literal hits outrank lemma-only hits.
--
-- "unshackled-word"."RefreshVerseLemmaText"(int[]) recomputes LemmaText of the given HebRefIds (all verses when NULL) and returns the number of
-- verses that changed (unchanged rows are not touched, so their SearchVector is not regenerated). Called by every code path that writes
-- Elb1871Morphology (lemma editor single/chapter save, bulk apply and revert) in the same transaction, and by the morphology seed (roadmap 0010).
-- The function is plpgsql on purpose: the body is not checked at creation time, so this script also runs on a fresh install where the
-- Elb1871Morphology table does not exist yet (LemmaText then stays NULL until the seed calls the function).
--
-- Idempotent: the column is rebuilt only while the generation expression does not mention LemmaText.

ALTER TABLE "unshackled-word"."Elb1871Verses" ADD COLUMN IF NOT EXISTS "LemmaText" text NULL;

CREATE OR REPLACE FUNCTION "unshackled-word"."RefreshVerseLemmaText"(ref_ids int[])
    RETURNS integer
    LANGUAGE plpgsql
AS
$$
DECLARE
    changed integer;
BEGIN
    UPDATE "unshackled-word"."Elb1871Verses" v
    SET "LemmaText" = l."LemmaText"
    FROM (SELECT x."HebRefId"
               , (SELECT string_agg(lower(m."Lemma"), ' ' ORDER BY m."PositionInVerse")
                  FROM "unshackled-word"."Elb1871Morphology" m
                  WHERE m."HebRefId" = x."HebRefId") AS "LemmaText"
          FROM (SELECT v2."HebRefId"
                FROM "unshackled-word"."Elb1871Verses" v2
                WHERE ref_ids IS NULL
                    OR v2."HebRefId" = ANY (ref_ids)) x) l
    WHERE v."HebRefId" = l."HebRefId"
        AND v."LemmaText" IS DISTINCT FROM l."LemmaText";

    GET DIAGNOSTICS changed = ROW_COUNT;
    RETURN changed;
END
$$;

DO
$$
    BEGIN
        IF NOT EXISTS (SELECT 1
                       FROM pg_attrdef ad
                           JOIN pg_attribute a ON a.attrelid = ad.adrelid AND a.attnum = ad.adnum
                       WHERE ad.adrelid = '"unshackled-word"."Elb1871Verses"'::regclass
                           AND a.attname = 'SearchVector'
                           AND pg_get_expr(ad.adbin, ad.adrelid) LIKE '%LemmaText%') THEN
            DROP INDEX IF EXISTS "unshackled-word"."Elb1871Verses_Search_idx";
            ALTER TABLE "unshackled-word"."Elb1871Verses" DROP COLUMN IF EXISTS "SearchVector";

            IF to_regclass('"unshackled-word"."Elb1871Morphology"') IS NOT NULL THEN
                PERFORM "unshackled-word"."RefreshVerseLemmaText"(NULL);
            END IF;

            ALTER TABLE "unshackled-word"."Elb1871Verses"
                ADD COLUMN "SearchVector" tsvector GENERATED ALWAYS AS (
                    setweight(to_tsvector('"unshackled-word".uw_german'::regconfig, "VerseText"), 'A') ||
                    setweight(to_tsvector('simple'::regconfig, coalesce("LemmaText", '')), 'B')
                    ) STORED;
        END IF;
    END
$$;

CREATE INDEX IF NOT EXISTS "Elb1871Verses_Search_idx" ON "unshackled-word"."Elb1871Verses" USING gin ("SearchVector");
