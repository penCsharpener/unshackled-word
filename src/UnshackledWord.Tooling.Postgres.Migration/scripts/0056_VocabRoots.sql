-- Vocab roots (roadmap 0030.1, docs/roadmaps/0030-vocab-roots/03-data-model.md).
--   VocabRoots         one row per root (word family): Greek identity = the root's Strong's key, Hebrew = (Track, RootNormalized, Homograph)
--   VocabStrongsRoots  one row per word key: the word's root, the stripped prepositions and their keys, the origin
--   VocabEntryRoots    view: the root of an entry = the VocabStrongsRoots row of its main key (most occurrences)
-- The link goes through the word's Strong's key, not through VocabEntries."Id", so it follows the word through every vocab rebuild.
-- There is deliberately no foreign key to VocabStrongs: a retired key keeps its root and gets it back on the next rebuild.
-- Also: run mode "vocab-root" for the agent run that fills the roots the prefill leaves open (roadmap 0030.3).
--
-- Idempotent: safe to run again.

-- ---- VocabRoots ---------------------------------------------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS "unshackled-word"."VocabRoots"
(
    "Id"              serial4                          NOT NULL,
    "Track"           char(1)                          NOT NULL,
    "Root"            varchar(50)  COLLATE "und-x-icu" NOT NULL,
    "RootNormalized"  varchar(50)  COLLATE "und-x-icu" NOT NULL,
    "Homograph"       int2                             NOT NULL DEFAULT 1,
    "StrongsKey"      varchar(10)                      NULL,
    "ParentRootId"    int4                             NULL,
    "UpdatedAt"       timestamptz                      NOT NULL DEFAULT now(),
    "UpdatedByUserId" int4                             NULL,
    CONSTRAINT "VocabRoots_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "VocabRoots_Track_CK" CHECK ("Track" IN ('G', 'H')),
    CONSTRAINT "VocabRoots_Root_UQ" UNIQUE ("Track", "RootNormalized", "Homograph"),
    CONSTRAINT "VocabRoots_Greek_Key_CK" CHECK ("Track" = 'H' OR "StrongsKey" IS NOT NULL),
    CONSTRAINT "VocabRoots_Parent_FK" FOREIGN KEY ("ParentRootId") REFERENCES "unshackled-word"."VocabRoots" ("Id"),
    CONSTRAINT "VocabRoots_Parent_CK" CHECK ("ParentRootId" IS NULL OR "ParentRootId" <> "Id")
);

CREATE INDEX IF NOT EXISTS "VocabRoots_Parent_idx" ON "unshackled-word"."VocabRoots" ("ParentRootId");

CREATE UNIQUE INDEX IF NOT EXISTS "VocabRoots_StrongsKey_UQ"
    ON "unshackled-word"."VocabRoots" ("StrongsKey") WHERE "StrongsKey" IS NOT NULL;

COMMENT ON COLUMN "unshackled-word"."VocabRoots"."Root" IS 'The bare root alone: μαρτυρέω, כתב (Hebrew = root letters).';
COMMENT ON COLUMN "unshackled-word"."VocabRoots"."RootNormalized" IS 'VocabText.Normalize of Root; with Track and Homograph the stable identity of the root.';
COMMENT ON COLUMN "unshackled-word"."VocabRoots"."StrongsKey" IS 'Strong''s key of the root word (G3140, H3789); required for Greek, NULL for a Hebrew root without a verb in the corpus.';
COMMENT ON COLUMN "unshackled-word"."VocabRoots"."ParentRootId" IS 'Deeper family (μαρτυρία -> μαρτυρέω): same track, no cycles (checked by the service layer).';

-- ---- VocabStrongsRoots --------------------------------------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS "unshackled-word"."VocabStrongsRoots"
(
    "Lang"            char(1)                          NOT NULL,
    "StrongsKey"      varchar(10)                      NOT NULL,
    "VocabRootId"     int4                             NOT NULL,
    "Prefixes"        varchar(60)  COLLATE "und-x-icu" NULL,
    "PrefixKeys"      varchar(10)[]                    NULL,
    "Origin"          varchar(10)                      NOT NULL,
    "AgentRunId"      int4                             NULL,
    "UpdatedAt"       timestamptz                      NOT NULL DEFAULT now(),
    "UpdatedByUserId" int4                             NULL,
    CONSTRAINT "VocabStrongsRoots_PK" PRIMARY KEY ("Lang", "StrongsKey"),
    CONSTRAINT "VocabStrongsRoots_Root_FK" FOREIGN KEY ("VocabRootId") REFERENCES "unshackled-word"."VocabRoots" ("Id"),
    CONSTRAINT "VocabStrongsRoots_Origin_CK" CHECK ("Origin" IN ('prefill', 'agent', 'manual'))
);

