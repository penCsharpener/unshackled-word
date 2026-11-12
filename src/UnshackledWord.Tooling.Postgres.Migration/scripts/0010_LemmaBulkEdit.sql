-- Bulk lemma replace: lookup indexes on the lemma, change sets, and a change-set id on the audit rows.
-- Nothing is dropped here. Elb1871Words.Lemma/PartOfSpeech/GrammaticalKey are retired in a later migration.

CREATE INDEX IF NOT EXISTS "Elb1871Morphology_Lemma_idx" ON "unshackled-word"."Elb1871Morphology" ("Lemma");

-- Autocomplete / case variants: case-insensitive prefix search (LIKE 'abc%' on lower("Lemma")).
CREATE INDEX IF NOT EXISTS "Elb1871Morphology_LemmaLower_idx" ON "unshackled-word"."Elb1871Morphology" (lower("Lemma") text_pattern_ops);

-- One row per confirmed bulk run or revert. Kind: 'bulkLemma' | 'revert' ('grid' reserved for grid saves).
CREATE TABLE IF NOT EXISTS "unshackled-word"."Elb1871MorphologyChangeSets"
(
    "Id"         serial4      NOT NULL,
    "UserId"     int4         NOT NULL,
    "Kind"       varchar(20)  NOT NULL,
    "FromLemma"  text         NULL,
    "ToLemma"    varchar(255) NULL,
    "Scope"      jsonb        NULL,
    "Note"       varchar(500) NULL,
    "WordCount"  int4         NOT NULL DEFAULT 0,
    "RevertOf"   int4         NULL,
    "RevertedAt" timestamptz  NULL,
    "CreatedAt"  timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT "Elb1871MorphologyChangeSets_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "Elb1871MorphologyChangeSets_Users_FK" FOREIGN KEY ("UserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "Elb1871MorphologyChangeSets_RevertOf_FK" FOREIGN KEY ("RevertOf") REFERENCES "unshackled-word"."Elb1871MorphologyChangeSets" ("Id")
);

ALTER TABLE "unshackled-word"."Elb1871MorphologyEdits"
    ADD COLUMN IF NOT EXISTS "ChangeSetId" int4 NULL;

DO
$$
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'Elb1871MorphologyEdits_ChangeSets_FK') THEN
            ALTER TABLE "unshackled-word"."Elb1871MorphologyEdits"
                ADD CONSTRAINT "Elb1871MorphologyEdits_ChangeSets_FK" FOREIGN KEY ("ChangeSetId") REFERENCES "unshackled-word"."Elb1871MorphologyChangeSets" ("Id");
        END IF;
    END
$$;

CREATE INDEX IF NOT EXISTS "Elb1871MorphologyEdits_ChangeSetId_idx" ON "unshackled-word"."Elb1871MorphologyEdits" ("ChangeSetId");
