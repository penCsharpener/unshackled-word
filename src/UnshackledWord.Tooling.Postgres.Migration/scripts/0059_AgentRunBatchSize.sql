-- Batch size of a vocab agent run (modes vocab-form, roadmap 0022.13, and vocab-root, roadmap 0030.4): entries per model call, set by the
-- maintainer when the run starts. The executor reads it from the stored run, so a resumed run keeps the same batch size.
--   "BatchSize": 1..200; NULL = the executor's default (25).
--
-- Idempotent.

ALTER TABLE "unshackled-word"."AgentRuns"
    ADD COLUMN IF NOT EXISTS "BatchSize" integer NULL;

ALTER TABLE "unshackled-word"."AgentRuns"
    DROP CONSTRAINT IF EXISTS "AgentRuns_BatchSize_CK";

ALTER TABLE "unshackled-word"."AgentRuns"
    ADD CONSTRAINT "AgentRuns_BatchSize_CK" CHECK ("BatchSize" IS NULL OR "BatchSize" BETWEEN 1 AND 200);

COMMENT ON COLUMN "unshackled-word"."AgentRuns"."BatchSize" IS 'Modes vocab-form/vocab-root: entries per model call (1..200), NULL = executor default 25.';
