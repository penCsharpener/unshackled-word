-- Septuagint tags (roadmap 0028.5, docs/roadmaps/0028-lxx-rahlfs/04-model.md).
--   LxxWordTags     the current tag (lemma, Strong's, Robinson-style morphology) per LXX word, proposed or confirmed. Public reads show only
--                   confirmed tags; proposals are for Admin/Maintainer. Pass 1 (0028.6, Source nt-form) and the AI review (0028.7) write here.
--   LxxHebrewLinks  which STEP Hebrew word(s) an LXX word renders. "StepHebrewWordId" has no FK, like the Elb1871 mappings: a STEP re-import
--                   renumbers the ids, so the repo seed anchors the Hebrew side by its SourceKey (LxxRefId, position, hash) and re-resolves
--                   it on import (SourceWordResolver). The index on "StepHebrewWordId" serves that re-resolve and reverse reads.
--   LxxTagAudit     append-only history of tag changes per LXX verse (before/after jsonb), local data like "TskCurationAudit".
-- Tags and links reference "LxxWords" without ON DELETE CASCADE: the text tables are never re-imported over tags, and a delete of tagged text
-- must fail instead of silently dropping reviewed work. No ON DELETE CASCADE on "Users" either: users are deactivated, not deleted (roadmap 0020).
-- "UserId" NULL = written by a seed import (the seed files carry no user).
-- Also: "AgentVerseProposals" (migration 0025) accepts Language 'lxx'. Its "one open proposal per verse" index gains "Language" and, for LXX
-- proposals, "Flags".textForm/subverse (Rahlfs numbering): an LXX proposal for Gen 1:1 must not collide with a Hebrew mapping proposal for Gen 1:1,
-- nor Daniel og with th. Greek (NT) and Hebrew (OT) proposals never share a verse, so existing rows keep their meaning.
--
-- Idempotent: safe to run again.

CREATE TABLE IF NOT EXISTS "unshackled-word"."LxxWordTags"
(
    "LxxWordId"  int4         NOT NULL,
    "Lemma"      varchar(100) NOT NULL,
    "Strongs"    varchar(20)  NOT NULL,
    "Morphology" varchar(30)  NOT NULL,
    "Status"     varchar(12)  NOT NULL,
    "Source"     varchar(12)  NOT NULL,
    "UserId"     int4         NULL,
    "UpdatedAt"  timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT "LxxWordTags_PK" PRIMARY KEY ("LxxWordId"),
    CONSTRAINT "LxxWordTags_LxxWord_FK" FOREIGN KEY ("LxxWordId") REFERENCES "unshackled-word"."LxxWords" ("Id"),
    CONSTRAINT "LxxWordTags_Users_FK" FOREIGN KEY ("UserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "LxxWordTags_Lemma_CK" CHECK (char_length(btrim("Lemma")) > 0),
    CONSTRAINT "LxxWordTags_Strongs_CK" CHECK ("Strongs" ~ '^G[0-9]{4}[A-Z]?$'),
    CONSTRAINT "LxxWordTags_Morphology_CK" CHECK (char_length(btrim("Morphology")) > 0),
    CONSTRAINT "LxxWordTags_Status_CK" CHECK ("Status" IN ('proposed', 'confirmed')),
    CONSTRAINT "LxxWordTags_Source_CK" CHECK ("Source" IN ('nt-form', 'ai', 'human'))
);

-- LXX occurrences of a Strong's number (0028.9): confirmed for everyone, the proposed count for maintainers
CREATE INDEX IF NOT EXISTS "LxxWordTags_Strongs_idx" ON "unshackled-word"."LxxWordTags" ("Strongs", "Status");
-- the review page (0028.7f): words with open proposals
CREATE INDEX IF NOT EXISTS "LxxWordTags_Proposed_idx" ON "unshackled-word"."LxxWordTags" ("LxxWordId") WHERE "Status" = 'proposed';

COMMENT ON TABLE "unshackled-word"."LxxWordTags" IS 'Current tag per Septuagint word (roadmap 0028): lemma, Strong''s (TBESG) and Robinson-style morphology. Public reads show confirmed tags only. Confirmed tags go into the repo seed (assets/seed/lxx/tags.csv).';
COMMENT ON COLUMN "unshackled-word"."LxxWordTags"."Lemma" IS 'Greek lemma as in TBESG (StepStrongsLexicon.OriginalWord), e.g. ἀρχή.';
COMMENT ON COLUMN "unshackled-word"."LxxWordTags"."Strongs" IS 'Disambiguated Strong''s number as in StepGreekWords.DisambiguatedStrongs, e.g. G0746, G1487M; that TBESG has it is checked by the application.';
COMMENT ON COLUMN "unshackled-word"."LxxWordTags"."Morphology" IS 'Robinson-style code as in StepGreekWords.Morphology, e.g. N-DSF, V-AAI-3S; validated by GreekMorphologyCode.';
COMMENT ON COLUMN "unshackled-word"."LxxWordTags"."Status" IS 'proposed (only Admin/Maintainer see it) | confirmed (public).';
COMMENT ON COLUMN "unshackled-word"."LxxWordTags"."Source" IS 'nt-form (deterministic pass from unambiguous NT forms, 0028.6) | ai (AI answer, confirmed unchanged) | human (entered or edited by a maintainer).';
COMMENT ON COLUMN "unshackled-word"."LxxWordTags"."UserId" IS 'Who wrote the current state (AI user, maintainer); NULL after a seed import.';

CREATE TABLE IF NOT EXISTS "unshackled-word"."LxxHebrewLinks"
(
    "LxxWordId"        int4        NOT NULL,
    "StepHebrewWordId" int4        NOT NULL,
    "Status"           varchar(12) NOT NULL,
    "UserId"           int4        NULL,
    "UpdatedAt"        timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "LxxHebrewLinks_PK" PRIMARY KEY ("LxxWordId", "StepHebrewWordId"),
    CONSTRAINT "LxxHebrewLinks_LxxWord_FK" FOREIGN KEY ("LxxWordId") REFERENCES "unshackled-word"."LxxWords" ("Id"),
    CONSTRAINT "LxxHebrewLinks_Users_FK" FOREIGN KEY ("UserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "LxxHebrewLinks_Status_CK" CHECK ("Status" IN ('proposed', 'confirmed'))
);

-- reverse reads (Hebrew word -> LXX words) and the re-resolve after a STEP re-import; the PK serves reads by LXX word
CREATE INDEX IF NOT EXISTS "LxxHebrewLinks_StepHebrewWord_idx" ON "unshackled-word"."LxxHebrewLinks" ("StepHebrewWordId");

COMMENT ON TABLE "unshackled-word"."LxxHebrewLinks" IS 'Septuagint word renders STEP Hebrew word(s) (roadmap 0028). Confirmed links go into the repo seed (assets/seed/lxx/links.csv) with the Hebrew side as SourceKey.';
COMMENT ON COLUMN "unshackled-word"."LxxHebrewLinks"."StepHebrewWordId" IS 'StepHebrewWords.Id of this installation, no FK (a STEP re-import renumbers it); the seed carries the Hebrew SourceKey instead.';
COMMENT ON COLUMN "unshackled-word"."LxxHebrewLinks"."Status" IS 'proposed (only Admin/Maintainer see it) | confirmed (public).';
COMMENT ON COLUMN "unshackled-word"."LxxHebrewLinks"."UserId" IS 'Who wrote the link; NULL after a seed import.';

CREATE TABLE IF NOT EXISTS "unshackled-word"."LxxTagAudit"
(
    "Id"         int8        GENERATED ALWAYS AS IDENTITY,
    "LxxVerseId" int4        NOT NULL,
    "Action"     varchar(20) NOT NULL,
    "Before"     jsonb       NULL,
    "After"      jsonb       NOT NULL,
    "UserId"     int4        NULL,
    "At"         timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "LxxTagAudit_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "LxxTagAudit_LxxVerse_FK" FOREIGN KEY ("LxxVerseId") REFERENCES "unshackled-word"."LxxVerses" ("Id"),
    CONSTRAINT "LxxTagAudit_Users_FK" FOREIGN KEY ("UserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "LxxTagAudit_Action_CK" CHECK ("Action" IN ('confirm', 'edit', 'reject', 'import'))
);

-- history of one verse, newest first
CREATE INDEX IF NOT EXISTS "LxxTagAudit_Verse_idx" ON "unshackled-word"."LxxTagAudit" ("LxxVerseId", "At" DESC);

COMMENT ON TABLE "unshackled-word"."LxxTagAudit" IS 'Append-only history of LxxWordTags/LxxHebrewLinks per LXX verse: one row per applied review action. Local data, not part of the repo seed.';
COMMENT ON COLUMN "unshackled-word"."LxxTagAudit"."Action" IS 'confirm (proposal confirmed) | edit (maintainer changed confirmed tags) | reject (proposal discarded) | import (seed import).';
COMMENT ON COLUMN "unshackled-word"."LxxTagAudit"."Before" IS 'Tags and links of the verse before the action ({words: [{position, lemma, strongs, morphology, status, source, hebrewWordIds}]}); NULL = none.';
COMMENT ON COLUMN "unshackled-word"."LxxTagAudit"."After" IS 'Tags and links of the verse after the action, same shape as Before.';
COMMENT ON COLUMN "unshackled-word"."LxxTagAudit"."UserId" IS 'Who applied the action; NULL for seed imports.';

-- ---- AgentVerseProposals: LXX proposals (0028.7) -----------------------------------------------------------------------------------

ALTER TABLE "unshackled-word"."AgentVerseProposals" DROP CONSTRAINT IF EXISTS "AgentVerseProposals_Language_CK";

ALTER TABLE "unshackled-word"."AgentVerseProposals"
    ADD CONSTRAINT "AgentVerseProposals_Language_CK" CHECK ("Language" IN ('greek', 'hebrew', 'lxx'));

DROP INDEX IF EXISTS "unshackled-word"."AgentVerseProposals_Open_UQ";

-- At most one open proposal per verse and language; LXX verses are keyed in Rahlfs numbering plus Flags.textForm and Flags.subverse.
CREATE UNIQUE INDEX IF NOT EXISTS "AgentVerseProposals_Open_UQ" ON "unshackled-word"."AgentVerseProposals"
    ("BibleBookId", "Chapter", "Verse", "Language", COALESCE("Flags" ->> 'textForm', ''), COALESCE("Flags" ->> 'subverse', '0'))
    WHERE "Status" = 'open';

COMMENT ON COLUMN "unshackled-word"."AgentVerseProposals"."Language" IS 'greek (NT mapping) | hebrew (OT mapping) | lxx (Septuagint tags, roadmap 0028.7: BibleBookId/Chapter/Verse in Rahlfs numbering, Flags.textForm and Flags.subverse complete the key).';
