-- Link roles, Hebrew morphemes and part lemmas (roadmap 0018.1, docs/roadmaps/0018-german-lemmas/05-data-model.md).
--
-- "Role": head = the German word carries the meaning of the source morpheme (counted), unit = it only belongs to the head's German form
-- (wurde in "wurde getötet", hinaus in "ging hinaus"; not counted). Every existing row becomes head through the default; the backfill
-- job (0018.4) sets the units. Added-word rows keep head and are ignored.
-- "StrongsNumber" (Hebrew, RevisionMapping): the morpheme of the Hebrew word the German word renders (H9001 for "und" of וַיֹּאמֶר);
-- NULL = the main (braced) morpheme until backfilled. On RevisionMapping only Lang H rows carry it (Greek derives it from the STEP word).
-- "PartLemma": lemma of a compound part (Gerste of "Gersten" in "Gerstenernte"); only on rows with "GermanWordPart".
-- "MultiHead": the maintainer accepted more than one head on this source morpheme (idioms like "zur Antwort geben"); set on every head row
-- of that morpheme, false everywhere else.
-- "AgentVerseProposals"."Roles": role/morpheme/part-lemma diffs of an agent answer, like "Lemmas".
--
-- Idempotent: ADD COLUMN IF NOT EXISTS, constraints guarded by pg_constraint, CREATE INDEX IF NOT EXISTS. Existing INSERTs keep working
-- because every new column has a default or is nullable.

ALTER TABLE "unshackled-word"."Elb1871GreekMapping"
    ADD COLUMN IF NOT EXISTS "Role" varchar(4) NOT NULL DEFAULT 'head';
ALTER TABLE "unshackled-word"."Elb1871GreekMapping"
    ADD COLUMN IF NOT EXISTS "PartLemma" varchar(40) NULL;
ALTER TABLE "unshackled-word"."Elb1871GreekMapping"
    ADD COLUMN IF NOT EXISTS "MultiHead" boolean NOT NULL DEFAULT false;

ALTER TABLE "unshackled-word"."Elb1871HebrewMapping"
    ADD COLUMN IF NOT EXISTS "Role" varchar(4) NOT NULL DEFAULT 'head';
ALTER TABLE "unshackled-word"."Elb1871HebrewMapping"
    ADD COLUMN IF NOT EXISTS "StrongsNumber" varchar(10) NULL;
ALTER TABLE "unshackled-word"."Elb1871HebrewMapping"
    ADD COLUMN IF NOT EXISTS "PartLemma" varchar(40) NULL;
ALTER TABLE "unshackled-word"."Elb1871HebrewMapping"
    ADD COLUMN IF NOT EXISTS "MultiHead" boolean NOT NULL DEFAULT false;

ALTER TABLE "unshackled-word"."RevisionMapping"
    ADD COLUMN IF NOT EXISTS "Role" varchar(4) NOT NULL DEFAULT 'head';
ALTER TABLE "unshackled-word"."RevisionMapping"
    ADD COLUMN IF NOT EXISTS "StrongsNumber" varchar(10) NULL;
ALTER TABLE "unshackled-word"."RevisionMapping"
    ADD COLUMN IF NOT EXISTS "PartLemma" varchar(40) NULL;
ALTER TABLE "unshackled-word"."RevisionMapping"
    ADD COLUMN IF NOT EXISTS "MultiHead" boolean NOT NULL DEFAULT false;

ALTER TABLE "unshackled-word"."AgentVerseProposals"
    ADD COLUMN IF NOT EXISTS "Roles" jsonb NULL;

