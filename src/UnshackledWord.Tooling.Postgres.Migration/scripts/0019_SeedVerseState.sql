-- Per-verse state of the mapping seed (roadmap 0010.10): "ContentHash" is the hash of the verse's mapping rows (German position, part,
-- STEP verse and position, added flag, parent position) as they were right after the last seed import or update, "SeedVersion" the version
-- of the seed that wrote them. A later seed update replaces only verses whose rows still hash to "ContentHash" (nobody changed them since);
-- every other verse is kept and reported. Schema only: rows are written by the SeedDb mapping import.
--
-- Also adds the update counts to "SeedImports" for the data-quality overview (roadmap 0010.10f); NULL for imports that are not updates.

CREATE TABLE IF NOT EXISTS "unshackled-word"."SeedVerseState"
(
    "Kind"        varchar(30) NOT NULL,
    "HebRefId"    int4        NOT NULL,
    "ContentHash" varchar(64) NOT NULL,
    "SeedVersion" varchar(64) NOT NULL,
    CONSTRAINT "SeedVerseState_PK" PRIMARY KEY ("Kind", "HebRefId")
);

COMMENT ON TABLE "unshackled-word"."SeedVerseState" IS 'Hash of the rows of one verse as the last seed import or update left them; decides whether a seed update may replace the verse.';

ALTER TABLE "unshackled-word"."SeedImports"
    ADD COLUMN IF NOT EXISTS "VersesUpdated"   int4 NULL,
    ADD COLUMN IF NOT EXISTS "VersesKeptLocal" int4 NULL;

COMMENT ON COLUMN "unshackled-word"."SeedImports"."VersesUpdated" IS 'Mapping seed updates only: verses replaced by the newer seed.';
COMMENT ON COLUMN "unshackled-word"."SeedImports"."VersesKeptLocal" IS 'Mapping seed updates only: verses kept because they were changed locally since the last import.';
