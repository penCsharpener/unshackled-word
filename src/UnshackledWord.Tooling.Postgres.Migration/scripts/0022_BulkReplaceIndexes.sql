-- Bulk word replace (roadmap 0013): whole-word, case-insensitive search on the German text of the base and of the revision variants.
CREATE INDEX IF NOT EXISTS "Elb1871Words_PlainWord_lower_idx" ON "unshackled-word"."Elb1871Words" (lower("PlainWord"));
CREATE INDEX IF NOT EXISTS "RevisionWords_PlainWord_lower_idx" ON "unshackled-word"."RevisionWords" (lower("PlainWord"));
