-- Options of an LXX agent run (roadmap 0028.7f, docs/roadmaps/0028-lxx-rahlfs/06a-review-page.md): the executor builds the packages later from the
-- stored run, so the options of the request are kept on the run.
--   "IncludeConfirmed": also package units whose words all have confirmed tags (default: they are skipped).
--   "TextForm": 'og' / 'th' restricts a Daniel run to one text form; NULL covers both.
--
-- Idempotent.

ALTER TABLE "unshackled-word"."AgentRuns"
    ADD COLUMN IF NOT EXISTS "IncludeConfirmed" boolean NOT NULL DEFAULT false;

ALTER TABLE "unshackled-word"."AgentRuns"
    ADD COLUMN IF NOT EXISTS "TextForm" text NULL;

ALTER TABLE "unshackled-word"."AgentRuns"
    DROP CONSTRAINT IF EXISTS "AgentRuns_TextForm_CK";

ALTER TABLE "unshackled-word"."AgentRuns"
    ADD CONSTRAINT "AgentRuns_TextForm_CK" CHECK ("TextForm" IS NULL OR "TextForm" IN ('og', 'th'));

COMMENT ON COLUMN "unshackled-word"."AgentRuns"."IncludeConfirmed" IS 'Mode lxx-tag: also package units whose words all have confirmed tags (roadmap 0028.7f).';
COMMENT ON COLUMN "unshackled-word"."AgentRuns"."TextForm" IS 'Mode lxx-tag: og/th restricts a Daniel run to that text form, NULL = both (roadmap 0028.7f).';
