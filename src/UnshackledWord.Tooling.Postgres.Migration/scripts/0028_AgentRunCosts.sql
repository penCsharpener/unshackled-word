-- Agent worker (roadmap 0016.6): cost and call count per package (the tokens per package exist since 0025). Run totals stay on "AgentRuns".

ALTER TABLE "unshackled-word"."AgentPackages"
    ADD COLUMN IF NOT EXISTS "CostUsd" numeric(12, 4) NOT NULL DEFAULT 0;
ALTER TABLE "unshackled-word"."AgentPackages"
    ADD COLUMN IF NOT EXISTS "Calls" int4 NOT NULL DEFAULT 0;

COMMENT ON COLUMN "unshackled-word"."AgentPackages"."CostUsd" IS 'Cost of all model calls for this package (first call and retries), from the profile prices of the model.';
COMMENT ON COLUMN "unshackled-word"."AgentPackages"."Calls" IS 'Number of model calls for this package (1 + resubmissions of rejected verses).';
