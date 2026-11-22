-- Vocab meaning override: a maintainer may replace the shown and accepted text of a meaning (e.g. "der" -> "der, die, das")
-- without losing its derivation: "German" stays the derived text (re-derivation matches rows by it), "Count" and "Share" stay and keep
-- being refreshed. Everything that shows or accepts a meaning reads COALESCE("GermanOverride", "German").
-- Idempotent.

ALTER TABLE "unshackled-word"."VocabMeanings"
    ADD COLUMN IF NOT EXISTS "GermanOverride" varchar(80) NULL;

ALTER TABLE "unshackled-word"."VocabMeanings"
    DROP CONSTRAINT IF EXISTS "VocabMeanings_GermanOverride_CK";

ALTER TABLE "unshackled-word"."VocabMeanings"
    ADD CONSTRAINT "VocabMeanings_GermanOverride_CK" CHECK ("GermanOverride" IS NULL OR "GermanOverride" <> '');

COMMENT ON COLUMN "unshackled-word"."VocabMeanings"."GermanOverride" IS 'Text shown and accepted instead of "German" when set (maintainer edit); "German" stays the derived text, "Count"/"Share" stay.';
