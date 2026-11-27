-- Grammar search (roadmap 0029.6): Hebrew/Aramaic grammar filters resolve in memory to the set of OSHB codes whose root segment matches
-- and are queried as "Grammar" = ANY(@codes). Without an index a query without a lemma (e.g. Stamm:piel) reads all 305,648 OT words.
-- Lemma + grammar uses the existing root Strong's index ("IX_StepHebrewWords_RootStrongs").
--
-- Idempotent: safe to run again.

CREATE INDEX IF NOT EXISTS "IX_StepHebrewWords_Grammar"
    ON "unshackled-word"."StepHebrewWords" ("Grammar", "LxxRefId");
