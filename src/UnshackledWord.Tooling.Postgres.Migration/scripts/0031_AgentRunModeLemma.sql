-- Run mode "lemma" (roadmap 0018.8, docs/roadmaps/0018-german-lemmas/06-ai-runs.md): links are fixed, the model only decides lemmas,
-- STTS, link roles, Hebrew morphemes, part lemmas and subject pronoun -> added word. Idempotent: the check is dropped and recreated.

ALTER TABLE "unshackled-word"."AgentRuns"
    DROP CONSTRAINT IF EXISTS "AgentRuns_Mode_CK";

ALTER TABLE "unshackled-word"."AgentRuns"
    ADD CONSTRAINT "AgentRuns_Mode_CK" CHECK ("Mode" IN ('fill-open', 'redo', 'lemma'));

COMMENT ON COLUMN "unshackled-word"."AgentRuns"."Mode" IS 'fill-open = only German words without a mapping; redo = replace the existing links of the range by a proposal; lemma = links fixed, lemmas/roles/morphemes/part lemmas decided (roadmap 0018).';
