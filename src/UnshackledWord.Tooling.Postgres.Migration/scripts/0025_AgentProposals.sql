-- Agent mapping (roadmap 0016.2): runs, work packages and verse proposals of the AI agents, plus the author of a live mapping row.
-- Agents only propose; nothing here is live data and nothing here is part of the repo seed (a fresh install starts without proposals).
-- The role "ai" (AppRoles.Ai) needs no row: roles are plain strings in "UserRoles"."Role". The AI users themselves are created in a later task.
-- The ApiTokens table (MCP write access) comes with 0016.8.

-- ---- who wrote a live mapping row ---------------------------------------------------------------------------------------------
-- NULL = the repo seed, an import or a row from before this column existed. A maintainer save in the editor and a merged proposal write the user id.

ALTER TABLE "unshackled-word"."Elb1871GreekMapping"
    ADD COLUMN IF NOT EXISTS "CreatedByUserId" int4 NULL;
ALTER TABLE "unshackled-word"."Elb1871HebrewMapping"
    ADD COLUMN IF NOT EXISTS "CreatedByUserId" int4 NULL;

DO
$$
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'Elb1871GreekMapping_CreatedByUser_FK') THEN
            ALTER TABLE "unshackled-word"."Elb1871GreekMapping"
                ADD CONSTRAINT "Elb1871GreekMapping_CreatedByUser_FK" FOREIGN KEY ("CreatedByUserId") REFERENCES "unshackled-word"."Users" ("Id");
        END IF;
        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'Elb1871HebrewMapping_CreatedByUser_FK') THEN
            ALTER TABLE "unshackled-word"."Elb1871HebrewMapping"
                ADD CONSTRAINT "Elb1871HebrewMapping_CreatedByUser_FK" FOREIGN KEY ("CreatedByUserId") REFERENCES "unshackled-word"."Users" ("Id");
        END IF;
    END
$$;

COMMENT ON COLUMN "unshackled-word"."Elb1871GreekMapping"."CreatedByUserId" IS 'User who saved the row (editor save, merged agent proposal); NULL = repo seed, import or older than this column.';
COMMENT ON COLUMN "unshackled-word"."Elb1871HebrewMapping"."CreatedByUserId" IS 'User who saved the row (editor save, merged agent proposal); NULL = repo seed, import or older than this column.';

-- ---- AgentRuns ----------------------------------------------------------------------------------------------------------------
-- One run = one verse range processed with one provider profile (batch worker) or one MCP session. Token and cost totals are
-- summed from the packages; MaxTokens/MaxCostUsd are the caps copied from the profile at start (the worker stops before the
-- package that would exceed one: status "budget-exceeded", resumable with a raised cap).

