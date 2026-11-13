-- =====================================================================================================================
-- Mapping v2: Elberfelder 1871 (German) <-> Greek (NT) / Hebrew (OT) source words
-- Draft for roadmap 0005 (docs/roadmaps/0005-mapping-redesign.md). Target script name when it ships:
--   src/UnshackledWord.Tooling.Postgres.Migration/scripts/0009_MappingV2Schema.sql
-- (0007 stays reserved for 0002 S29 and is retired by this design, 0008 belongs to roadmap 0004.)
--
-- Postgres 18+. Only plain tables, CHECK/UNIQUE/FK constraints, a few partial indexes, one exclusion constraint
-- (btree_gist, trusted extension) and one generic append-only trigger. Dapper friendly: every table has an int "Id",
-- every state is a short varchar code with a CHECK (no Postgres ENUM types, so adding a code is a one-line ALTER).
-- The script is idempotent (IF NOT EXISTS / ON CONFLICT DO NOTHING) so it can be re-run outside DbUp.
--
-- ---------------------------------------------------------------------------------------------------------------------
-- MODEL IN ONE PARAGRAPH
-- ---------------------------------------------------------------------------------------------------------------------
-- A mapping is a set of LINKS. A link is one alignment unit: a SET of German tokens <-> a SET of source tokens of one
-- source text (TAGNT or TAHOT today). 1:1, 1:n, n:1, n:m and discontinuous phrases ("er ... ging" <-> ἦλθεν) are all
-- the same shape; only the member lists differ. Every link has a KIND:
--   aligned  - >= 1 source word and >= 1 German 'core' word. German words in the link with role 'added' are words the
--              translators added to render this phrase ("des" in "des Geschlechts" <-> γενέσεως).
--   added    - German words only (all role 'added'): additions that belong to no particular source phrase.
--   covered  - source words only, no German 'core' word: the source word needs no German word because German grammar
--              covers it (article, particle, object marker, person ending...). "Reason" says why; optional German
--              members with role 'grammar' point at the German word whose inflection/word order carries it.
-- "Unmapped" is NOT a state: it is the absence of any active link for the token. That keeps "not yet mapped" and
-- "deliberately has no counterpart" (covered / added) apart without a third NULL-ish row type.
--
-- Links are IMMUTABLE once written (trigger below). A change is a CHANGESET (proposal): it adds new links and lists
-- active links it removes. Many users write changesets; only a maintainer/reviewer merges them (Linux-style). Merging
-- flips the lifecycle columns: removed links 'active' -> 'superseded', new links 'proposed' -> 'active'. Nothing is
-- deleted, so the full history is the table itself. AI output is a changeset authored by an AI user and tied to an
-- AI_RUN (model, prompt version, scope). "Let AI redo this verse range" = a new AiRun for the range; its changeset
-- supersedes older open AI changesets of that range and, on merge, replaces only unconfirmed AI-authored links.
-- Human trust is a separate, append-only CONFIRMATION log per link (confirmed / disputed / retracted).
--
-- Text revisions: Elb1871Words stays the immutable base text (level 0). A GERMAN_EDIT replaces a span of base
-- positions of one verse with new tokens (GermanEditWords) at a slider LEVEL 1..10000. Links reference either base
-- tokens or edit tokens, and carry the level they belong to ("Level"). The effective mapping at slider level L is, per
-- token, the active link with the highest Level <= L whose German tokens are all visible at L. Base links therefore
-- stay valid forever; an edit word with "InheritsFromWordId" reuses the base word's links without a new link.
--
-- Identity of tokens:
--   German base token = "Elb1871Words"."Id" (real FK; the base text is seeded once and never rewritten).
--   Source token      = "SourceWords"."Id", our own stable registry row per STEP word. The seeder numbers STEP words
--                       1..n in Bible order, so a changed STEP release shifts ids; links never point at
--                       StepGreekWords/StepHebrewWords directly. "SourceWords"."ExternalId" is the current STEP id and
--                       is re-resolved by "SyncSourceWords" (verse + position + occurrence + MatchText, then n-th
--                       occurrence of the same text in the verse) after a STEP re-import.
--   Verse ids: German side uses Elb1871Words."HebRefId", source side uses STEP "LxxRefId" (book*1000000+chapter*1000+verse).
-- =====================================================================================================================

CREATE SCHEMA IF NOT EXISTS "unshackled-word";
CREATE EXTENSION IF NOT EXISTS btree_gist;   -- only for the GermanEdits exclusion constraint (int = inside GiST)


-- =====================================================================================================================
-- 1. USERS (minimal; FK-ready for the auth system that comes with the vocab trainer)
-- =====================================================================================================================
-- Reads are anonymous, so readers never need a user row. Every write (changeset, confirmation, AI run request) needs
-- one. "ExternalSubject" is the subject/sub claim of whatever identity provider is merged later; until then the
-- maintainer row can be created by hand. Ids below 1000 are reserved for fixed technical users (seeded below) so
-- seeds, CSVs and code can name them as constants.
CREATE TABLE IF NOT EXISTS "unshackled-word"."Users"
(
    "Id"              INT GENERATED BY DEFAULT AS IDENTITY (START WITH 1000) NOT NULL,
    "UserName"        VARCHAR(100) COLLATE "und-x-icu"                       NOT NULL,
    "DisplayName"     VARCHAR(200) COLLATE "und-x-icu"                       NOT NULL,
    -- human = a person; ai = a model writer (one row per model family); rule = deterministic suggester
    -- (e.g. "articles with a mapped head are covered"); system = migrations/seed.
    "Kind"            VARCHAR(10)                                            NOT NULL,
    -- contributor = may write changesets/confirm nothing; reviewer = may confirm links; maintainer = may merge.
    "Role"            VARCHAR(12)                                            NOT NULL DEFAULT 'contributor',
    "ExternalSubject" VARCHAR(200)                                           NULL,
    "IsActive"        BOOLEAN                                                NOT NULL DEFAULT TRUE,
    "CreatedAt"       TIMESTAMP WITH TIME ZONE                               NOT NULL DEFAULT now(),
    CONSTRAINT "Users_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "Users_UserName_UQ" UNIQUE ("UserName"),
    CONSTRAINT "Users_ExternalSubject_UQ" UNIQUE ("ExternalSubject"),
    CONSTRAINT "Users_Kind_CK" CHECK ("Kind" IN ('human', 'ai', 'rule', 'system')),
    CONSTRAINT "Users_Role_CK" CHECK ("Role" IN ('contributor', 'reviewer', 'maintainer')),
    -- machines never review or merge their own output; only humans and the migration/system user may.
    CONSTRAINT "Users_MachinesAreContributors_CK" CHECK ("Kind" IN ('human', 'system') OR "Role" = 'contributor')
);

INSERT INTO "unshackled-word"."Users" ("Id", "UserName", "DisplayName", "Kind", "Role")
VALUES (1, 'system', 'System (Migration/Seed)', 'system', 'maintainer'),
       (2, 'ai-legacy', 'KI (Gemini, Altbestand vor Mapping v2)', 'ai', 'contributor'),
       (3, 'ai-gemini', 'KI (Gemini)', 'ai', 'contributor'),
       (4, 'rules', 'Regelbasierte Vorschläge', 'rule', 'contributor')
ON CONFLICT DO NOTHING;


-- =====================================================================================================================
-- 2. SOURCE SIDE: source texts (editions) and the stable source word registry
-- =====================================================================================================================
-- One row per token stream we can align against. Today: STEP TAGNT (Greek, one row per word of the union of editions,
-- edition membership in StepGreekWords."Editions"/"IsInNestleAland"/"IsInTextusReceptus") and STEP TAHOT (Hebrew,
-- Ketiv/Qere in StepHebrewWords."Type"). Later e.g. SR GNT ("SrGntWords"), Byzantine text, WLC. Edition membership of
-- a STEP word is read at query time from the STEP row, never copied (STEP data must not be redistributed/modified).
CREATE TABLE IF NOT EXISTS "unshackled-word"."SourceTexts"
(
    "Id"        SMALLINT                         NOT NULL,
    "Code"      VARCHAR(30)                      NOT NULL,
    "Language"  VARCHAR(10)                      NOT NULL,
    "Name"      VARCHAR(200) COLLATE "und-x-icu" NOT NULL,
    "WordTable" VARCHAR(100)                     NOT NULL,   -- documentation: where "SourceWords"."ExternalId" points
    CONSTRAINT "SourceTexts_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "SourceTexts_Code_UQ" UNIQUE ("Code"),
    CONSTRAINT "SourceTexts_Language_CK" CHECK ("Language" IN ('greek', 'hebrew'))
);

INSERT INTO "unshackled-word"."SourceTexts" ("Id", "Code", "Language", "Name", "WordTable")
VALUES (1, 'step-tagnt', 'greek', 'STEP Bible TAGNT (alle Editionen, NA28/TR/Byz markiert)', 'StepGreekWords'),
       (2, 'step-tahot', 'hebrew', 'STEP Bible TAHOT (Leningradensis, Ketiv/Qere markiert)', 'StepHebrewWords')
ON CONFLICT DO NOTHING;

