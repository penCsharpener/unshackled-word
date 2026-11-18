-- User administration (roadmap 0020): deactivation, last sign-in, who granted a role, and the append-only admin log.
-- Deactivation is a manual, indefinite admin switch (no timed or automatic lockout). A deactivated account keeps its id and every row it
-- authored; it only cannot sign in. Reactivation clears the three Deactivation columns.

ALTER TABLE "unshackled-word"."Users"
    ADD COLUMN IF NOT EXISTS "DeactivatedAt"       timestamptz  NULL,
    ADD COLUMN IF NOT EXISTS "DeactivatedByUserId" int4         NULL,
    ADD COLUMN IF NOT EXISTS "DeactivationReason"  varchar(500) NULL,
    ADD COLUMN IF NOT EXISTS "LastSignInAt"        timestamptz  NULL;

DO
$$
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'Users_DeactivatedByUser_FK') THEN
            ALTER TABLE "unshackled-word"."Users"
                ADD CONSTRAINT "Users_DeactivatedByUser_FK" FOREIGN KEY ("DeactivatedByUserId") REFERENCES "unshackled-word"."Users" ("Id");
        END IF;

        -- Either all deactivation columns describe one deactivation, or none is set (reason stays optional).
        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'Users_Deactivation_CK') THEN
            ALTER TABLE "unshackled-word"."Users"
                ADD CONSTRAINT "Users_Deactivation_CK" CHECK (
                    ("DeactivatedAt" IS NULL AND "DeactivatedByUserId" IS NULL AND "DeactivationReason" IS NULL)
                    OR ("DeactivatedAt" IS NOT NULL AND "DeactivatedByUserId" IS NOT NULL)
                );
        END IF;
    END
$$;

CREATE INDEX IF NOT EXISTS "Users_Deactivated_idx" ON "unshackled-word"."Users" ("DeactivatedAt") WHERE "DeactivatedAt" IS NOT NULL;

COMMENT ON COLUMN "unshackled-word"."Users"."DeactivatedAt" IS 'Set by an admin; a deactivated account fails every sign-in path and its sessions end. NULL = active.';
COMMENT ON COLUMN "unshackled-word"."Users"."DeactivationReason" IS 'Optional note by the admin, shown only to admins.';
COMMENT ON COLUMN "unshackled-word"."Users"."LastSignInAt" IS 'Updated by every successful sign-in (password, magic link, passkey, reset).';


ALTER TABLE "unshackled-word"."UserRoles"
    ADD COLUMN IF NOT EXISTS "GrantedByUserId" int4 NULL;

DO
$$
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'UserRoles_GrantedByUser_FK') THEN
            ALTER TABLE "unshackled-word"."UserRoles"
                ADD CONSTRAINT "UserRoles_GrantedByUser_FK" FOREIGN KEY ("GrantedByUserId") REFERENCES "unshackled-word"."Users" ("Id");
        END IF;

        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'UserRoles_Role_CK') THEN
            ALTER TABLE "unshackled-word"."UserRoles"
                ADD CONSTRAINT "UserRoles_Role_CK" CHECK ("Role" IN ('admin', 'maintainer', 'reviewer', 'user', 'ai'));
        END IF;
    END
$$;

CREATE INDEX IF NOT EXISTS "UserRoles_Role_idx" ON "unshackled-word"."UserRoles" ("Role");

COMMENT ON COLUMN "unshackled-word"."UserRoles"."GrantedByUserId" IS 'Admin who granted the role; NULL for roles from registration, the first-account bootstrap and AI-user creation.';


CREATE TABLE IF NOT EXISTS "unshackled-word"."AdminLog"
(
    "Id"           int8         GENERATED ALWAYS AS IDENTITY,
    "ActorUserId"  int4         NOT NULL,
    "TargetUserId" int4         NULL,
    "InviteCodeId" int4         NULL,
    "Action"       varchar(40)  NOT NULL,
    "Details"      jsonb        NOT NULL DEFAULT '{}'::jsonb,
    "CreatedAt"    timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT "AdminLog_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "AdminLog_ActorUser_FK" FOREIGN KEY ("ActorUserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "AdminLog_TargetUser_FK" FOREIGN KEY ("TargetUserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "AdminLog_InviteCode_FK" FOREIGN KEY ("InviteCodeId") REFERENCES "unshackled-word"."InviteCodes" ("Id"),
    CONSTRAINT "AdminLog_Action_CK" CHECK ("Action" IN (
        'user_created', 'user_updated', 'role_granted', 'role_revoked', 'user_deactivated', 'user_reactivated',
        'password_reset_sent', 'welcome_sent', 'invite_created', 'invite_revoked', 'invite_emailed'
    ))
);

CREATE INDEX IF NOT EXISTS "AdminLog_CreatedAt_idx" ON "unshackled-word"."AdminLog" ("CreatedAt" DESC, "Id" DESC);
CREATE INDEX IF NOT EXISTS "AdminLog_TargetUser_idx" ON "unshackled-word"."AdminLog" ("TargetUserId", "CreatedAt" DESC) WHERE "TargetUserId" IS NOT NULL;

COMMENT ON TABLE "unshackled-word"."AdminLog" IS 'Append-only record of admin actions in the user administration and invitations (roadmap 0020). Local data, not part of the repo seed.';
COMMENT ON COLUMN "unshackled-word"."AdminLog"."Details" IS 'Action-specific JSON, see 05-logic.md of roadmap 0020; never contains tokens or password hashes.';

-- Append-only: refuse UPDATE and DELETE at the database, not only in the repository.
CREATE OR REPLACE FUNCTION "unshackled-word"."AdminLog_AppendOnly"()
    RETURNS trigger
    LANGUAGE plpgsql
AS
$$
BEGIN
    RAISE EXCEPTION 'AdminLog is append-only (% refused)', TG_OP;
END
$$;

DROP TRIGGER IF EXISTS "AdminLog_AppendOnly_TR" ON "unshackled-word"."AdminLog";
CREATE TRIGGER "AdminLog_AppendOnly_TR"
    BEFORE UPDATE OR DELETE ON "unshackled-word"."AdminLog"
    FOR EACH ROW
EXECUTE FUNCTION "unshackled-word"."AdminLog_AppendOnly"();