CREATE TABLE IF NOT EXISTS "unshackled-word"."AgentRuns"
(
    "Id"                 int4          GENERATED ALWAYS AS IDENTITY,
    "CreatedByUserId"    int4          NOT NULL,
    "AiUserId"           int4          NOT NULL,
    "Profile"            varchar(100)  NOT NULL,
    "Model"              varchar(200)  NOT NULL,
    "PromptVersion"      varchar(50)   NOT NULL,
    "BibleBookId"        int4          NOT NULL,
    "FromHebRefId"       int4          NOT NULL,
    "ToHebRefId"         int4          NOT NULL,
    "Mode"               varchar(20)   NOT NULL,
    "Status"             varchar(30)   NOT NULL DEFAULT 'queued',
    "StatusNote"         varchar(1000) NULL,
    "InputTokens"        int8          NOT NULL DEFAULT 0,
    "CachedInputTokens"  int8          NOT NULL DEFAULT 0,
    "OutputTokens"       int8          NOT NULL DEFAULT 0,
    "CostUsd"            numeric(12, 4) NOT NULL DEFAULT 0,
    "EstimatedTokens"    int8          NULL,
    "EstimatedCostUsd"   numeric(12, 4) NULL,
    "MaxTokens"          int8          NULL,
    "MaxCostUsd"         numeric(12, 4) NULL,
    "CreatedAt"          timestamptz   NOT NULL DEFAULT now(),
    "StartedAt"          timestamptz   NULL,
    "FinishedAt"         timestamptz   NULL,
    CONSTRAINT "AgentRuns_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "AgentRuns_CreatedByUser_FK" FOREIGN KEY ("CreatedByUserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "AgentRuns_AiUser_FK" FOREIGN KEY ("AiUserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "AgentRuns_Mode_CK" CHECK ("Mode" IN ('fill-open', 'redo')),
    CONSTRAINT "AgentRuns_Status_CK" CHECK ("Status" IN ('queued', 'running', 'waiting-for-provider', 'completed', 'failed', 'cancelled', 'budget-exceeded')),
    CONSTRAINT "AgentRuns_Range_CK" CHECK ("FromHebRefId" <= "ToHebRefId")
);

CREATE INDEX IF NOT EXISTS "AgentRuns_Status_idx" ON "unshackled-word"."AgentRuns" ("Status", "Id" DESC);

COMMENT ON TABLE "unshackled-word"."AgentRuns" IS 'One agent run over a verse range (batch worker or MCP session). Local data, not part of the repo seed.';
COMMENT ON COLUMN "unshackled-word"."AgentRuns"."CreatedByUserId" IS 'Maintainer who started the run (or the agent user for an MCP session).';
COMMENT ON COLUMN "unshackled-word"."AgentRuns"."AiUserId" IS 'The AI user (role ai, one per provider profile) the proposals and merged rows are attributed to.';
COMMENT ON COLUMN "unshackled-word"."AgentRuns"."Mode" IS 'fill-open = only German words without a mapping; redo = replace the existing links of the range by a proposal.';
COMMENT ON COLUMN "unshackled-word"."AgentRuns"."Status" IS 'queued, running, waiting-for-provider (batch API), completed, failed, cancelled, budget-exceeded (a cap would be exceeded; resumable).';
COMMENT ON COLUMN "unshackled-word"."AgentRuns"."FromHebRefId" IS 'First verse of the range as BBCCCVVV; ToHebRefId is the last (inclusive).';

-- ---- AgentPackages ------------------------------------------------------------------------------------------------------------
-- A work package (a few verses with short local ids). "Id" is the public packageId handed to the model; "LocalIdMap" maps the local
-- ids back to database ids, "WordIdsHash" binds the package to the verse text it was built from (a changed text makes it stale).

CREATE TABLE IF NOT EXISTS "unshackled-word"."AgentPackages"
(
    "Id"                uuid         NOT NULL DEFAULT gen_random_uuid(),
    "RunId"             int4         NOT NULL,
    "FromHebRefId"      int4         NOT NULL,
    "ToHebRefId"        int4         NOT NULL,
    "WordIdsHash"       varchar(64)  NOT NULL,
    "LocalIdMap"        jsonb        NOT NULL,
    "Status"            varchar(20)  NOT NULL DEFAULT 'open',
    "InputTokens"       int8         NOT NULL DEFAULT 0,
    "CachedInputTokens" int8         NOT NULL DEFAULT 0,
    "OutputTokens"      int8         NOT NULL DEFAULT 0,
    "Attempts"          int4         NOT NULL DEFAULT 0,
    "CreatedAt"         timestamptz  NOT NULL DEFAULT now(),
    "ExpiresAt"         timestamptz  NOT NULL,
    CONSTRAINT "AgentPackages_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "AgentPackages_Run_FK" FOREIGN KEY ("RunId") REFERENCES "unshackled-word"."AgentRuns" ("Id") ON DELETE CASCADE,
    CONSTRAINT "AgentPackages_Status_CK" CHECK ("Status" IN ('open', 'submitted', 'completed', 'stale', 'expired', 'failed')),
    CONSTRAINT "AgentPackages_Range_CK" CHECK ("FromHebRefId" <= "ToHebRefId")
);

CREATE INDEX IF NOT EXISTS "AgentPackages_Run_idx" ON "unshackled-word"."AgentPackages" ("RunId", "FromHebRefId");

COMMENT ON TABLE "unshackled-word"."AgentPackages" IS 'Work package of an agent run; the id is the packageId the model sees. Local data, not part of the repo seed.';
COMMENT ON COLUMN "unshackled-word"."AgentPackages"."LocalIdMap" IS 'jsonb: local id (g1, s1, ...) -> database id; ids are mapped back only through this column, never parsed.';
COMMENT ON COLUMN "unshackled-word"."AgentPackages"."WordIdsHash" IS 'Hash of the word ids of the package verses; a mismatch at submit time means the text changed (status stale).';
COMMENT ON COLUMN "unshackled-word"."AgentPackages"."Status" IS 'open, submitted, completed, stale (text changed, rebuild), expired, failed.';

-- ---- AgentVerseProposals ------------------------------------------------------------------------------------------------------
-- The validated answer for one verse, with real database ids. A newer proposal for the same verse supersedes the open older one
-- (enforced by the unique index); merging writes the live tables through the normal save paths.

CREATE TABLE IF NOT EXISTS "unshackled-word"."AgentVerseProposals"
(
    "Id"               int8         GENERATED ALWAYS AS IDENTITY,
    "RunId"            int4         NOT NULL,
    "PackageId"        uuid         NOT NULL,
    "BibleBookId"      int4         NOT NULL,
    "Chapter"          int4         NOT NULL,
    "Verse"            int4         NOT NULL,
    "Language"         varchar(10)  NOT NULL,
    "Links"            jsonb        NOT NULL DEFAULT '[]'::jsonb,
    "Lemmas"           jsonb        NULL,
    "Parts"            jsonb        NULL,
    "Flags"            jsonb        NULL,
    "Note"             text         NULL,
    "Status"           varchar(20)  NOT NULL DEFAULT 'open',
    "CreatedAt"        timestamptz  NOT NULL DEFAULT now(),
    "ReviewedByUserId" int4         NULL,
    "ReviewedAt"       timestamptz  NULL,
    CONSTRAINT "AgentVerseProposals_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "AgentVerseProposals_Run_FK" FOREIGN KEY ("RunId") REFERENCES "unshackled-word"."AgentRuns" ("Id") ON DELETE CASCADE,
    CONSTRAINT "AgentVerseProposals_Package_FK" FOREIGN KEY ("PackageId") REFERENCES "unshackled-word"."AgentPackages" ("Id") ON DELETE CASCADE,
    CONSTRAINT "AgentVerseProposals_ReviewedByUser_FK" FOREIGN KEY ("ReviewedByUserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "AgentVerseProposals_Language_CK" CHECK ("Language" IN ('greek', 'hebrew')),
    CONSTRAINT "AgentVerseProposals_Status_CK" CHECK ("Status" IN ('open', 'merged', 'edited-merged', 'discarded', 'superseded'))
);

-- At most one open proposal per verse; the store marks the older one "superseded" in the same transaction.
CREATE UNIQUE INDEX IF NOT EXISTS "AgentVerseProposals_Open_UQ" ON "unshackled-word"."AgentVerseProposals" ("BibleBookId", "Chapter", "Verse") WHERE "Status" = 'open';
CREATE INDEX IF NOT EXISTS "AgentVerseProposals_Run_idx" ON "unshackled-word"."AgentVerseProposals" ("RunId", "BibleBookId", "Chapter", "Verse");

COMMENT ON TABLE "unshackled-word"."AgentVerseProposals" IS 'Validated agent answer for one verse, waiting for the maintainer. Never live data; not part of the repo seed.';
COMMENT ON COLUMN "unshackled-word"."AgentVerseProposals"."Links" IS 'jsonb array of mapping links with real ids (elbWordId, stepWordId, isAddedWord, parentElbWordId, partOrder, germanWordPart).';
COMMENT ON COLUMN "unshackled-word"."AgentVerseProposals"."Lemmas" IS 'jsonb: proposed lemma changes of the verse words.';
COMMENT ON COLUMN "unshackled-word"."AgentVerseProposals"."Parts" IS 'jsonb: compound splits as the agent sent them (merged into Links with partOrder/germanWordPart on merge).';
COMMENT ON COLUMN "unshackled-word"."AgentVerseProposals"."Flags" IS 'jsonb: soft-rule flags the reviewer sees.';
COMMENT ON COLUMN "unshackled-word"."AgentVerseProposals"."Status" IS 'open, merged, edited-merged (maintainer changed it first), discarded, superseded (a newer proposal for the verse replaced it).';
