# 0020 · Decisions

## Maintainer decisions (2026-11-18)

- **Add user = create + email link.** The admin enters email, display name and roles; the account is created with the email counted as verified and without a password. The person gets a welcome mail with a set-password link. The admin never sees or sets a password.
- **Roles are the fixed set.** The admin assigns `admin`, `maintainer`, `reviewer`; `user` is implicit on every account and cannot be removed; `ai` is never shown as assignable (AI users are listed read-only).
- **Deactivate, not lock out.** An admin deactivates an account with an optional reason and reactivates it later. No timed or automatic lockout.
- **In scope as well:** invitations move to the gear menu, admins edit user details, every admin action goes to an append-only admin log.

## Decisions taken in the plan (change here if wrong)

- **Admin is superuser, only admins manage users.** Maintainers do not see the gear. `Roles(AppRoles.Admin)` on every new endpoint, like the invitation endpoints.
- **Self-protection guards.** An admin cannot deactivate themself or remove their own `admin` role, and the last active admin cannot lose `admin` or be deactivated. Checked in the backend; the UI only disables the controls.
- **Role changes and deactivation end the target's sessions** by rotating `Users.SecurityStamp`; `OnValidatePrincipal` already compares the stamp on every request. The person signs in again and gets fresh role claims. An email change rotates the stamp too.
- **Admin-changed email counts as verified**; a notice goes to the old and the new address.
- **Welcome link:** a `password_reset` token with a 72 h lifetime (normal resets: 1 h), mail text "Passwort festlegen". After expiry the person uses "Passwort vergessen" or a magic link; the admin can resend from the user page.
- **Deactivated accounts fail every sign-in path**: password login, magic link, passkey, password reset. Requests for magic link and reset mails send nothing (same response as an unknown address). Password login says "Konto deaktiviert" only after a correct password.
- **Writes stay attributed.** Deactivation never touches mappings, revisions, proposals or audits authored by the user.
- **Version:** roadmap bumps to 0.6.0 (CLAUDE.md versioning rule).

## Code findings the plan rests on

- Schema `0008_Auth.sql`: `Users` (no status column), `UserRoles (UserId, Role, GrantedAt)`, `AuthTokens` with purposes `email_verification`, `password_reset`, `magic_link`; `InviteCodes`.
- `AuthServiceCollectionExtensions` validates the cookie's `security_stamp` claim against the DB on every request, so rotating the stamp logs out everywhere.
- `api/auth/me` reflects session claims; roles in the frontend come from there (`hasWriteRole`, `user.roles.includes("admin")`).
- `UserMenu` holds the invitations link for admins; `AdminInvitationsPage` guards itself in the page. Header icons are inline SVG (`ThemeToggle`), there is no icon library.
- `Maintainer/ApiTokens/ListUsers` already lists users by role for token issuing; the admin list is a separate endpoint, not a widening of that one.
