-- Grammar search (roadmap 0029.1): a grammar filter resolves in memory to the set of Robinson codes it matches and is queried as
-- "Morphology" = ANY(@codes). Without an index on "Morphology" a query without a lemma (e.g. Kasus:nom) reads all 142,095 NT words.
-- The composite index also serves lemma + grammar ("DisambiguatedStrongs" first is covered by the existing strongs index).
--
-- Idempotent: safe to run again.

CREATE INDEX IF NOT EXISTS "IX_StepGreekWords_Morphology"
    ON "unshackled-word"."StepGreekWords" ("Morphology", "LxxRefId");
