-- Roadmap 0030.5: manual root edits are logged in VocabEntryChanges with the new kind 'root'.
-- A root row never bumps ContentVersion and is never shown to learners (VocabChangeService leaves the kind out).
--
-- Idempotent: safe to run again.

ALTER TABLE "unshackled-word"."VocabEntryChanges" DROP CONSTRAINT IF EXISTS "VocabEntryChanges_Kind_CK";

ALTER TABLE "unshackled-word"."VocabEntryChanges"
    ADD CONSTRAINT "VocabEntryChanges_Kind_CK" CHECK ("Kind" IN ('meanings', 'form', 'lemma', 'keys', 'merge', 'split', 'retire', 'delete', 'root'));

COMMENT ON COLUMN "unshackled-word"."VocabEntryChanges"."Kind" IS 'meanings | form | lemma | keys | merge | split | retire | delete | root (root: maintainer edit of the root, no version bump, never shown to learners).';
