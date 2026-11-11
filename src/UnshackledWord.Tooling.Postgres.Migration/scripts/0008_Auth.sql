-- Accounts, roles, single-use emailed tokens, passkeys and the invite-code admission gate.
-- Ported from lexio (0002_Users, 0003_UserSecurityStamp, 0018_Passkeys, 0021_InviteCodes) without the
-- language-learning parts (expert languages, UI language).

CREATE TABLE IF NOT EXISTS "unshackled-word"."Users"
(
    "Id"              serial4                  NOT NULL,
    "Email"           varchar                  NOT NULL,
    "EmailNormalized" varchar                  NOT NULL,
    "PasswordHash"    varchar                  NULL,
    "DisplayName"     varchar                  NOT NULL,
    "EmailVerifiedAt" timestamptz              NULL,
    "SecurityStamp"   varchar                  NOT NULL DEFAULT gen_random_uuid()::varchar,
    "CreatedAt"       timestamptz              NOT NULL DEFAULT now(),
    "UpdatedAt"       timestamptz              NULL,
    CONSTRAINT "Users_PK" PRIMARY KEY ("Id")
);

COMMENT
ON COLUMN "unshackled-word"."Users"."PasswordHash" IS 'Nullable: passwordless sign-in (magic link, passkey) needs no password.';
COMMENT
ON COLUMN "unshackled-word"."Users"."SecurityStamp" IS 'Regenerated on security-relevant changes (password reset); sessions issued before the change stop validating.';

CREATE UNIQUE INDEX IF NOT EXISTS "Users_EmailNormalized_idx" ON "unshackled-word"."Users" ("EmailNormalized");


-- Role is one of: 'admin', 'maintainer', 'reviewer', 'user' (see AppRoles).
CREATE TABLE IF NOT EXISTS "unshackled-word"."UserRoles"
(
    "UserId"    int4        NOT NULL,
    "Role"      varchar     NOT NULL,
    "GrantedAt" timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "UserRoles_PK" PRIMARY KEY ("UserId", "Role"),
    CONSTRAINT "UserRoles_Users_FK" FOREIGN KEY ("UserId") REFERENCES "unshackled-word"."Users" ("Id")
);


-- One table for every single-use emailed token: 'email_verification', 'password_reset', 'magic_link'.
CREATE TABLE IF NOT EXISTS "unshackled-word"."AuthTokens"
(
    "Id"         serial4     NOT NULL,
    "UserId"     int4        NOT NULL,
    "Purpose"    varchar     NOT NULL,
    "TokenHash"  varchar     NOT NULL,
    "ExpiresAt"  timestamptz NOT NULL,
    "ConsumedAt" timestamptz NULL,
    "CreatedAt"  timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "AuthTokens_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "AuthTokens_Users_FK" FOREIGN KEY ("UserId") REFERENCES "unshackled-word"."Users" ("Id")
);

COMMENT
ON COLUMN "unshackled-word"."AuthTokens"."TokenHash" IS 'Only the SHA-256 hash is stored, never the raw token.';

CREATE UNIQUE INDEX IF NOT EXISTS "AuthTokens_TokenHash_idx" ON "unshackled-word"."AuthTokens" ("TokenHash");
CREATE INDEX IF NOT EXISTS "AuthTokens_UserId_Purpose_idx" ON "unshackled-word"."AuthTokens" ("UserId", "Purpose");


-- Passkeys (WebAuthn/FIDO2): durable credentials, registered per authenticator against a user.
CREATE TABLE IF NOT EXISTS "unshackled-word"."PasskeyCredentials"
(
    "Id"           serial4     NOT NULL,
    "UserId"       int4        NOT NULL,
    "CredentialId" bytea       NOT NULL,
    "PublicKey"    bytea       NOT NULL,
    "SignCount"    int8        NOT NULL DEFAULT 0,
    "Transports"   text[]      NOT NULL DEFAULT '{}',
    "AaGuid"       uuid        NOT NULL,
    "Name"         varchar     NOT NULL,
    "CreatedAt"    timestamptz NOT NULL DEFAULT now(),
    "LastUsedAt"   timestamptz NULL,
    CONSTRAINT "PasskeyCredentials_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "PasskeyCredentials_Users_FK" FOREIGN KEY ("UserId") REFERENCES "unshackled-word"."Users" ("Id")
);

COMMENT
ON COLUMN "unshackled-word"."PasskeyCredentials"."SignCount" IS 'Authenticator signature counter, used for clone detection.';
COMMENT
ON COLUMN "unshackled-word"."PasskeyCredentials"."Name" IS 'User-chosen label, e.g. "Laptop Touch ID".';

CREATE UNIQUE INDEX IF NOT EXISTS "PasskeyCredentials_CredentialId_idx" ON "unshackled-word"."PasskeyCredentials" ("CredentialId");
CREATE INDEX IF NOT EXISTS "PasskeyCredentials_UserId_idx" ON "unshackled-word"."PasskeyCredentials" ("UserId");


-- Admission gate: invite codes. An emailed invitation is a row here with MaxSeats = 1 and BoundEmailNormalized set.
CREATE TABLE IF NOT EXISTS "unshackled-word"."InviteCodes"
(
    "Id"                   serial4     NOT NULL,
    "Code"                 varchar     NOT NULL,
    "BoundEmailNormalized" varchar     NULL,
    "MaxSeats"             int4        NOT NULL,
    "ExpiresAt"            timestamptz NOT NULL,
    "CreatedBy"            int4        NOT NULL,
    "CreatedAt"            timestamptz NOT NULL DEFAULT now(),
    "RevokedAt"            timestamptz NULL,
    "Note"                 varchar     NULL,
    CONSTRAINT "InviteCodes_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "InviteCodes_Users_FK" FOREIGN KEY ("CreatedBy") REFERENCES "unshackled-word"."Users" ("Id")
);

COMMENT
ON COLUMN "unshackled-word"."InviteCodes"."Code" IS 'Stored in plaintext so an admin can read it back to share it.';

CREATE UNIQUE INDEX IF NOT EXISTS "InviteCodes_Code_idx" ON "unshackled-word"."InviteCodes" ("Code");

CREATE TABLE IF NOT EXISTS "unshackled-word"."InviteCodeRedemptions"
(
    "InviteCodeId" int4        NOT NULL,
    "UserId"       int4        NOT NULL,
    "RedeemedAt"   timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "InviteCodeRedemptions_PK" PRIMARY KEY ("InviteCodeId", "UserId"),
    CONSTRAINT "InviteCodeRedemptions_InviteCodes_FK" FOREIGN KEY ("InviteCodeId") REFERENCES "unshackled-word"."InviteCodes" ("Id"),
    CONSTRAINT "InviteCodeRedemptions_Users_FK" FOREIGN KEY ("UserId") REFERENCES "unshackled-word"."Users" ("Id")
);
