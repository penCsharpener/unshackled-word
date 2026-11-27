-- Run report of the vocab root agent run (roadmap 0030.4).
--   VocabRootRunItems: one row per entry the run did not simply write: rejected by the validator, flagged for the maintainer
--   (unsure or not plausibly concatenating) or a prefill row the run replaced (option "Vorbefüllung prüfen").
-- The accepted answers are the VocabStrongsRoots rows with Origin 'agent' and this AgentRunId, so they need no report row.
--
-- Idempotent: safe to run again.

CREATE TABLE IF NOT EXISTS "unshackled-word"."VocabRootRunItems"
(
    "Id"           serial4                          NOT NULL,
    "AgentRunId"   int4                             NOT NULL,
    "VocabEntryId" int4                             NOT NULL,
    "StrongsKey"   varchar(10)                      NOT NULL,
    "Kind"         varchar(10)                      NOT NULL,
    "Reason"       text                             NOT NULL,
    "Answer"       text                             NULL,
    "CreatedAt"    timestamptz                      NOT NULL DEFAULT now(),
    CONSTRAINT "VocabRootRunItems_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "VocabRootRunItems_Run_FK" FOREIGN KEY ("AgentRunId") REFERENCES "unshackled-word"."AgentRuns" ("Id") ON DELETE CASCADE,
    CONSTRAINT "VocabRootRunItems_Entry_FK" FOREIGN KEY ("VocabEntryId") REFERENCES "unshackled-word"."VocabEntries" ("Id") ON DELETE CASCADE,
    CONSTRAINT "VocabRootRunItems_Kind_CK" CHECK ("Kind" IN ('rejected', 'flagged', 'replaced')),
    CONSTRAINT "VocabRootRunItems_Run_Entry_UQ" UNIQUE ("AgentRunId", "VocabEntryId")
);

COMMENT ON TABLE "unshackled-word"."VocabRootRunItems" IS 'Run report of mode vocab-root (roadmap 0030.4): rejected, flagged and replaced entries of one run.';
COMMENT ON COLUMN "unshackled-word"."VocabRootRunItems"."Kind" IS 'rejected = validator rule broken after the retry; flagged = unsure or no plausible concatenation (not written); replaced = a prefill row the run replaced.';
COMMENT ON COLUMN "unshackled-word"."VocabRootRunItems"."Answer" IS 'The model answer as short text (prefixes and root), for the maintainer.';
