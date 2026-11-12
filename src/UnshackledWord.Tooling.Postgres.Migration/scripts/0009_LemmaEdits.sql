-- Audit trail of lemma/morphology edits made in the lemma editor (direct-write mode).
-- BeforeValues is NULL when the Elb1871Morphology row did not exist yet.

CREATE TABLE IF NOT EXISTS "unshackled-word"."Elb1871MorphologyEdits"
(
    "Id"              serial4     NOT NULL,
    "ElbWordId"       int4        NOT NULL,
    "HebRefId"        int4        NOT NULL,
    "PositionInVerse" int4        NOT NULL,
    "UserId"          int4        NOT NULL,
    "EditedAt"        timestamptz NOT NULL DEFAULT now(),
    "BeforeValues"    jsonb       NULL,
    "AfterValues"     jsonb       NOT NULL,
    CONSTRAINT "Elb1871MorphologyEdits_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "Elb1871MorphologyEdits_Users_FK" FOREIGN KEY ("UserId") REFERENCES "unshackled-word"."Users" ("Id")
);

CREATE INDEX IF NOT EXISTS "Elb1871MorphologyEdits_ElbWordId_idx" ON "unshackled-word"."Elb1871MorphologyEdits" ("ElbWordId", "EditedAt" DESC);
