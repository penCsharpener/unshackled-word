-- Maintainer review of notes (roadmap 0023.5) writes to the admin log: extend "AdminLog_Action_CK" (migration 0035) by
-- note_approved, note_rejected, note_hidden and note_unhidden. "TargetUserId" is the note's author, "Details" holds { noteId, reason }.

ALTER TABLE "unshackled-word"."AdminLog" DROP CONSTRAINT IF EXISTS "AdminLog_Action_CK";

ALTER TABLE "unshackled-word"."AdminLog"
    ADD CONSTRAINT "AdminLog_Action_CK" CHECK ("Action" IN (
        'user_created', 'user_updated', 'role_granted', 'role_revoked', 'user_deactivated', 'user_reactivated',
        'password_reset_sent', 'welcome_sent', 'invite_created', 'invite_revoked', 'invite_emailed',
        'note_approved', 'note_rejected', 'note_hidden', 'note_unhidden'
    ));
