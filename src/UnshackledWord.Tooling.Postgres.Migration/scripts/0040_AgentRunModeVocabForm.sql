-- Run mode "vocab-form" (roadmap 0022.13, docs/roadmaps/0022-vocab-trainer/08-tasks.md): an agent fills the dictionary form of vocab entries.
-- The run reuses the AgentRuns row for status, caps and token/cost totals; the verse columns carry the vocab range instead:
-- BibleBookId = VocabCourses."Id", FromHebRefId/ToHebRefId = first/last FrequencyRank of the entries asked.
-- Idempotent: the check is dropped and recreated.

ALTER TABLE "unshackled-word"."AgentRuns"
    DROP CONSTRAINT IF EXISTS "AgentRuns_Mode_CK";

ALTER TABLE "unshackled-word"."AgentRuns"
    ADD CONSTRAINT "AgentRuns_Mode_CK" CHECK ("Mode" IN ('fill-open', 'redo', 'lemma', 'vocab-form'));

COMMENT ON COLUMN "unshackled-word"."AgentRuns"."Mode" IS 'fill-open = only German words without a mapping; redo = replace the existing links of the range by a proposal; lemma = links fixed, lemmas/roles/morphemes/part lemmas decided (roadmap 0018); vocab-form = dictionary forms of vocab entries (roadmap 0022.13; BibleBookId = VocabCourses.Id, From/ToHebRefId = frequency rank range).';