-- Stable identity for every source token. ~448k rows (142,096 Greek + 305,652 Hebrew today).
-- Why a registry instead of pointing at STEP ids: StepDataBibleTextImporter numbers StepGreekWords/StepHebrewWords
-- 1..n in (LxxRefId, PositionInVerse) order (EnumerateWithIds). The ids are stable while the STEP file is unchanged,
-- but one inserted or removed word in a newer STEP release shifts every later id, and an FK to them would block the
-- re-import (same reason 0004 has no FK to STEP). Links get a real FK to this table; after a re-import
-- "SyncSourceWords" (below) re-resolves "ExternalId" by natural key (roadmap 0005.4). "MatchText" (STEP's
-- no-diacritics form) exists only for that re-match; it stays in the self-hoster's DB and is never exported.
-- "Occurrence" disambiguates the 75 (RefId, PositionInVerse) pairs that TAHOT has twice (e.g. 4026001 pos 1-3).
-- "RetiredAt": the STEP word is gone (removed, or its text changed). The row is never deleted (links keep their
-- history); it loses its "ExternalId" and its key no longer blocks the new word at the same position.
-- Position_UQ / External_UQ are DEFERRABLE INITIALLY IMMEDIATE: Postgres then checks them at the end of each
-- statement, not per row, so a re-match can shift the positions of a whole verse in one UPDATE. (They are no FK
-- targets; IdText_UQ, the FK target, stays non-deferrable. Deferrable constraints cannot be ON CONFLICT arbiters, so
-- writers use NOT EXISTS.)
CREATE TABLE IF NOT EXISTS "unshackled-word"."SourceWords"
(
    "Id"              INT GENERATED BY DEFAULT AS IDENTITY NOT NULL,
    "SourceTextId"    SMALLINT                             NOT NULL,
    "RefId"           INT                                  NOT NULL,   -- STEP "LxxRefId"
    "PositionInVerse" INT                                  NOT NULL,   -- current STEP position (updated by a re-match)
    "Occurrence"      SMALLINT                             NOT NULL DEFAULT 1,
    "ExternalId"      INT                                  NULL,       -- current StepGreekWords/StepHebrewWords "Id"; NULL = retired
    "MatchText"       VARCHAR(100) COLLATE "und-x-icu"     NOT NULL,
    "CreatedAt"       TIMESTAMP WITH TIME ZONE             NOT NULL DEFAULT now(),
    "RetiredAt"       TIMESTAMP WITH TIME ZONE             NULL,
    CONSTRAINT "SourceWords_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "SourceWords_SourceText_FK" FOREIGN KEY ("SourceTextId") REFERENCES "unshackled-word"."SourceTexts" ("Id"),
    -- one live row per position; retired rows (distinct "RetiredAt") do not collide with the live one
    CONSTRAINT "SourceWords_Position_UQ" UNIQUE NULLS NOT DISTINCT ("SourceTextId", "RefId", "PositionInVerse", "Occurrence", "RetiredAt")
        DEFERRABLE INITIALLY IMMEDIATE,
    CONSTRAINT "SourceWords_External_UQ" UNIQUE ("SourceTextId", "ExternalId") DEFERRABLE INITIALLY IMMEDIATE,
    -- target of the composite FK from MappingLinkSourceWords: guarantees a member's word is from the link's text
    CONSTRAINT "SourceWords_IdText_UQ" UNIQUE ("Id", "SourceTextId"),
    CONSTRAINT "SourceWords_Occurrence_CK" CHECK ("Occurrence" >= 1),
    CONSTRAINT "SourceWords_Retired_CK" CHECK ("RetiredAt" IS NULL OR "ExternalId" IS NULL)
);


-- =====================================================================================================================
-- 3. GERMAN SIDE: versions (slider presets) and text edits on top of the immutable base
-- =====================================================================================================================
-- Named slider presets. Level 0 = Elb1871 unrevised. A version shows every active edit with Level <= its level.
-- Levels 1..10000 (steps of 1000 initially, CLAUDE.md); the slider can later use any value in between.
CREATE TABLE IF NOT EXISTS "unshackled-word"."GermanVersions"
(
    "Id"          SMALLINT                          NOT NULL,
    "Code"        VARCHAR(30)                       NOT NULL,
    "Name"        VARCHAR(200) COLLATE "und-x-icu"  NOT NULL,
    "Level"       SMALLINT                          NOT NULL,
    "Description" TEXT COLLATE "und-x-icu"          NULL,
    CONSTRAINT "GermanVersions_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "GermanVersions_Code_UQ" UNIQUE ("Code"),
    CONSTRAINT "GermanVersions_Level_UQ" UNIQUE ("Level"),
    CONSTRAINT "GermanVersions_Level_CK" CHECK ("Level" BETWEEN 0 AND 10000)
);

INSERT INTO "unshackled-word"."GermanVersions" ("Id", "Code", "Name", "Level", "Description")
VALUES (0, 'elb1871', 'Elberfelder 1871', 0, 'Unrevidierter Basistext (Elb1871Words), unveränderlich.'),
       (1, 'elb-modern-literal', 'Elberfelder modernisiert (wörtlich)', 1000, 'Erste Revisionsstufe: Rechtschreibung, veraltete Wörter, Anpassung an heutige Textausgaben.')
ON CONFLICT DO NOTHING;


