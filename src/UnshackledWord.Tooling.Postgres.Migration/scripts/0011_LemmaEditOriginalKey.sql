-- Original-language key (G4680 / H2450 / added / none) of the edited word at edit time, for later reports
-- ("all Weise edits of sophos"). Filled by the bulk lemma edit and its revert; NULL for grid edits and older rows.

ALTER TABLE "unshackled-word"."Elb1871MorphologyEdits"
    ADD COLUMN IF NOT EXISTS "OriginalKey" text NULL;

CREATE INDEX IF NOT EXISTS "Elb1871MorphologyEdits_OriginalKey_idx" ON "unshackled-word"."Elb1871MorphologyEdits" ("OriginalKey") WHERE "OriginalKey" IS NOT NULL;
