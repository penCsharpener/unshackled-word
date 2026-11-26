-- AI tagging of the Septuagint (roadmap 0028.7, docs/roadmaps/0028-lxx-rahlfs/05-tagging.md "Pass 2").
--   AgentRuns."Mode" gains 'lxx-tag': an agent run over a Rahlfs book/chapter range whose proposals are stored in "AgentVerseProposals" with
--   "Language" = 'lxx' (word tags in "Lemmas", Hebrew links in "Links", "Flags" an object with textForm/subverse/lxxVerseId and the dropped words).
--   LxxWordTags."Strongs" accepts five digits: TBESG numbers LXX-only words from G10000 on (about 1,500 entries, e.g. G20969 πήσσω); four digits
--   only would make those words untaggable. Same shape otherwise (G + digits + optional disambiguation letter).
--
-- Idempotent: the checks are dropped and recreated.

ALTER TABLE "unshackled-word"."AgentRuns"
    DROP CONSTRAINT IF EXISTS "AgentRuns_Mode_CK";

ALTER TABLE "unshackled-word"."AgentRuns"
    ADD CONSTRAINT "AgentRuns_Mode_CK" CHECK ("Mode" IN ('fill-open', 'redo', 'lemma', 'vocab-form', 'lxx-tag'));

COMMENT ON COLUMN "unshackled-word"."AgentRuns"."Mode" IS 'fill-open = only German words without a mapping; redo = replace the existing links of the range by a proposal; lemma = links fixed, lemmas/roles/morphemes/part lemmas decided (roadmap 0018); vocab-form = dictionary forms of vocab entries (roadmap 0022.13; BibleBookId = VocabCourses.Id, From/ToHebRefId = frequency rank range); lxx-tag = lemma, Strong''s, morphology and Hebrew links of LXX words (roadmap 0028.7; From/ToHebRefId = Rahlfs BBCCCVVV range).';

ALTER TABLE "unshackled-word"."LxxWordTags"
    DROP CONSTRAINT IF EXISTS "LxxWordTags_Strongs_CK";

ALTER TABLE "unshackled-word"."LxxWordTags"
    ADD CONSTRAINT "LxxWordTags_Strongs_CK" CHECK ("Strongs" ~ '^G[0-9]{4,5}[A-Z]?$');