-- =====================================================================================================================
-- 4. AI RUNS, CHANGESETS (proposals) and their removal lists
-- =====================================================================================================================
-- One AI run = one model call series over a verse range with fixed model + prompt. Also the queue for
-- "let AI redo this verse range": the WebApi inserts a 'queued' row, Tooling.AiWorker picks it up.
-- "Parameters" is jsonb on purpose: provider-specific knobs (temperature, thinking budget, batch size) differ per
-- model and are only ever displayed/compared, never filtered on; a column per knob would churn with every provider.
CREATE TABLE IF NOT EXISTS "unshackled-word"."AiRuns"
(
    "Id"                INT GENERATED BY DEFAULT AS IDENTITY NOT NULL,
    "AiUserId"          INT                                  NOT NULL,   -- the writer (Kind 'ai' or 'rule')
    "RequestedByUserId" INT                                  NULL,       -- the human who clicked "redo"; NULL for batch/legacy runs
    "SourceTextId"      SMALLINT                             NOT NULL,
    "FromRefId"         INT                                  NOT NULL,   -- German verse range (Elb1871Words."HebRefId"), inclusive
    "ToRefId"           INT                                  NOT NULL,
    "Provider"          VARCHAR(30)                          NOT NULL,   -- 'google', 'rules', ...
    "Model"             VARCHAR(64)                          NOT NULL,   -- e.g. 'gemini-3.1-flash-lite-preview'
    "PromptVersion"     VARCHAR(30)                          NOT NULL,   -- bumped by hand when the system instruction changes
    "PromptHash"        CHAR(64)                             NULL,       -- sha256 of the exact system instruction
    "Parameters"        JSONB                                NULL,
    "Status"            VARCHAR(10)                          NOT NULL DEFAULT 'queued',
    "RequestedAt"       TIMESTAMP WITH TIME ZONE             NOT NULL DEFAULT now(),
    "StartedAt"         TIMESTAMP WITH TIME ZONE             NULL,
    "FinishedAt"        TIMESTAMP WITH TIME ZONE             NULL,
    "Error"             TEXT                                 NULL,
    "Notes"             TEXT COLLATE "und-x-icu"             NULL,
    CONSTRAINT "AiRuns_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "AiRuns_AiUser_FK" FOREIGN KEY ("AiUserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "AiRuns_RequestedBy_FK" FOREIGN KEY ("RequestedByUserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "AiRuns_SourceText_FK" FOREIGN KEY ("SourceTextId") REFERENCES "unshackled-word"."SourceTexts" ("Id"),
    CONSTRAINT "AiRuns_Range_CK" CHECK ("FromRefId" <= "ToRefId"),
    -- queued -> running -> completed | failed;  queued|running -> cancelled
    CONSTRAINT "AiRuns_Status_CK" CHECK ("Status" IN ('queued', 'running', 'completed', 'failed', 'cancelled')),
    CONSTRAINT "AiRuns_Started_CK" CHECK ("Status" = 'queued' OR "Status" = 'cancelled' OR "StartedAt" IS NOT NULL),
    CONSTRAINT "AiRuns_Finished_CK" CHECK (("Status" IN ('completed', 'failed')) = ("FinishedAt" IS NOT NULL) OR "Status" = 'cancelled')
);

CREATE INDEX IF NOT EXISTS "AiRuns_Queue_IDX" ON "unshackled-word"."AiRuns" ("RequestedAt") WHERE "Status" = 'queued';
CREATE INDEX IF NOT EXISTS "AiRuns_Range_IDX" ON "unshackled-word"."AiRuns" ("SourceTextId", "FromRefId", "ToRefId");

-- A changeset is the unit of review: "please replace these links by those". States:
--
--   draft --submit--> submitted --merge--> merged          (terminal; links become active)
--     |                  |  \--reject---> rejected        (terminal; its proposed links -> discarded)
--     \--withdraw--------+--------------> withdrawn       (terminal; same)
--                        \--newer AI run on same range--> superseded (AI changesets only; same)
--
-- The maintainer's own edits are a changeset that is submitted and merged in the same request (self-review allowed
-- for maintainers only, enforced in the service). A revert is just another changeset; there is no "reverted" state.
-- AI changesets may be auto-merged by policy (roadmap decision): they then only remove unconfirmed AI-authored links.
CREATE TABLE IF NOT EXISTS "unshackled-word"."Changesets"
(
    "Id"                      INT GENERATED BY DEFAULT AS IDENTITY NOT NULL,
    "AuthorUserId"            INT                                  NOT NULL,
    "AiRunId"                 INT                                  NULL,      -- set iff the author is a machine
    "Title"                   VARCHAR(200) COLLATE "und-x-icu"     NOT NULL,
    "Comment"                 TEXT COLLATE "und-x-icu"             NULL,
    "FromRefId"               INT                                  NULL,      -- German verse range touched (for lists, conflicts, "redo")
    "ToRefId"                 INT                                  NULL,
    "Status"                  VARCHAR(12)                          NOT NULL DEFAULT 'draft',
    "CreatedAt"               TIMESTAMP WITH TIME ZONE             NOT NULL DEFAULT now(),
    "SubmittedAt"             TIMESTAMP WITH TIME ZONE             NULL,
    "ReviewedByUserId"        INT                                  NULL,
    "ReviewedAt"              TIMESTAMP WITH TIME ZONE             NULL,
    "ReviewComment"           TEXT COLLATE "und-x-icu"             NULL,
    "MergedAt"                TIMESTAMP WITH TIME ZONE             NULL,
    "SupersededByChangesetId" INT                                  NULL,
    CONSTRAINT "Changesets_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "Changesets_Author_FK" FOREIGN KEY ("AuthorUserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "Changesets_AiRun_FK" FOREIGN KEY ("AiRunId") REFERENCES "unshackled-word"."AiRuns" ("Id"),
    CONSTRAINT "Changesets_Reviewer_FK" FOREIGN KEY ("ReviewedByUserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "Changesets_SupersededBy_FK" FOREIGN KEY ("SupersededByChangesetId") REFERENCES "unshackled-word"."Changesets" ("Id"),
    CONSTRAINT "Changesets_Status_CK" CHECK ("Status" IN ('draft', 'submitted', 'merged', 'rejected', 'withdrawn', 'superseded')),
    CONSTRAINT "Changesets_Range_CK" CHECK (num_nulls("FromRefId", "ToRefId") IN (0, 2) AND ("FromRefId" IS NULL OR "FromRefId" <= "ToRefId")),
    CONSTRAINT "Changesets_Merged_CK" CHECK (("Status" = 'merged') = ("MergedAt" IS NOT NULL)),
    CONSTRAINT "Changesets_Reviewed_CK" CHECK ("Status" NOT IN ('merged', 'rejected') OR ("ReviewedByUserId" IS NOT NULL AND "ReviewedAt" IS NOT NULL)),
    CONSTRAINT "Changesets_Superseded_CK" CHECK (("Status" = 'superseded') = ("SupersededByChangesetId" IS NOT NULL)),
    CONSTRAINT "Changesets_Submitted_CK" CHECK ("Status" IN ('draft', 'withdrawn') OR "SubmittedAt" IS NOT NULL)
);

CREATE INDEX IF NOT EXISTS "Changesets_Open_IDX" ON "unshackled-word"."Changesets" ("FromRefId", "ToRefId") WHERE "Status" IN ('draft', 'submitted');
CREATE INDEX IF NOT EXISTS "Changesets_Author_IDX" ON "unshackled-word"."Changesets" ("AuthorUserId", "CreatedAt" DESC);
CREATE INDEX IF NOT EXISTS "Changesets_AiRun_IDX" ON "unshackled-word"."Changesets" ("AiRunId") WHERE "AiRunId" IS NOT NULL;


-- =====================================================================================================================
-- 5. GERMAN EDITS (revision layer; designed now so links can reference edit tokens, implemented in a later roadmap)
-- =====================================================================================================================
-- An edit replaces base positions FromPosition..ToPosition (inclusive) of one German verse with 0..n new tokens.
--   replacement: ToPosition >= FromPosition, n >= 1        "erkannte" -> "verstand"
--   deletion:    ToPosition >= FromPosition, n = 0
--   insertion:   ToPosition = FromPosition - 1, n >= 1     new tokens go before base position FromPosition
-- Spans are always expressed against the immutable base positions (Elb1871Words."PositionInVerse"), never against
-- another edit, so edits at different levels compose without chains. Rule (service): a higher-level edit that
-- overlaps a lower-level one must cover the lower one's whole span; at slider level L the highest applicable level
-- wins. Lifecycle columns mirror MappingLinks (proposed/active/superseded/discarded via changesets).
CREATE TABLE IF NOT EXISTS "unshackled-word"."GermanEdits"
(
    "Id"                 INT GENERATED BY DEFAULT AS IDENTITY NOT NULL,
    "ChangesetId"        INT                                  NOT NULL,
    "VersionId"          SMALLINT                             NULL,      -- optional grouping (which named preset motivated it)
    "Level"              SMALLINT                             NOT NULL,
    "RefId"              INT                                  NOT NULL,  -- German verse (Elb1871Words."HebRefId")
    "FromPosition"       INT                                  NOT NULL,
    "ToPosition"         INT                                  NOT NULL,
    "Note"               TEXT COLLATE "und-x-icu"             NULL,
    "State"              VARCHAR(10)                          NOT NULL DEFAULT 'proposed',
    "IsActive"           BOOLEAN                              NOT NULL DEFAULT FALSE,
    "CreatedAt"          TIMESTAMP WITH TIME ZONE             NOT NULL DEFAULT now(),
    "ActivatedAt"        TIMESTAMP WITH TIME ZONE             NULL,
    "EndedAt"            TIMESTAMP WITH TIME ZONE             NULL,
    "EndedByChangesetId" INT                                  NULL,
    CONSTRAINT "GermanEdits_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "GermanEdits_Changeset_FK" FOREIGN KEY ("ChangesetId") REFERENCES "unshackled-word"."Changesets" ("Id"),
    CONSTRAINT "GermanEdits_Version_FK" FOREIGN KEY ("VersionId") REFERENCES "unshackled-word"."GermanVersions" ("Id"),
    CONSTRAINT "GermanEdits_EndedBy_FK" FOREIGN KEY ("EndedByChangesetId") REFERENCES "unshackled-word"."Changesets" ("Id"),
    CONSTRAINT "GermanEdits_Level_CK" CHECK ("Level" BETWEEN 1 AND 10000),
    CONSTRAINT "GermanEdits_Span_CK" CHECK ("FromPosition" >= 1 AND "ToPosition" >= "FromPosition" - 1),
    CONSTRAINT "GermanEdits_State_CK" CHECK ("State" IN ('proposed', 'active', 'superseded', 'discarded')),
    CONSTRAINT "GermanEdits_IsActive_CK" CHECK ("IsActive" = ("State" = 'active')),
    CONSTRAINT "GermanEdits_Lifecycle_CK" CHECK (
        ("State" = 'proposed' AND "ActivatedAt" IS NULL AND "EndedAt" IS NULL)
            OR ("State" = 'active' AND "ActivatedAt" IS NOT NULL AND "EndedAt" IS NULL)
            OR ("State" = 'superseded' AND "ActivatedAt" IS NOT NULL AND "EndedAt" IS NOT NULL AND "EndedByChangesetId" IS NOT NULL)
            OR ("State" = 'discarded' AND "ActivatedAt" IS NULL AND "EndedAt" IS NOT NULL)),
    -- Invariant: two active edits of the same verse and level never overlap. Range type + exclusion is the only
    -- declarative way to say "no overlap"; half-open int4range(From, To+1) is empty for insertions, which overlap
    -- nothing (two insertions at the same point are ordered by "Id").
    CONSTRAINT "GermanEdits_NoOverlap_EX" EXCLUDE USING gist (
        "RefId" WITH =, "Level" WITH =, int4range("FromPosition", "ToPosition" + 1) WITH &&) WHERE ("IsActive")
);

CREATE INDEX IF NOT EXISTS "GermanEdits_Ref_IDX" ON "unshackled-word"."GermanEdits" ("RefId", "Level") WHERE "IsActive";
CREATE INDEX IF NOT EXISTS "GermanEdits_Changeset_IDX" ON "unshackled-word"."GermanEdits" ("ChangesetId");

-- New tokens of an edit. "InheritsFromWordId": this token is a rewording of that base token (spelling, synonym) and
-- keeps its links ("erkannte" -> "verstand" keeps the link to ἔγνω). Without it the token needs its own link.
CREATE TABLE IF NOT EXISTS "unshackled-word"."GermanEditWords"
(
    "Id"                 INT GENERATED BY DEFAULT AS IDENTITY NOT NULL,
    "EditId"             INT                                  NOT NULL,
    "Ordinal"            SMALLINT                             NOT NULL,   -- 1..n inside the edit
    "WordInContext"      VARCHAR(100) COLLATE "und-x-icu"     NOT NULL,
    "PlainWord"          VARCHAR(100) COLLATE "und-x-icu"     NULL,
    "Lemma"              VARCHAR(100) COLLATE "und-x-icu"     NULL,
    "InheritsFromWordId" INT                                  NULL,
    CONSTRAINT "GermanEditWords_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "GermanEditWords_Edit_FK" FOREIGN KEY ("EditId") REFERENCES "unshackled-word"."GermanEdits" ("Id"),
    CONSTRAINT "GermanEditWords_Inherits_FK" FOREIGN KEY ("InheritsFromWordId") REFERENCES "unshackled-word"."Elb1871Words" ("Id"),
    CONSTRAINT "GermanEditWords_Ordinal_UQ" UNIQUE ("EditId", "Ordinal"),
    CONSTRAINT "GermanEditWords_Ordinal_CK" CHECK ("Ordinal" >= 1)
);


-- =====================================================================================================================
-- 6. LINKS and their members (the mapping itself)
-- =====================================================================================================================
-- Cardinality: Link 1..n German members, 0..n source members (see Kind). Per slider level, a source word is in at most
-- ONE active link, and a German token part is in at most ONE active link as 'core' or 'added' (partial unique indexes
-- on the member tables). 'grammar' members may repeat ("Gott" carries the article ὁ and is also aligned to θεός).
--
-- Link lifecycle (columns State/IsActive/ActivatedAt/EndedAt/EndedByChangesetId are the ONLY mutable ones):
--   proposed --changeset merged--> active --a later merged changeset removes it--> superseded
--   proposed --changeset rejected/withdrawn/superseded--> discarded
--
-- "Level": 0 = link between base tokens (the Elb1871 mapping). > 0 = link that involves edit tokens of that level
-- (= max level of the edits whose tokens it contains). It is stored, not derived, because the uniqueness of
-- "one active link per token" holds per level: at level 1000 "Er sagte" <-> εἶπεν may coexist with the level-0
-- "Er sprach" <-> εἶπεν. The effective mapping at slider level L picks, per token, the highest Level <= L.
--
-- AI suggestion vs human: who wrote a link = its changeset's author (AiRunId set => machine). Whether a human
-- confirmed it = MappingConfirmations. Both are orthogonal to the lifecycle: an active link can be an unconfirmed AI
-- suggestion (all legacy data after migration), a proposed link can be a human's unmerged suggestion.
CREATE TABLE IF NOT EXISTS "unshackled-word"."MappingLinks"
(
    "Id"                 INT GENERATED BY DEFAULT AS IDENTITY NOT NULL,
    "ChangesetId"        INT                                  NOT NULL,
    "SourceTextId"       SMALLINT                             NOT NULL,
    "RefId"              INT                                  NOT NULL,   -- German verse of the link (Elb1871Words."HebRefId"); 200 legacy groups span two verses: lowest wins
    "Level"              SMALLINT                             NOT NULL DEFAULT 0,
    "Kind"               VARCHAR(8)                           NOT NULL,
    "Reason"             VARCHAR(24)                          NULL,
    "Confidence"         NUMERIC(4, 3)                        NULL,       -- AI self-reported 0..1; NULL for humans and legacy
    "Note"               TEXT COLLATE "und-x-icu"             NULL,
    "State"              VARCHAR(10)                          NOT NULL DEFAULT 'proposed',
    "IsActive"           BOOLEAN                              NOT NULL DEFAULT FALSE,
    "CreatedAt"          TIMESTAMP WITH TIME ZONE             NOT NULL DEFAULT now(),
    "ActivatedAt"        TIMESTAMP WITH TIME ZONE             NULL,
    "EndedAt"            TIMESTAMP WITH TIME ZONE             NULL,
    "EndedByChangesetId" INT                                  NULL,
    CONSTRAINT "MappingLinks_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "MappingLinks_Changeset_FK" FOREIGN KEY ("ChangesetId") REFERENCES "unshackled-word"."Changesets" ("Id"),
    CONSTRAINT "MappingLinks_SourceText_FK" FOREIGN KEY ("SourceTextId") REFERENCES "unshackled-word"."SourceTexts" ("Id"),
    CONSTRAINT "MappingLinks_EndedBy_FK" FOREIGN KEY ("EndedByChangesetId") REFERENCES "unshackled-word"."Changesets" ("Id"),
    CONSTRAINT "MappingLinks_Level_CK" CHECK ("Level" BETWEEN 0 AND 10000),
    CONSTRAINT "MappingLinks_Kind_CK" CHECK ("Kind" IN ('aligned', 'added', 'covered')),
    -- Reason vocabulary per kind. covered: why the source word has no German word of its own.
    -- 'textual_variant' = the word is not in the edition the German follows (e.g. TR-only word, Elb1871 follows
    -- another reading); 'untranslated' = translator left it out (δέ, μέν). added: why the German word was added.
    -- ("Reason" IS NOT NULL is needed: NULL IN (...) is NULL, and a CHECK that yields NULL passes.)
    CONSTRAINT "MappingLinks_Reason_CK" CHECK (
        ("Kind" = 'aligned' AND "Reason" IS NULL)
            OR ("Kind" = 'covered' AND "Reason" IS NOT NULL AND "Reason" IN ('article', 'particle', 'conjunction', 'object_marker', 'inflection',
                                                    'pronoun_in_verb', 'preposition_in_case', 'idiom', 'textual_variant',
                                                    'untranslated', 'other'))
            OR ("Kind" = 'added' AND ("Reason" IS NULL OR "Reason" IN ('article', 'auxiliary', 'pronoun', 'clarification',
                                                                      'idiom', 'punctuation', 'other')))),
    CONSTRAINT "MappingLinks_Confidence_CK" CHECK ("Confidence" IS NULL OR "Confidence" BETWEEN 0 AND 1),
    CONSTRAINT "MappingLinks_State_CK" CHECK ("State" IN ('proposed', 'active', 'superseded', 'discarded')),
    CONSTRAINT "MappingLinks_IsActive_CK" CHECK ("IsActive" = ("State" = 'active')),
    CONSTRAINT "MappingLinks_Lifecycle_CK" CHECK (
        ("State" = 'proposed' AND "ActivatedAt" IS NULL AND "EndedAt" IS NULL)
            OR ("State" = 'active' AND "ActivatedAt" IS NOT NULL AND "EndedAt" IS NULL)
            OR ("State" = 'superseded' AND "ActivatedAt" IS NOT NULL AND "EndedAt" IS NOT NULL AND "EndedByChangesetId" IS NOT NULL)
            OR ("State" = 'discarded' AND "ActivatedAt" IS NULL AND "EndedAt" IS NOT NULL)),
    -- Targets of the composite FKs from the member tables. They copy (Level, IsActive[, SourceTextId]) and get them
    -- updated by ON UPDATE CASCADE, so the per-level partial unique indexes on members always see the link's state.
    CONSTRAINT "MappingLinks_IdLevelActive_UQ" UNIQUE ("Id", "Level", "IsActive"),
    CONSTRAINT "MappingLinks_IdTextLevelActive_UQ" UNIQUE ("Id", "SourceTextId", "Level", "IsActive")
);

CREATE INDEX IF NOT EXISTS "MappingLinks_ActiveRef_IDX" ON "unshackled-word"."MappingLinks" ("RefId", "SourceTextId") WHERE "IsActive";
CREATE INDEX IF NOT EXISTS "MappingLinks_Ref_IDX" ON "unshackled-word"."MappingLinks" ("RefId");
CREATE INDEX IF NOT EXISTS "MappingLinks_Changeset_IDX" ON "unshackled-word"."MappingLinks" ("ChangesetId");

-- German members. Exactly one of BaseWordId / EditWordId. "PartIndex" 0 = whole token; 1..n = n-th part of a compound
-- that maps to different source words ("Schaf|hirten": two links, PartIndex 1 and 2). "PartText" is the part's letters.
-- Role: core = renders the source words; added = added by the translators inside this phrase; grammar = (covered
-- links only) the German word whose inflection/word order carries the covered source word.
CREATE TABLE IF NOT EXISTS "unshackled-word"."MappingLinkGermanWords"
(
    "Id"         INT GENERATED BY DEFAULT AS IDENTITY NOT NULL,
    "LinkId"     INT                                  NOT NULL,
    "LinkLevel"  SMALLINT                             NOT NULL,   -- copy of MappingLinks."Level" (FK-maintained)
    "IsActive"   BOOLEAN                              NOT NULL,   -- copy of MappingLinks."IsActive" (FK-maintained)
    "BaseWordId" INT                                  NULL,
    "EditWordId" INT                                  NULL,
    "PartIndex"  SMALLINT                             NOT NULL DEFAULT 0,
    "PartText"   VARCHAR(60) COLLATE "und-x-icu"      NULL,
    "Role"       VARCHAR(8)                           NOT NULL DEFAULT 'core',
    CONSTRAINT "MappingLinkGermanWords_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "MappingLinkGermanWords_Link_FK" FOREIGN KEY ("LinkId", "LinkLevel", "IsActive")
        REFERENCES "unshackled-word"."MappingLinks" ("Id", "Level", "IsActive") ON UPDATE CASCADE,
    CONSTRAINT "MappingLinkGermanWords_BaseWord_FK" FOREIGN KEY ("BaseWordId") REFERENCES "unshackled-word"."Elb1871Words" ("Id"),
    CONSTRAINT "MappingLinkGermanWords_EditWord_FK" FOREIGN KEY ("EditWordId") REFERENCES "unshackled-word"."GermanEditWords" ("Id"),
    CONSTRAINT "MappingLinkGermanWords_OneToken_CK" CHECK (num_nonnulls("BaseWordId", "EditWordId") = 1),
    CONSTRAINT "MappingLinkGermanWords_Part_CK" CHECK ("PartIndex" >= 0 AND (("PartIndex" = 0) = ("PartText" IS NULL))),
    CONSTRAINT "MappingLinkGermanWords_Role_CK" CHECK ("Role" IN ('core', 'added', 'grammar')),
    CONSTRAINT "MappingLinkGermanWords_Member_UQ" UNIQUE NULLS NOT DISTINCT ("LinkId", "BaseWordId", "EditWordId", "PartIndex")
);

-- Invariant: one active core/added link per German token part and level.
CREATE UNIQUE INDEX IF NOT EXISTS "MappingLinkGermanWords_ActiveBase_UX"
    ON "unshackled-word"."MappingLinkGermanWords" ("BaseWordId", "PartIndex", "LinkLevel")
    WHERE "IsActive" AND "Role" <> 'grammar' AND "BaseWordId" IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS "MappingLinkGermanWords_ActiveEdit_UX"
    ON "unshackled-word"."MappingLinkGermanWords" ("EditWordId", "PartIndex", "LinkLevel")
    WHERE "IsActive" AND "Role" <> 'grammar' AND "EditWordId" IS NOT NULL;
CREATE INDEX IF NOT EXISTS "MappingLinkGermanWords_BaseWord_IDX" ON "unshackled-word"."MappingLinkGermanWords" ("BaseWordId");
CREATE INDEX IF NOT EXISTS "MappingLinkGermanWords_EditWord_IDX" ON "unshackled-word"."MappingLinkGermanWords" ("EditWordId") WHERE "EditWordId" IS NOT NULL;

-- Source members. The composite FKs make it impossible to put a TAHOT word into a TAGNT link.
CREATE TABLE IF NOT EXISTS "unshackled-word"."MappingLinkSourceWords"
(
    "Id"           INT GENERATED BY DEFAULT AS IDENTITY NOT NULL,
    "LinkId"       INT                                  NOT NULL,
    "SourceTextId" SMALLINT                             NOT NULL,
    "LinkLevel"    SMALLINT                             NOT NULL,
    "IsActive"     BOOLEAN                              NOT NULL,
    "SourceWordId" INT                                  NOT NULL,
    CONSTRAINT "MappingLinkSourceWords_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "MappingLinkSourceWords_Link_FK" FOREIGN KEY ("LinkId", "SourceTextId", "LinkLevel", "IsActive")
        REFERENCES "unshackled-word"."MappingLinks" ("Id", "SourceTextId", "Level", "IsActive") ON UPDATE CASCADE,
    CONSTRAINT "MappingLinkSourceWords_SourceWord_FK" FOREIGN KEY ("SourceWordId", "SourceTextId")
        REFERENCES "unshackled-word"."SourceWords" ("Id", "SourceTextId"),
    CONSTRAINT "MappingLinkSourceWords_Member_UQ" UNIQUE ("LinkId", "SourceWordId")
);

-- Invariant: one active link per source word and level.
CREATE UNIQUE INDEX IF NOT EXISTS "MappingLinkSourceWords_Active_UX"
    ON "unshackled-word"."MappingLinkSourceWords" ("SourceWordId", "LinkLevel") WHERE "IsActive";
CREATE INDEX IF NOT EXISTS "MappingLinkSourceWords_SourceWord_IDX" ON "unshackled-word"."MappingLinkSourceWords" ("SourceWordId");

-- Links (or edits) an un-merged changeset will end when merged. Additions need no table: they are the changeset's
-- own 'proposed' rows. Merge order (one transaction, see roadmap 0005.6): 1) check every listed link/edit is still
-- active (else conflict: someone merged first), 2) end them, 3) activate the new rows. The partial unique indexes
-- cannot be deferred, so the order 2 before 3 matters.
CREATE TABLE IF NOT EXISTS "unshackled-word"."ChangesetRemovals"
(
    "Id"          INT GENERATED BY DEFAULT AS IDENTITY NOT NULL,
    "ChangesetId" INT                                  NOT NULL,
    "LinkId"      INT                                  NULL,
    "EditId"      INT                                  NULL,
    CONSTRAINT "ChangesetRemovals_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "ChangesetRemovals_Changeset_FK" FOREIGN KEY ("ChangesetId") REFERENCES "unshackled-word"."Changesets" ("Id"),
    CONSTRAINT "ChangesetRemovals_Link_FK" FOREIGN KEY ("LinkId") REFERENCES "unshackled-word"."MappingLinks" ("Id"),
    CONSTRAINT "ChangesetRemovals_Edit_FK" FOREIGN KEY ("EditId") REFERENCES "unshackled-word"."GermanEdits" ("Id"),
    CONSTRAINT "ChangesetRemovals_One_CK" CHECK (num_nonnulls("LinkId", "EditId") = 1),
    CONSTRAINT "ChangesetRemovals_Link_UQ" UNIQUE ("ChangesetId", "LinkId"),
    CONSTRAINT "ChangesetRemovals_Edit_UQ" UNIQUE ("ChangesetId", "EditId")
);

