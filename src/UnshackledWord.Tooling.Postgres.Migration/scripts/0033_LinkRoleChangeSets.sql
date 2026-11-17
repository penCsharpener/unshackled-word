-- Change sets of the link-roles backfill (roadmap 0018.4, docs/roadmaps/0018-german-lemmas/05-data-model.md "Change set").
--
-- One backfill apply = one "Elb1871MorphologyChangeSets" row with "Kind" = 'linkRoles' (its undo: 'linkRolesUndo' with "RevertOf").
-- Lemma changes of rules 3b and 6 are ordinary "Elb1871MorphologyEdits" rows with that "ChangeSetId". Mapping changes go here, one row per
-- changed Elb1871GreekMapping / Elb1871HebrewMapping row, with the before and after values of every column the backfill writes, so undo
-- restores them exactly (NULLs included). The lemma page's revert refuses this kind; only the link-roles job undoes it as a whole.
--
-- Idempotent: CREATE TABLE / INDEX IF NOT EXISTS.

CREATE TABLE IF NOT EXISTS "unshackled-word"."MappingChangeSetRows"
(
    "Id"                        bigserial    NOT NULL,
    "ChangeSetId"               int4         NOT NULL,
    "Lang"                      char(1)      NOT NULL,
    "MappingId"                 int4         NOT NULL,
    "HebRefId"                  int4         NOT NULL,
    "ElbWordId"                 int4         NOT NULL,
    "Rule"                      varchar(8)   NOT NULL,
    "BeforeRole"                varchar(4)   NOT NULL,
    "BeforeStrongsNumber"       varchar(10)  NULL,
    "BeforeIsAddedWord"         boolean      NULL,
    "BeforeParentGermanWordId"  int4         NULL,
    "BeforeStepWordId"          int4         NULL,
    "AfterRole"                 varchar(4)   NOT NULL,
    "AfterStrongsNumber"        varchar(10)  NULL,
    "AfterIsAddedWord"          boolean      NULL,
    "AfterParentGermanWordId"   int4         NULL,
    "AfterStepWordId"           int4         NULL,
    CONSTRAINT "MappingChangeSetRows_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "MappingChangeSetRows_ChangeSets_FK" FOREIGN KEY ("ChangeSetId") REFERENCES "unshackled-word"."Elb1871MorphologyChangeSets" ("Id"),
    CONSTRAINT "MappingChangeSetRows_Lang_CK" CHECK ("Lang" IN ('G', 'H'))
);

CREATE INDEX IF NOT EXISTS "MappingChangeSetRows_ChangeSetId_idx" ON "unshackled-word"."MappingChangeSetRows" ("ChangeSetId", "Lang");

COMMENT ON TABLE "unshackled-word"."MappingChangeSetRows" IS 'Mapping rows changed by a link-roles backfill change set (Elb1871MorphologyChangeSets.Kind = linkRoles): before/after of Role, StrongsNumber, IsAddedWord, ParentGermanWordId, StepWordId (roadmap 0018.4).';
COMMENT ON COLUMN "unshackled-word"."MappingChangeSetRows"."Lang" IS 'G = Elb1871GreekMapping, H = Elb1871HebrewMapping.';
COMMENT ON COLUMN "unshackled-word"."MappingChangeSetRows"."Rule" IS 'Report id of the deciding rule: 1, 1-main, 2, 3, 3b, 4, 5.';