CREATE INDEX IF NOT EXISTS "VocabStrongsRoots_Root_idx" ON "unshackled-word"."VocabStrongsRoots" ("VocabRootId");

COMMENT ON COLUMN "unshackled-word"."VocabStrongsRoots"."StrongsKey" IS 'The word''s key, same form as VocabStrongs."StrongsKey".';
COMMENT ON COLUMN "unshackled-word"."VocabStrongsRoots"."Prefixes" IS 'Stripped prepositions in word order (σύν + ἐπί); NULL = simple word or Hebrew.';
COMMENT ON COLUMN "unshackled-word"."VocabStrongsRoots"."PrefixKeys" IS 'Strong''s keys of the prepositions, same order as Prefixes ({G4862,G1909}).';
COMMENT ON COLUMN "unshackled-word"."VocabStrongsRoots"."Origin" IS 'prefill = Abbott-Smith split or Hebrew verb rule; agent = AI run (shown with the KI badge); manual = maintainer, never overwritten.';

-- ---- VocabEntryRoots ----------------------------------------------------------------------------------------------------------

-- The root of an entry is the VocabStrongsRoots row of its main key (most occurrences, then the lowest key).
-- IsSimple: no prefixes and the root is the entry's own main key (the list shows such a root dimmed).
CREATE OR REPLACE VIEW "unshackled-word"."VocabEntryRoots" AS
WITH main_keys AS
(
    SELECT DISTINCT ON (vs."VocabEntryId")
           vs."VocabEntryId"
         , vs."Lang"
         , vs."StrongsKey"
    FROM "unshackled-word"."VocabStrongs" vs
    ORDER BY vs."VocabEntryId", vs."Occurrences" DESC, vs."StrongsKey"
)
SELECT mk."VocabEntryId"
     , mk."StrongsKey"                                                              AS "MainStrongsKey"
     , r."Id"                                                                       AS "VocabRootId"
     , r."Track"
     , r."Root"
     , r."StrongsKey"                                                               AS "RootStrongsKey"
     , r."ParentRootId"
     , sr."Prefixes"
     , sr."PrefixKeys"
     , sr."Origin"
     , (sr."Prefixes" IS NULL AND COALESCE(r."StrongsKey" = mk."StrongsKey", false)) AS "IsSimple"
FROM main_keys mk
    JOIN "unshackled-word"."VocabStrongsRoots" sr ON sr."Lang" = mk."Lang" AND sr."StrongsKey" = mk."StrongsKey"
    JOIN "unshackled-word"."VocabRoots" r ON r."Id" = sr."VocabRootId";

-- ---- AgentRuns mode -----------------------------------------------------------------------------------------------------------

ALTER TABLE "unshackled-word"."AgentRuns"
    DROP CONSTRAINT IF EXISTS "AgentRuns_Mode_CK";

ALTER TABLE "unshackled-word"."AgentRuns"
    ADD CONSTRAINT "AgentRuns_Mode_CK" CHECK ("Mode" IN ('fill-open', 'redo', 'lemma', 'vocab-form', 'lxx-tag', 'vocab-root'));

COMMENT ON COLUMN "unshackled-word"."AgentRuns"."Mode" IS 'fill-open = only German words without a mapping; redo = replace the existing links of the range by a proposal; lemma = links fixed, lemmas/roles/morphemes/part lemmas decided (roadmap 0018); vocab-form = dictionary forms of vocab entries (roadmap 0022.13; BibleBookId = VocabCourses.Id, From/ToHebRefId = frequency rank range); lxx-tag = lemma, Strong''s, morphology and Hebrew links of LXX words (roadmap 0028.7; From/ToHebRefId = Rahlfs BBCCCVVV range); vocab-root = roots and stripped prepositions of vocab entries (roadmap 0030.3; BibleBookId = VocabCourses.Id, From/ToHebRefId = frequency rank range).';
