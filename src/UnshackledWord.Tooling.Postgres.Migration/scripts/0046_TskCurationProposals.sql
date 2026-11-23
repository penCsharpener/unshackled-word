-- Suggestions for TSK curation by users without a maintainer role (roadmap 0024.6, docs/roadmaps/0024-tsk-curation/03-model.md).
--   TskCurationProposals  one row per suggestion: the full desired state of one link (same key and columns as "TskCurations", migration 0045)
--                         plus "Delete" (withdraw an added link). A maintainer accepts (applied with audit "Source" 'proposal') or rejects it.
-- "BaseUpdatedAt" is "TskCurations"."UpdatedAt" of the link when the suggestion was made (NULL = the link had no curation row); accepting a
-- proposal whose link changed meanwhile ("UpdatedAt" IS DISTINCT FROM "BaseUpdatedAt") is refused unless the maintainer forces it.
-- One pending proposal per user and link ("TskCurationProposals_Pending_UX"): a new suggestion supersedes the earlier pending one.
-- Like the audit, proposals are local data and not part of the repo seed.
-- Accepting and rejecting write the admin log: extend "AdminLog_Action_CK" (migrations 0035, 0043) by tsk_proposal_accepted and
-- tsk_proposal_rejected ("TargetUserId" = the proposal's author, "Details" = { proposalId, key }).
--
-- Idempotent: safe to run again.

CREATE TABLE IF NOT EXISTS "unshackled-word"."TskCurationProposals"
(
    "Id"                  int4         GENERATED ALWAYS AS IDENTITY,
    "SourceHebRefId"      int4         NOT NULL,
    "TargetStartHebRefId" int4         NOT NULL,
    "TargetEndHebRefId"   int4         NULL,
    "Origin"              varchar(5)   NOT NULL DEFAULT 'tsk',
    "Rating"              varchar(9)   NULL,
    "Comment"             varchar(500) NULL,
    "StartPosition"       int4         NULL,
    "EndPosition"         int4         NULL,
    "QuotedText"          varchar(500) NULL,
    "Delete"              bool         NOT NULL DEFAULT false,
    "BaseUpdatedAt"       timestamptz  NULL,
    "UserId"              int4         NOT NULL,
    "CreatedAt"           timestamptz  NOT NULL DEFAULT now(),
    "State"               varchar(10)  NOT NULL DEFAULT 'pending',
    "DecidedByUserId"     int4         NULL,
    "DecidedAt"           timestamptz  NULL,
    "Reason"              varchar(500) NULL,
    CONSTRAINT "TskCurationProposals_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "TskCurationProposals_User_FK" FOREIGN KEY ("UserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "TskCurationProposals_DecidedBy_FK" FOREIGN KEY ("DecidedByUserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "TskCurationProposals_Origin_CK" CHECK ("Origin" IN ('tsk', 'added')),
    CONSTRAINT "TskCurationProposals_Rating_CK" CHECK ("Rating" IS NULL OR "Rating" IN ('highlight', 'hide')),
    CONSTRAINT "TskCurationProposals_Comment_CK" CHECK ("Comment" IS NULL OR char_length(btrim("Comment")) > 0),
    CONSTRAINT "TskCurationProposals_Word_CK" CHECK (
           ("StartPosition" IS NULL AND "EndPosition" IS NULL AND "QuotedText" IS NULL)
        OR ("StartPosition" IS NOT NULL AND "EndPosition" IS NOT NULL AND "QuotedText" IS NOT NULL AND "StartPosition" BETWEEN 1 AND "EndPosition")),
    CONSTRAINT "TskCurationProposals_Range_CK" CHECK ("TargetEndHebRefId" IS NULL
        OR ("TargetEndHebRefId" > "TargetStartHebRefId" AND "TargetEndHebRefId" / 1000 = "TargetStartHebRefId" / 1000)),
    CONSTRAINT "TskCurationProposals_Delete_CK" CHECK (NOT "Delete" OR "Origin" = 'added'),
    CONSTRAINT "TskCurationProposals_State_CK" CHECK ("State" IN ('pending', 'accepted', 'rejected', 'superseded')),
    CONSTRAINT "TskCurationProposals_Decided_CK" CHECK (("State" = 'pending') = ("DecidedAt" IS NULL)),
    CONSTRAINT "TskCurationProposals_Reason_CK" CHECK ("Reason" IS NULL OR char_length(btrim("Reason")) > 0)
);

-- one pending proposal per user and link
CREATE UNIQUE INDEX IF NOT EXISTS "TskCurationProposals_Pending_UX" ON "unshackled-word"."TskCurationProposals"
    ("UserId", "SourceHebRefId", "TargetStartHebRefId", COALESCE("TargetEndHebRefId", 0))
    WHERE "State" = 'pending';

-- the review list (pending, by source verse) and the pending proposals of one chapter
CREATE INDEX IF NOT EXISTS "TskCurationProposals_PendingSource_idx" ON "unshackled-word"."TskCurationProposals"
    ("SourceHebRefId", "TargetStartHebRefId", "CreatedAt")
    WHERE "State" = 'pending';

-- the own list of a user, newest first
CREATE INDEX IF NOT EXISTS "TskCurationProposals_User_idx" ON "unshackled-word"."TskCurationProposals"
    ("UserId", "CreatedAt" DESC);

COMMENT ON TABLE "unshackled-word"."TskCurationProposals" IS 'Suggested TSK curation changes by non-maintainers (roadmap 0024.6): full desired state of one link. Local data, not part of the repo seed.';
COMMENT ON COLUMN "unshackled-word"."TskCurationProposals"."Delete" IS 'true = withdraw the added link (Origin added); the state columns then hold the state at proposal time.';
COMMENT ON COLUMN "unshackled-word"."TskCurationProposals"."BaseUpdatedAt" IS 'TskCurations.UpdatedAt of the link when proposed; NULL = no curation row. A differing current value makes the proposal stale.';
COMMENT ON COLUMN "unshackled-word"."TskCurationProposals"."State" IS 'pending | accepted | rejected | superseded (replaced by a newer proposal of the same user, or withdrawn).';
COMMENT ON COLUMN "unshackled-word"."TskCurationProposals"."DecidedByUserId" IS 'The maintainer who accepted or rejected; NULL while pending and for superseded proposals.';
COMMENT ON COLUMN "unshackled-word"."TskCurationProposals"."Reason" IS 'Optional reason of a rejection (max 500 chars), shown to the author.';

ALTER TABLE "unshackled-word"."AdminLog" DROP CONSTRAINT IF EXISTS "AdminLog_Action_CK";

ALTER TABLE "unshackled-word"."AdminLog"
    ADD CONSTRAINT "AdminLog_Action_CK" CHECK ("Action" IN (
        'user_created', 'user_updated', 'role_granted', 'role_revoked', 'user_deactivated', 'user_reactivated',
        'password_reset_sent', 'welcome_sent', 'invite_created', 'invite_revoked', 'invite_emailed',
        'note_approved', 'note_rejected', 'note_hidden', 'note_unhidden',
        'tsk_proposal_accepted', 'tsk_proposal_rejected'
    ));
