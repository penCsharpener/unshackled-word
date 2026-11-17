-- MCP write access and proposal review (roadmap 0016.7 / 0016.8).
-- ApiTokens: bearer tokens a maintainer issues for a user with role "ai", "maintainer" or "admin". Only the SHA-256 hash of the token is stored
-- (the token is random with 256 bits, so no salt or slow hash is needed); the token itself is shown once at creation. Scope "propose" lets the
-- holder submit agent proposals through the MCP server, nothing else. A token is valid until ExpiresAt, unless RevokedAt is set.

CREATE TABLE IF NOT EXISTS "unshackled-word"."ApiTokens"
(
    "Id"              int4          GENERATED ALWAYS AS IDENTITY,
    "UserId"          int4          NOT NULL,
    "Name"            varchar(100)  NOT NULL,
    "TokenHash"       varchar(64)   NOT NULL,
    "TokenPrefix"     varchar(12)   NOT NULL,
    "Scope"           varchar(20)   NOT NULL DEFAULT 'propose',
    "CreatedByUserId" int4          NOT NULL,
    "CreatedAt"       timestamptz   NOT NULL DEFAULT now(),
    "ExpiresAt"       timestamptz   NOT NULL,
    "RevokedAt"       timestamptz   NULL,
    "LastUsedAt"      timestamptz   NULL,
    CONSTRAINT "ApiTokens_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "ApiTokens_User_FK" FOREIGN KEY ("UserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "ApiTokens_CreatedByUser_FK" FOREIGN KEY ("CreatedByUserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "ApiTokens_Scope_CK" CHECK ("Scope" IN ('propose'))
);

CREATE UNIQUE INDEX IF NOT EXISTS "ApiTokens_TokenHash_UQ" ON "unshackled-word"."ApiTokens" ("TokenHash");
CREATE INDEX IF NOT EXISTS "ApiTokens_User_idx" ON "unshackled-word"."ApiTokens" ("UserId");

COMMENT ON TABLE "unshackled-word"."ApiTokens" IS 'Bearer tokens for the MCP server (scope propose). Only the SHA-256 hash is stored. Not part of the repo seed.';
COMMENT ON COLUMN "unshackled-word"."ApiTokens"."TokenHash" IS 'Lower-case hex SHA-256 of the full token string (uwt_ + 43 base64url characters).';
COMMENT ON COLUMN "unshackled-word"."ApiTokens"."TokenPrefix" IS 'First characters of the token (never enough to use it), so a maintainer can tell tokens apart in the list.';

-- A run created through the MCP server belongs to the token that built its packages; the daily proposal cap per token counts through this column.
ALTER TABLE "unshackled-word"."AgentRuns"
    ADD COLUMN IF NOT EXISTS "ApiTokenId" int4 NULL;

DO
$$
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'AgentRuns_ApiToken_FK') THEN
            ALTER TABLE "unshackled-word"."AgentRuns"
                ADD CONSTRAINT "AgentRuns_ApiToken_FK" FOREIGN KEY ("ApiTokenId") REFERENCES "unshackled-word"."ApiTokens" ("Id");
        END IF;
    END
$$;

CREATE INDEX IF NOT EXISTS "AgentRuns_ApiToken_idx" ON "unshackled-word"."AgentRuns" ("ApiTokenId") WHERE "ApiTokenId" IS NOT NULL;

COMMENT ON COLUMN "unshackled-word"."AgentRuns"."ApiTokenId" IS 'MCP token that built the packages of this run; NULL for runs of the batch worker and the debug endpoint.';

-- A merged proposal writes its lemma edits under the AI user (UserId, the author) and records the maintainer who accepted them here.
ALTER TABLE "unshackled-word"."Elb1871MorphologyEdits"
    ADD COLUMN IF NOT EXISTS "ReviewedByUserId" int4 NULL;

DO
$$
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'Elb1871MorphologyEdits_ReviewedByUser_FK') THEN
            ALTER TABLE "unshackled-word"."Elb1871MorphologyEdits"
                ADD CONSTRAINT "Elb1871MorphologyEdits_ReviewedByUser_FK" FOREIGN KEY ("ReviewedByUserId") REFERENCES "unshackled-word"."Users" ("Id");
        END IF;
    END
$$;

COMMENT ON COLUMN "unshackled-word"."Elb1871MorphologyEdits"."ReviewedByUserId" IS 'Maintainer who merged an agent proposal; NULL for direct edits (UserId is then the editor).';
