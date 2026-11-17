-- Reference-set score of an agent run (roadmap 0018.10, docs/roadmaps/0018-german-lemmas/06-ai-runs.md "Measuring and rollout").
-- The scorer result is computed on demand by GET maintainer/agent-runs/{id}/score and stored here; not part of the repo seed. Idempotent.

ALTER TABLE "unshackled-word"."AgentRuns"
    ADD COLUMN IF NOT EXISTS "Score" jsonb NULL;
ALTER TABLE "unshackled-word"."AgentRuns"
    ADD COLUMN IF NOT EXISTS "ScoredAt" timestamptz NULL;

COMMENT ON COLUMN "unshackled-word"."AgentRuns"."Score" IS 'jsonb: result of the reference-set scorer (lemma, role/morpheme accuracy, link precision/recall, flags, tokens and cost per verse, bulk-merge bar). Null until scored.';
COMMENT ON COLUMN "unshackled-word"."AgentRuns"."ScoredAt" IS 'When Score was computed.';