DO
$$
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'Elb1871GreekMapping_Role_CK') THEN
            ALTER TABLE "unshackled-word"."Elb1871GreekMapping"
                ADD CONSTRAINT "Elb1871GreekMapping_Role_CK" CHECK ("Role" IN ('head', 'unit'));
        END IF;
        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'Elb1871GreekMapping_PartLemma_CK') THEN
            ALTER TABLE "unshackled-word"."Elb1871GreekMapping"
                ADD CONSTRAINT "Elb1871GreekMapping_PartLemma_CK" CHECK ("PartLemma" IS NULL OR "GermanWordPart" IS NOT NULL);
        END IF;

        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'Elb1871HebrewMapping_Role_CK') THEN
            ALTER TABLE "unshackled-word"."Elb1871HebrewMapping"
                ADD CONSTRAINT "Elb1871HebrewMapping_Role_CK" CHECK ("Role" IN ('head', 'unit'));
        END IF;
        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'Elb1871HebrewMapping_PartLemma_CK') THEN
            ALTER TABLE "unshackled-word"."Elb1871HebrewMapping"
                ADD CONSTRAINT "Elb1871HebrewMapping_PartLemma_CK" CHECK ("PartLemma" IS NULL OR "GermanWordPart" IS NOT NULL);
        END IF;

        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'RevisionMapping_Role_CK') THEN
            ALTER TABLE "unshackled-word"."RevisionMapping"
                ADD CONSTRAINT "RevisionMapping_Role_CK" CHECK ("Role" IN ('head', 'unit'));
        END IF;
        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'RevisionMapping_PartLemma_CK') THEN
            ALTER TABLE "unshackled-word"."RevisionMapping"
                ADD CONSTRAINT "RevisionMapping_PartLemma_CK" CHECK ("PartLemma" IS NULL OR "GermanWordPart" IS NOT NULL);
        END IF;
        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'RevisionMapping_Strongs_CK') THEN
            ALTER TABLE "unshackled-word"."RevisionMapping"
                ADD CONSTRAINT "RevisionMapping_Strongs_CK" CHECK ("StrongsNumber" IS NULL OR "Lang" = 'H');
        END IF;
    END
$$;

CREATE INDEX IF NOT EXISTS "IdxElbHebrewMappingStrongs" ON "unshackled-word"."Elb1871HebrewMapping" ("StrongsNumber");

COMMENT ON COLUMN "unshackled-word"."Elb1871GreekMapping"."Role" IS 'head = carries the meaning of the source word (counted), unit = belongs to the head''s German form (wurde, hinaus, sich; not counted).';
COMMENT ON COLUMN "unshackled-word"."Elb1871HebrewMapping"."Role" IS 'head = carries the meaning of the source morpheme (counted), unit = belongs to the head''s German form (not counted).';
COMMENT ON COLUMN "unshackled-word"."RevisionMapping"."Role" IS 'head or unit, same meaning as on Elb1871GreekMapping/Elb1871HebrewMapping.';
COMMENT ON COLUMN "unshackled-word"."Elb1871HebrewMapping"."StrongsNumber" IS 'Morpheme of the Hebrew word this German word renders (one tag of StepHebrewWords.DisambiguatedStrongs, braces stripped); NULL = the main (braced) morpheme.';
COMMENT ON COLUMN "unshackled-word"."RevisionMapping"."StrongsNumber" IS 'Hebrew morpheme (Lang H only), same meaning as Elb1871HebrewMapping.StrongsNumber; NULL = main morpheme.';
COMMENT ON COLUMN "unshackled-word"."Elb1871GreekMapping"."PartLemma" IS 'Lemma of the compound part "GermanWordPart"; NULL = no part or not set.';
COMMENT ON COLUMN "unshackled-word"."Elb1871HebrewMapping"."PartLemma" IS 'Lemma of the compound part "GermanWordPart"; NULL = no part or not set.';
COMMENT ON COLUMN "unshackled-word"."RevisionMapping"."PartLemma" IS 'Lemma of the compound part "GermanWordPart"; NULL = no part or not set.';
COMMENT ON COLUMN "unshackled-word"."Elb1871GreekMapping"."MultiHead" IS 'True on the head rows of a source morpheme whose several heads the maintainer accepted (idiom); false otherwise.';
COMMENT ON COLUMN "unshackled-word"."Elb1871HebrewMapping"."MultiHead" IS 'True on the head rows of a source morpheme whose several heads the maintainer accepted (idiom); false otherwise.';
COMMENT ON COLUMN "unshackled-word"."RevisionMapping"."MultiHead" IS 'True on the head rows of a source morpheme whose several heads the maintainer accepted (idiom); false otherwise.';
COMMENT ON COLUMN "unshackled-word"."AgentVerseProposals"."Roles" IS 'jsonb: proposed role/morpheme/part-lemma changes of the verse links (roadmap 0018).';