CREATE INDEX IF NOT EXISTS "ChangesetRemovals_Link_IDX" ON "unshackled-word"."ChangesetRemovals" ("LinkId") WHERE "LinkId" IS NOT NULL;


-- =====================================================================================================================
-- 7. CONFIRMATIONS (human trust, append-only)
-- =====================================================================================================================
-- A reviewer/maintainer says "this link is right" (confirmed), "this is wrong" (disputed) or takes back an earlier
-- verdict (retracted). The latest verdict per link by a reviewer/maintainer counts (view MappingLinkTrust).
-- Confirming an AI link without changing it is just a row here; no new link. Merging a human-authored changeset
-- writes a 'confirmed' row by the merging maintainer for each new link (review = confirmation).
CREATE TABLE IF NOT EXISTS "unshackled-word"."MappingConfirmations"
(
    "Id"        INT GENERATED BY DEFAULT AS IDENTITY NOT NULL,
    "LinkId"    INT                                  NOT NULL,
    "UserId"    INT                                  NOT NULL,
    "Verdict"   VARCHAR(10)                          NOT NULL,
    "Comment"   TEXT COLLATE "und-x-icu"             NULL,
    "CreatedAt" TIMESTAMP WITH TIME ZONE             NOT NULL DEFAULT now(),
    CONSTRAINT "MappingConfirmations_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "MappingConfirmations_Link_FK" FOREIGN KEY ("LinkId") REFERENCES "unshackled-word"."MappingLinks" ("Id"),
    CONSTRAINT "MappingConfirmations_User_FK" FOREIGN KEY ("UserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "MappingConfirmations_Verdict_CK" CHECK ("Verdict" IN ('confirmed', 'disputed', 'retracted'))
);

CREATE INDEX IF NOT EXISTS "MappingConfirmations_Link_IDX" ON "unshackled-word"."MappingConfirmations" ("LinkId", "CreatedAt" DESC, "Id" DESC);


-- =====================================================================================================================
-- 8. MIGRATION BOOKKEEPING (one row per legacy Elb1871GreekMapping / Elb1871HebrewMapping row)
-- =====================================================================================================================
-- Proves that every legacy row was either turned into a member of a link or deliberately dropped, and lets the
-- word-detail "history" show "from legacy row #123". Written once by mapping-v2-migrate.sql; its existence is also
-- the migration's idempotency guard. Treatment codes are listed in that script.
CREATE TABLE IF NOT EXISTS "unshackled-word"."MappingLegacyRows"
(
    "Language"     VARCHAR(10) NOT NULL,
    "LegacyId"     INT         NOT NULL,
    "LinkId"       INT         NULL,
    "Treatment"    VARCHAR(40) NOT NULL,
    "PartDropped"  BOOLEAN     NOT NULL DEFAULT FALSE,   -- GermanWordPart was whole-word noise or '*'
    CONSTRAINT "MappingLegacyRows_PK" PRIMARY KEY ("Language", "LegacyId"),
    CONSTRAINT "MappingLegacyRows_Link_FK" FOREIGN KEY ("LinkId") REFERENCES "unshackled-word"."MappingLinks" ("Id"),
    CONSTRAINT "MappingLegacyRows_Language_CK" CHECK ("Language" IN ('greek', 'hebrew'))
);

CREATE INDEX IF NOT EXISTS "MappingLegacyRows_Link_IDX" ON "unshackled-word"."MappingLegacyRows" ("LinkId");


-- =====================================================================================================================
-- 9. APPEND-ONLY GUARD
-- =====================================================================================================================
-- History is only trustworthy if content cannot be rewritten. The trigger rejects DELETE, TRUNCATE and any UPDATE
-- that changes a column not listed in the trigger arguments. (to_jsonb(row) - text[] removes the allowed keys before
-- comparing.) TRUNCATE fires no row triggers, hence the extra statement triggers; they also stop
-- "TRUNCATE Elb1871Words CASCADE" from wiping the mapping through the member FK.
-- Escape hatch for scratch DBs/tests only: SET unshackled_word.allow_delete = 'on' (session) permits DELETE/TRUNCATE.
CREATE OR REPLACE FUNCTION "unshackled-word"."GuardAppendOnly"() RETURNS trigger
    LANGUAGE plpgsql AS
$$
BEGIN
    IF TG_OP IN ('DELETE', 'TRUNCATE') THEN
        IF current_setting('unshackled_word.allow_delete', TRUE) = 'on' THEN
            RETURN OLD;
        END IF;
        RAISE EXCEPTION '% is append-only: % not allowed', TG_TABLE_NAME, TG_OP;
    END IF;
    -- COALESCE: without trigger arguments TG_ARGV may be NULL, and jsonb - NULL would hide every change.
    IF (to_jsonb(OLD) - COALESCE(TG_ARGV, '{}'::text[])) IS DISTINCT FROM (to_jsonb(NEW) - COALESCE(TG_ARGV, '{}'::text[])) THEN
        RAISE EXCEPTION '% is append-only: only % may change', TG_TABLE_NAME, COALESCE(TG_ARGV, '{}'::text[]);
    END IF;
    RETURN NEW;
END;
$$;

CREATE OR REPLACE TRIGGER "MappingLinks_AppendOnly_TR"
    BEFORE UPDATE OR DELETE ON "unshackled-word"."MappingLinks"
    FOR EACH ROW EXECUTE FUNCTION "unshackled-word"."GuardAppendOnly"('State', 'IsActive', 'ActivatedAt', 'EndedAt', 'EndedByChangesetId');
CREATE OR REPLACE TRIGGER "MappingLinkGermanWords_AppendOnly_TR"
    BEFORE UPDATE OR DELETE ON "unshackled-word"."MappingLinkGermanWords"
    FOR EACH ROW EXECUTE FUNCTION "unshackled-word"."GuardAppendOnly"('IsActive');
CREATE OR REPLACE TRIGGER "MappingLinkSourceWords_AppendOnly_TR"
    BEFORE UPDATE OR DELETE ON "unshackled-word"."MappingLinkSourceWords"
    FOR EACH ROW EXECUTE FUNCTION "unshackled-word"."GuardAppendOnly"('IsActive');
CREATE OR REPLACE TRIGGER "GermanEdits_AppendOnly_TR"
    BEFORE UPDATE OR DELETE ON "unshackled-word"."GermanEdits"
    FOR EACH ROW EXECUTE FUNCTION "unshackled-word"."GuardAppendOnly"('State', 'IsActive', 'ActivatedAt', 'EndedAt', 'EndedByChangesetId');
CREATE OR REPLACE TRIGGER "GermanEditWords_AppendOnly_TR"
    BEFORE UPDATE OR DELETE ON "unshackled-word"."GermanEditWords"
    FOR EACH ROW EXECUTE FUNCTION "unshackled-word"."GuardAppendOnly"();
CREATE OR REPLACE TRIGGER "MappingConfirmations_AppendOnly_TR"
    BEFORE UPDATE OR DELETE ON "unshackled-word"."MappingConfirmations"
    FOR EACH ROW EXECUTE FUNCTION "unshackled-word"."GuardAppendOnly"();
-- SourceWords is not fully append-only: a re-match after a STEP re-import may change the STEP id, the position and
-- the retirement mark; identity ("Id", text, verse, "MatchText") never changes and rows are never deleted.
CREATE OR REPLACE TRIGGER "SourceWords_AppendOnly_TR"
    BEFORE UPDATE OR DELETE ON "unshackled-word"."SourceWords"
    FOR EACH ROW EXECUTE FUNCTION "unshackled-word"."GuardAppendOnly"('ExternalId', 'PositionInVerse', 'Occurrence', 'RetiredAt');
CREATE OR REPLACE TRIGGER "MappingLinks_NoTruncate_TR"
    BEFORE TRUNCATE ON "unshackled-word"."MappingLinks"
    FOR EACH STATEMENT EXECUTE FUNCTION "unshackled-word"."GuardAppendOnly"();
CREATE OR REPLACE TRIGGER "MappingLinkGermanWords_NoTruncate_TR"
    BEFORE TRUNCATE ON "unshackled-word"."MappingLinkGermanWords"
    FOR EACH STATEMENT EXECUTE FUNCTION "unshackled-word"."GuardAppendOnly"();
CREATE OR REPLACE TRIGGER "MappingLinkSourceWords_NoTruncate_TR"
    BEFORE TRUNCATE ON "unshackled-word"."MappingLinkSourceWords"
    FOR EACH STATEMENT EXECUTE FUNCTION "unshackled-word"."GuardAppendOnly"();
CREATE OR REPLACE TRIGGER "GermanEdits_NoTruncate_TR"
    BEFORE TRUNCATE ON "unshackled-word"."GermanEdits"
    FOR EACH STATEMENT EXECUTE FUNCTION "unshackled-word"."GuardAppendOnly"();
CREATE OR REPLACE TRIGGER "GermanEditWords_NoTruncate_TR"
    BEFORE TRUNCATE ON "unshackled-word"."GermanEditWords"
    FOR EACH STATEMENT EXECUTE FUNCTION "unshackled-word"."GuardAppendOnly"();
CREATE OR REPLACE TRIGGER "MappingConfirmations_NoTruncate_TR"
    BEFORE TRUNCATE ON "unshackled-word"."MappingConfirmations"
    FOR EACH STATEMENT EXECUTE FUNCTION "unshackled-word"."GuardAppendOnly"();
CREATE OR REPLACE TRIGGER "SourceWords_NoTruncate_TR"
    BEFORE TRUNCATE ON "unshackled-word"."SourceWords"
    FOR EACH STATEMENT EXECUTE FUNCTION "unshackled-word"."GuardAppendOnly"();


-- =====================================================================================================================
-- 9a. SOURCE WORD SYNC (registry fill and re-match after a STEP re-import)
-- =====================================================================================================================
-- One function for the first fill (migration step 1), the SeedDb SourceWordRegistryRunner (roadmap 0005.4) and every
-- later STEP re-import. Idempotent; on an unchanged STEP import it changes nothing. Per source text:
--   pass 1  exact: same verse, position, occurrence and MatchText      -> keep (re-point "ExternalId" if the id moved)
--   pass 2  shifted: same verse and MatchText, n-th occurrence of that text in the verse among the rows pass 1 left
--           (a word inserted or removed earlier in the verse shifts positions, the text sequence stays)
--   rest    registry rows without a match are retired (word removed or text changed); STEP words without a match
--           get a new registry row. Links on retired words stay as they are and are reported ("ActiveLinksOnRetired"),
--           the maintainer re-maps them (the old model pointed such rows silently at the next word).
-- Usage: SELECT * FROM "unshackled-word"."SyncSourceWords"(1::smallint);   -- 1 = TAGNT, 2 = TAHOT
CREATE OR REPLACE FUNCTION "unshackled-word"."SyncSourceWords"(p_source_text_id SMALLINT)
    RETURNS TABLE ("SourceTextId" SMALLINT, "Exact" INT, "Shifted" INT, "Retired" INT, "Inserted" INT, "ActiveLinksOnRetired" INT)
    LANGUAGE plpgsql AS
$$
#variable_conflict use_column
DECLARE
    v_exact    INT;
    v_shifted  INT;
    v_retired  INT;
    v_inserted INT;
    v_links    INT;
BEGIN
    DROP TABLE IF EXISTS sync_cur;
    DROP TABLE IF EXISTS sync_match;

    CREATE TEMP TABLE sync_cur AS
    SELECT x.ext_id
         , x.ref_id
         , x.pos
         , row_number() OVER (PARTITION BY x.ref_id, x.pos ORDER BY x.ext_id)::smallint AS occ
         , x.match_text
    FROM (SELECT sgw."Id" AS ext_id, sgw."LxxRefId" AS ref_id, sgw."PositionInVerse" AS pos, sgw."GreekNoDiacritics" AS match_text
          FROM "unshackled-word"."StepGreekWords" sgw
          WHERE p_source_text_id = 1
          UNION ALL
          SELECT shw."Id", shw."LxxRefId", shw."PositionInVerse", shw."HebrewNoDiacritics"
          FROM "unshackled-word"."StepHebrewWords" shw
          WHERE p_source_text_id = 2) x;
    CREATE UNIQUE INDEX ON sync_cur (ext_id);
    ANALYZE sync_cur;

    CREATE TEMP TABLE sync_match (sw_id INT NOT NULL, ext_id INT NOT NULL, pos INT NOT NULL, occ SMALLINT NOT NULL);

    INSERT INTO sync_match (sw_id, ext_id, pos, occ)
    SELECT sw."Id", c.ext_id, c.pos, c.occ
    FROM "unshackled-word"."SourceWords" sw
        JOIN sync_cur c ON c.ref_id = sw."RefId" AND c.pos = sw."PositionInVerse" AND c.occ = sw."Occurrence" AND c.match_text = sw."MatchText"
    WHERE 1=1
        AND sw."SourceTextId" = p_source_text_id
        AND sw."RetiredAt" IS NULL;
    GET DIAGNOSTICS v_exact = ROW_COUNT;
    CREATE UNIQUE INDEX ON sync_match (sw_id);
    CREATE UNIQUE INDEX ON sync_match (ext_id);
    ANALYZE sync_match;

    INSERT INTO sync_match (sw_id, ext_id, pos, occ)
    SELECT r.sw_id, c.ext_id, c.pos, c.occ
    FROM (SELECT sw."Id" AS sw_id
               , sw."RefId"
               , sw."MatchText"
               , row_number() OVER (PARTITION BY sw."RefId", sw."MatchText" ORDER BY sw."PositionInVerse", sw."Occurrence") AS rn
          FROM "unshackled-word"."SourceWords" sw
          WHERE 1=1
              AND sw."SourceTextId" = p_source_text_id
              AND sw."RetiredAt" IS NULL
              AND NOT EXISTS (SELECT 1 FROM sync_match m WHERE m.sw_id = sw."Id")) r
        JOIN (SELECT cu.*
                   , row_number() OVER (PARTITION BY cu.ref_id, cu.match_text ORDER BY cu.pos, cu.occ) AS rn
              FROM sync_cur cu
              WHERE NOT EXISTS (SELECT 1 FROM sync_match m WHERE m.ext_id = cu.ext_id)) c
            ON c.ref_id = r."RefId" AND c.match_text = r."MatchText" AND c.rn = r.rn;
    GET DIAGNOSTICS v_shifted = ROW_COUNT;

    UPDATE "unshackled-word"."SourceWords" sw
    SET "ExternalId" = NULL
      , "RetiredAt"  = now()
    WHERE 1=1
        AND sw."SourceTextId" = p_source_text_id
        AND sw."RetiredAt" IS NULL
        AND NOT EXISTS (SELECT 1 FROM sync_match m WHERE m.sw_id = sw."Id");
    GET DIAGNOSTICS v_retired = ROW_COUNT;

    UPDATE "unshackled-word"."SourceWords" sw
    SET "ExternalId"      = m.ext_id
      , "PositionInVerse" = m.pos
      , "Occurrence"      = m.occ
    FROM sync_match m
    WHERE 1=1
        AND m.sw_id = sw."Id"
        AND (sw."ExternalId", sw."PositionInVerse", sw."Occurrence") IS DISTINCT FROM (m.ext_id, m.pos, m.occ);

    INSERT INTO "unshackled-word"."SourceWords" ("SourceTextId", "RefId", "PositionInVerse", "Occurrence", "ExternalId", "MatchText")
    SELECT p_source_text_id, c.ref_id, c.pos, c.occ, c.ext_id, c.match_text
    FROM sync_cur c
    WHERE NOT EXISTS (SELECT 1 FROM sync_match m WHERE m.ext_id = c.ext_id)
    ORDER BY c.ext_id;
    GET DIAGNOSTICS v_inserted = ROW_COUNT;

    SELECT count(*)::int
    INTO v_links
    FROM "unshackled-word"."MappingLinkSourceWords" ms
        JOIN "unshackled-word"."SourceWords" sw ON sw."Id" = ms."SourceWordId"
    WHERE 1=1
        AND ms."IsActive"
        AND sw."SourceTextId" = p_source_text_id
        AND sw."RetiredAt" IS NOT NULL;

    DROP TABLE sync_cur;
    DROP TABLE sync_match;

    RETURN QUERY SELECT p_source_text_id, v_exact, v_shifted, v_retired, v_inserted, v_links;
END;
$$;


-- =====================================================================================================================
-- 10. VIEWS
-- =====================================================================================================================

-- Who wrote a link and whether a human currently vouches for it. Latest reviewer/maintainer verdict wins.
CREATE OR REPLACE VIEW "unshackled-word"."MappingLinkTrust" AS
SELECT ml."Id"                                         "LinkId"
     , cs."AuthorUserId"
     , au."Kind"                                       "AuthorKind"
     , cs."AiRunId"
     , ar."Model"
     , ar."PromptVersion"
     , ml."Confidence"
     , lv."Verdict"                                    "LatestVerdict"
     , lv."UserId"                                     "VerdictUserId"
     , lv."CreatedAt"                                  "VerdictAt"
     , COALESCE(lv."Verdict" = 'confirmed', FALSE)     "IsConfirmed"
     , au."Kind" IN ('ai', 'rule') AND COALESCE(lv."Verdict" = 'confirmed', FALSE) = FALSE "IsUnconfirmedMachine"
FROM "unshackled-word"."MappingLinks" ml
    JOIN "unshackled-word"."Changesets" cs ON cs."Id" = ml."ChangesetId"
    JOIN "unshackled-word"."Users" au ON au."Id" = cs."AuthorUserId"
    LEFT JOIN "unshackled-word"."AiRuns" ar ON ar."Id" = cs."AiRunId"
    LEFT JOIN LATERAL (SELECT mc."Verdict"
                            , mc."UserId"
                            , mc."CreatedAt"
                       FROM "unshackled-word"."MappingConfirmations" mc
                           JOIN "unshackled-word"."Users" ru ON ru."Id" = mc."UserId"
                       WHERE 1=1
                           AND mc."LinkId" = ml."Id"
                           AND ru."Role" IN ('reviewer', 'maintainer')
                       ORDER BY mc."CreatedAt" DESC, mc."Id" DESC
                       LIMIT 1) lv ON TRUE;

-- Shape rules that a CHECK cannot express (they span member rows). Must return 0 rows; the changeset service runs the
-- same rules before merge, the verification step of the migration runs this view.
CREATE OR REPLACE VIEW "unshackled-word"."MappingLinkShapeErrors" AS
WITH counts AS (SELECT ml."Id"
                     , ml."Kind"
                     , ml."Level"
                     , (SELECT count(*) FROM "unshackled-word"."MappingLinkSourceWords" s WHERE s."LinkId" = ml."Id") "SourceCount"
                     , (SELECT count(*) FROM "unshackled-word"."MappingLinkGermanWords" g WHERE g."LinkId" = ml."Id" AND g."Role" = 'core') "CoreCount"
                     , (SELECT count(*) FROM "unshackled-word"."MappingLinkGermanWords" g WHERE g."LinkId" = ml."Id" AND g."Role" = 'added') "AddedCount"
                     , (SELECT count(*) FROM "unshackled-word"."MappingLinkGermanWords" g WHERE g."LinkId" = ml."Id" AND g."Role" = 'grammar') "GrammarCount"
                     , (SELECT count(*) FROM "unshackled-word"."MappingLinkGermanWords" g WHERE g."LinkId" = ml."Id" AND g."EditWordId" IS NOT NULL) "EditCount"
                FROM "unshackled-word"."MappingLinks" ml
                WHERE 1=1
                    AND ml."State" IN ('proposed', 'active'))
SELECT c."Id" "LinkId"
     , CASE
           WHEN c."Kind" = 'aligned' AND (c."SourceCount" = 0 OR c."CoreCount" = 0) THEN 'aligned needs source and core German words'
           WHEN c."Kind" = 'aligned' AND c."GrammarCount" > 0 THEN 'grammar role only in covered links'
           WHEN c."Kind" = 'added' AND (c."SourceCount" > 0 OR c."CoreCount" > 0 OR c."GrammarCount" > 0 OR c."AddedCount" = 0) THEN 'added link has only added German words'
           WHEN c."Kind" = 'covered' AND (c."SourceCount" = 0 OR c."CoreCount" > 0 OR c."AddedCount" > 0) THEN 'covered link has source words and at most grammar German words'
           WHEN c."Level" = 0 AND c."EditCount" > 0 THEN 'level-0 link must not use edit tokens'
           END "Error"
FROM counts c
WHERE 1=1
    AND ((c."Kind" = 'aligned' AND (c."SourceCount" = 0 OR c."CoreCount" = 0 OR c."GrammarCount" > 0))
        OR (c."Kind" = 'added' AND (c."SourceCount" > 0 OR c."CoreCount" > 0 OR c."GrammarCount" > 0 OR c."AddedCount" = 0))
        OR (c."Kind" = 'covered' AND (c."SourceCount" = 0 OR c."CoreCount" > 0 OR c."AddedCount" > 0))
        OR (c."Level" = 0 AND c."EditCount" > 0));

-- Compatibility read models with EXACTLY the columns of the legacy tables, built from active level-0 links.
-- Dual-read phase (roadmap 0005.5): readers swap the table name for the view name behind a setting; nothing else in
-- their SQL changes. Mapping of the new model onto the old row shape:
--   aligned link: one row per (core German member x source member)   -> n:m groups become a cross product
--   added member: one row with StepWordId NULL, IsAddedWord TRUE, ParentGermanWordId = first core word of the link
--                 (by position); members of 'added' links have no parent
--   covered link: no row (the old model cannot say it; the source word looks unmapped to old readers)
--   "Id" is the German member id: unique per German word/part, repeated for its source words (readers only ORDER BY it)
CREATE OR REPLACE VIEW "unshackled-word"."Elb1871GreekMappingCompat" AS
SELECT gm."Id"
     , gm."BaseWordId"                                   "ElbWordId"
     , sw."ExternalId"                                   "StepWordId"
     , ew."HebRefId"
     , sgw."DisambiguatedStrongs"::varchar(10)           "StrongsNumber"
     , (gm."Role" = 'added')                             "IsAddedWord"
     , CASE WHEN gm."Role" = 'added' THEN par."BaseWordId" END "ParentGermanWordId"
     , ew."PositionInVerse"
     , gm."PartText"::varchar(30)                        "GermanWordPart"
     , ml."CreatedAt"
FROM "unshackled-word"."MappingLinks" ml
    JOIN "unshackled-word"."MappingLinkGermanWords" gm ON gm."LinkId" = ml."Id" AND gm."Role" IN ('core', 'added') AND gm."BaseWordId" IS NOT NULL
    JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = gm."BaseWordId"
    LEFT JOIN "unshackled-word"."MappingLinkSourceWords" ms ON ms."LinkId" = ml."Id" AND gm."Role" = 'core'
    LEFT JOIN "unshackled-word"."SourceWords" sw ON sw."Id" = ms."SourceWordId"
    LEFT JOIN "unshackled-word"."StepGreekWords" sgw ON sgw."Id" = sw."ExternalId"
    LEFT JOIN LATERAL (SELECT pg."BaseWordId"
                       FROM "unshackled-word"."MappingLinkGermanWords" pg
                           JOIN "unshackled-word"."Elb1871Words" pw ON pw."Id" = pg."BaseWordId"
                       WHERE 1=1
                           AND pg."LinkId" = ml."Id"
                           AND pg."Role" = 'core'
                       ORDER BY pw."PositionInVerse", pg."PartIndex"
                       LIMIT 1) par ON gm."Role" = 'added'
WHERE 1=1
    AND ml."IsActive"
    AND ml."Level" = 0
    AND ml."SourceTextId" = 1
    AND ml."Kind" IN ('aligned', 'added');

CREATE OR REPLACE VIEW "unshackled-word"."Elb1871HebrewMappingCompat" AS
SELECT gm."Id"
     , gm."BaseWordId"                                   "ElbWordId"
     , sw."ExternalId"                                   "StepWordId"
     , ew."HebRefId"
     , (gm."Role" = 'added')                             "IsAddedWord"
     , CASE WHEN gm."Role" = 'added' THEN par."BaseWordId" END "ParentGermanWordId"
     , ew."PositionInVerse"
     , gm."PartText"::varchar(30)                        "GermanWordPart"
     , ml."CreatedAt"
FROM "unshackled-word"."MappingLinks" ml
    JOIN "unshackled-word"."MappingLinkGermanWords" gm ON gm."LinkId" = ml."Id" AND gm."Role" IN ('core', 'added') AND gm."BaseWordId" IS NOT NULL
    JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = gm."BaseWordId"
    LEFT JOIN "unshackled-word"."MappingLinkSourceWords" ms ON ms."LinkId" = ml."Id" AND gm."Role" = 'core'
    LEFT JOIN "unshackled-word"."SourceWords" sw ON sw."Id" = ms."SourceWordId"
    LEFT JOIN LATERAL (SELECT pg."BaseWordId"
                       FROM "unshackled-word"."MappingLinkGermanWords" pg
                           JOIN "unshackled-word"."Elb1871Words" pw ON pw."Id" = pg."BaseWordId"
                       WHERE 1=1
                           AND pg."LinkId" = ml."Id"
                           AND pg."Role" = 'core'
                       ORDER BY pw."PositionInVerse", pg."PartIndex"
                       LIMIT 1) par ON gm."Role" = 'added'
WHERE 1=1
    AND ml."IsActive"
    AND ml."Level" = 0
    AND ml."SourceTextId" = 2
    AND ml."Kind" IN ('aligned', 'added');


-- =====================================================================================================================
-- 11. EXAMPLE QUERIES (Dapper parameters @...; run in psql by replacing them)
-- =====================================================================================================================

-- Q1. Current effective mapping of one German verse at level 0 (Elb1871), one row per link, with trust info.
--     Joh 1:1 = @RefId 43001001.
-- SELECT ml."Id" "LinkId"
--      , ml."Kind"
--      , ml."Reason"
--      , string_agg(CASE WHEN gm."Role" = 'core' THEN coalesce(gm."PartText", ew."WordInContext") END, ' ' ORDER BY ew."PositionInVerse", gm."PartIndex") "German"
--      , string_agg(CASE WHEN gm."Role" = 'added' THEN ew."WordInContext" END, ' ' ORDER BY ew."PositionInVerse") "AddedGerman"
--      , string_agg(CASE WHEN gm."Role" = 'grammar' THEN ew."WordInContext" END, ' ' ORDER BY ew."PositionInVerse") "CoveredByGerman"
--      , (SELECT string_agg(sgw."Greek", ' ' ORDER BY sw."RefId", sw."PositionInVerse")
--         FROM "unshackled-word"."MappingLinkSourceWords" ms
--             JOIN "unshackled-word"."SourceWords" sw ON sw."Id" = ms."SourceWordId"
--             JOIN "unshackled-word"."StepGreekWords" sgw ON sgw."Id" = sw."ExternalId"
--         WHERE ms."LinkId" = ml."Id") "Source"
--      , t."AuthorKind"
--      , t."Model"
--      , t."IsConfirmed"
-- FROM "unshackled-word"."MappingLinks" ml
--     JOIN "unshackled-word"."MappingLinkTrust" t ON t."LinkId" = ml."Id"
--     JOIN "unshackled-word"."MappingLinkGermanWords" gm ON gm."LinkId" = ml."Id"
--     JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = gm."BaseWordId"
-- WHERE 1=1
--     AND ml."IsActive"
--     AND ml."Level" = 0
--     AND ml."RefId" = @RefId
-- GROUP BY ml."Id", ml."Kind", ml."Reason", t."AuthorKind", t."Model", t."IsConfirmed"
-- ORDER BY min(ew."PositionInVerse");
--     (covered links have no core German word; they sort by their grammar word, or last when they have none —
--      add "UNION ALL" over covered links without German members if they must be listed too, see Q2.)

-- Q2. Source words of a verse range that are UNMAPPED at level 0 (no active link at all; covered words do not appear).
-- SELECT sw."RefId"
--      , sw."PositionInVerse"
--      , sgw."Greek"
--      , sgw."Morphology"
--      , sgw."Editions"
-- FROM "unshackled-word"."SourceWords" sw
--     JOIN "unshackled-word"."StepGreekWords" sgw ON sgw."Id" = sw."ExternalId"
-- WHERE 1=1
--     AND sw."SourceTextId" = 1
--     AND sw."RefId" BETWEEN @FromRefId AND @ToRefId
--     AND NOT EXISTS (SELECT 1
--                     FROM "unshackled-word"."MappingLinkSourceWords" ms
--                     WHERE 1=1
--                         AND ms."SourceWordId" = sw."Id"
--                         AND ms."IsActive"
--                         AND ms."LinkLevel" = 0)
-- ORDER BY sw."RefId", sw."PositionInVerse";

-- Q3. German words added by the translators (level 0), with the phrase they belong to.
-- SELECT ew."HebRefId"
--      , ew."PositionInVerse"
--      , ew."WordInContext"                                    "AddedWord"
--      , ml."Kind"                                             -- 'aligned' = inside a phrase, 'added' = free-standing
--      , ml."Reason"
--      , (SELECT string_agg(cw."WordInContext", ' ' ORDER BY cw."PositionInVerse")
--         FROM "unshackled-word"."MappingLinkGermanWords" cg
--             JOIN "unshackled-word"."Elb1871Words" cw ON cw."Id" = cg."BaseWordId"
--         WHERE cg."LinkId" = ml."Id" AND cg."Role" = 'core') "Phrase"
-- FROM "unshackled-word"."MappingLinkGermanWords" gm
--     JOIN "unshackled-word"."MappingLinks" ml ON ml."Id" = gm."LinkId"
--     JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = gm."BaseWordId"
-- WHERE 1=1
--     AND gm."IsActive"
--     AND gm."LinkLevel" = 0
--     AND gm."Role" = 'added'
--     AND ew."HebRefId" BETWEEN @FromRefId AND @ToRefId
-- ORDER BY ew."HebRefId", ew."PositionInVerse";

-- Q4. Full history of one German word (every link it was ever part of, in any state), who wrote/merged/ended it,
--     and the verdicts. This is "history of a link" seen from the token, which survives link replacement.
-- SELECT ml."Id" "LinkId"
--      , ml."Kind"
--      , ml."Level"
--      , ml."State"
--      , ml."CreatedAt"
--      , ml."ActivatedAt"
--      , ml."EndedAt"
--      , cs."Id" "ChangesetId"
--      , cs."Status" "ChangesetStatus"
--      , au."UserName" "Author"
--      , ar."Model"
--      , rv."UserName" "MergedBy"
--      , ml."EndedByChangesetId"
--      , (SELECT string_agg(mc."Verdict" || ' by ' || cu."UserName", ', ' ORDER BY mc."CreatedAt")
--         FROM "unshackled-word"."MappingConfirmations" mc
--             JOIN "unshackled-word"."Users" cu ON cu."Id" = mc."UserId"
--         WHERE mc."LinkId" = ml."Id") "Verdicts"
--      , (SELECT string_agg(lr."Language" || '#' || lr."LegacyId" || ':' || lr."Treatment", ', ')
--         FROM "unshackled-word"."MappingLegacyRows" lr
--         WHERE lr."LinkId" = ml."Id") "FromLegacyRows"
-- FROM "unshackled-word"."MappingLinkGermanWords" gm
--     JOIN "unshackled-word"."MappingLinks" ml ON ml."Id" = gm."LinkId"
--     JOIN "unshackled-word"."Changesets" cs ON cs."Id" = ml."ChangesetId"
--     JOIN "unshackled-word"."Users" au ON au."Id" = cs."AuthorUserId"
--     LEFT JOIN "unshackled-word"."AiRuns" ar ON ar."Id" = cs."AiRunId"
--     LEFT JOIN "unshackled-word"."Users" rv ON rv."Id" = cs."ReviewedByUserId"
-- WHERE 1=1
--     AND gm."BaseWordId" = @ElbWordId
-- ORDER BY ml."CreatedAt", ml."Id";

-- Q5. Effective mapping of a verse at slider level @Level (0..10000): visible tokens and the winning link per token.
--     A base token is hidden when an applied edit covers its position (highest applicable level per span wins);
--     edit tokens of applied edits become visible; a link counts when all its German core/added members are visible
--     (directly, or through an edit word that InheritsFromWordId a hidden base word); per token the highest Level wins.
-- WITH applied AS (SELECT ge.*
--                  FROM "unshackled-word"."GermanEdits" ge
--                  WHERE 1=1
--                      AND ge."IsActive"
--                      AND ge."RefId" = @RefId
--                      AND ge."Level" <= @Level
--                      AND NOT EXISTS (SELECT 1                                   -- hidden by a higher applicable edit
--                                      FROM "unshackled-word"."GermanEdits" hi
--                                      WHERE hi."IsActive" AND hi."RefId" = ge."RefId" AND hi."Level" <= @Level
--                                          AND hi."Level" > ge."Level"
--                                          AND int4range(hi."FromPosition", hi."ToPosition" + 1) && int4range(ge."FromPosition", ge."ToPosition" + 1)))
--    , visible AS (SELECT ew."Id" "BaseWordId", NULL::int "EditWordId", ew."PositionInVerse"::numeric "SortKey", ew."WordInContext"
--                  FROM "unshackled-word"."Elb1871Words" ew
--                  WHERE ew."HebRefId" = @RefId
--                      AND NOT EXISTS (SELECT 1 FROM applied a WHERE ew."PositionInVerse" BETWEEN a."FromPosition" AND a."ToPosition")
--                  UNION ALL
--                  SELECT NULL, gw."Id", a."FromPosition" - 0.5 + gw."Ordinal" / 1000.0, gw."WordInContext"
--                  FROM applied a JOIN "unshackled-word"."GermanEditWords" gw ON gw."EditId" = a."Id")
--    , inherited AS (SELECT gw."InheritsFromWordId" "BaseWordId" FROM visible v JOIN "unshackled-word"."GermanEditWords" gw ON gw."Id" = v."EditWordId" WHERE gw."InheritsFromWordId" IS NOT NULL)
--    , candidates AS (SELECT ml.*
--                     FROM "unshackled-word"."MappingLinks" ml
--                     WHERE ml."IsActive" AND ml."RefId" = @RefId AND ml."Level" <= @Level
--                         AND NOT EXISTS (SELECT 1 FROM "unshackled-word"."MappingLinkGermanWords" gm
--                                         WHERE gm."LinkId" = ml."Id" AND gm."Role" <> 'grammar'
--                                             AND NOT EXISTS (SELECT 1 FROM visible v WHERE v."BaseWordId" = gm."BaseWordId" OR v."EditWordId" = gm."EditWordId")
--                                             AND NOT EXISTS (SELECT 1 FROM inherited i WHERE i."BaseWordId" = gm."BaseWordId")))
-- SELECT DISTINCT ON (ms."SourceWordId") ms."SourceWordId", c."Id" "LinkId", c."Level", c."Kind"
-- FROM candidates c JOIN "unshackled-word"."MappingLinkSourceWords" ms ON ms."LinkId" = c."Id"
-- ORDER BY ms."SourceWordId", c."Level" DESC;
--     (German side analogous: DISTINCT ON the visible token instead of the source word.)

-- Q6. Covered-by-grammar words of a book with reason and the German word carrying them (e.g. all object markers).
-- SELECT ml."Reason", sw."RefId", sw."PositionInVerse", shw."Hebrew", ew."WordInContext" "CarriedBy"
-- FROM "unshackled-word"."MappingLinks" ml
--     JOIN "unshackled-word"."MappingLinkSourceWords" ms ON ms."LinkId" = ml."Id"
--     JOIN "unshackled-word"."SourceWords" sw ON sw."Id" = ms."SourceWordId"
--     JOIN "unshackled-word"."StepHebrewWords" shw ON shw."Id" = sw."ExternalId"
--     LEFT JOIN "unshackled-word"."MappingLinkGermanWords" gm ON gm."LinkId" = ml."Id" AND gm."Role" = 'grammar'
--     LEFT JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = gm."BaseWordId"
-- WHERE 1=1
--     AND ml."IsActive" AND ml."Level" = 0 AND ml."Kind" = 'covered'
--     AND ml."RefId" BETWEEN 1000000 AND 1999999
-- ORDER BY sw."RefId", sw."PositionInVerse";
