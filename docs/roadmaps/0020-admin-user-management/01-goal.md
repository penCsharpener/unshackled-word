# 0020 · Goal

## Missing today

- Roles live in `UserRoles` (`admin`, `maintainer`, `reviewer`, `user`, plus the internal `ai`, see `AppRoles`), but nothing in the app grants or revokes them. The only way to make someone a maintainer is SQL against the database.
- An admin cannot see who has an account. The only admin feature is invitations (`api/admin/invite-codes`, page `/admin/invitations`), reached from the user menu.
- An account cannot be switched off. A maintainer who leaves keeps write access until somebody edits the database.
- An admin cannot create an account for a person directly; that person has to register with an invite code.
- Nothing records who changed what in the account system.

## End state

An admin sees a gear icon in the header between the theme toggle and the user menu. It opens the admin menu:

- **Benutzer / Users** – list of all accounts with search and filters; per user: details (name, email), roles, status, last sign-in, recent admin actions. The admin edits name and email, sends a password-reset mail, grants and revokes roles, deactivates and reactivates the account.
- **Benutzer anlegen / Add user** – email, display name, roles. The account exists at once; the person gets a welcome mail with a link to set a password (magic link and passkey work as usual afterwards).
- **Einladungen / Invitations** – the existing page, moved here from the user menu.
- **Admin-Protokoll / Admin log** – append-only list of every admin action (who, when, on whom, what).

Non-admins never see the gear, and every `api/admin/*` endpoint answers 403 for them.

## Out of scope

- Lockout as a security feature (timed lockout, automatic lockout after failed logins). Deactivation here is a manual, indefinite admin switch.
- Editable roles or a permission model; roles stay the fixed set in `AppRoles`.
- Deleting or anonymizing accounts (authored mappings, revisions and audits reference `Users.Id`).
- Reviewer hierarchy and what reviewers may do (CLAUDE.md: "later").
- Managing AI users (roadmap 0016) and API tokens; they stay on the maintainer page.
