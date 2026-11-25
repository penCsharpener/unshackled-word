-- Greek editions (roadmap 0025 review): "GreekEditions"."Code" and "StepGreekWordEditions"."ShiftRaw" get the length of the source column
-- "StepGreekWords"."Editions" (varchar(100)). With varchar(20), a new STEP separator (one whole string read as one token) or a long suffix made
-- "RefreshStepGreekWordEditions"() fail with "value too long" instead of creating an unknown catalog row for the data-quality check (06-risks.md).
-- Widening a varchar is a catalog-only change (no table rewrite).
--
-- Idempotent: safe to run again.

ALTER TABLE "unshackled-word"."GreekEditions"
    ALTER COLUMN "Code" TYPE varchar(100);

ALTER TABLE "unshackled-word"."StepGreekWordEditions"
    ALTER COLUMN "ShiftRaw" TYPE varchar(100);
