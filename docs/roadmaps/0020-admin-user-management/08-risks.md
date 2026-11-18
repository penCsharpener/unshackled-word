# 0020 · Risks

- **Locking out the only admin is the one failure that needs SQL to undo.** The guards (self, last admin) and the row lock in [06-logic.md §1](06-logic.md#1-guards) exist for this; they must be backend checks with tests, not just disabled checkboxes. Before 0020.4 lands on a shared instance, note the SQL to re-grant `admin` in the README's troubleshooting section.

- **A forgotten sign-in path keeps deactivated users in.** There are six ways to get a session or a token (password, magic link, passkey, reset, verify email, existing cookie). 0020.6 needs one test per path; a new sign-in method added later must reuse the same `IsDeactivated` lookup, which is why it lives in `AuthRepository` and not in each endpoint.

- **Role changes sign the user out.** Rotating the stamp is the simplest way to refresh role claims, but granting a role to someone mid-edit throws them to the sign-in page and may lose an unsaved mapping. Acceptable at this scale; if it annoys, refresh the role claims in `OnValidatePrincipal` instead of rotating the stamp.

- **Admin-changed email is trusted without verification.** A typo hands the account to whoever owns the mistyped address once they request a reset. The notice to the old address and the log row make this visible; the admin can correct it. Re-verification of admin-entered addresses is not planned.

- **Welcome links live 72 h.** A leaked welcome mail lets someone set the password and sign in with the granted roles. Roles on a new account should be the minimum; resending supersedes the old link (`supersedeOpen`, [06-logic.md §5](06-logic.md#5-create-user)), and deactivation consumes all open tokens.

- **Superseding open tokens changes self-service behaviour.** Today two reset requests leave two working links; after 0020.2 only the newest works. A user who clicks the first mail gets "invalid link". Intended, but the mail text should say "nur der neueste Link ist gültig".

- **The role CHECK constraint can fail the migration.** A stray role value in `UserRoles` stops `0035` on that instance. Run the pre-check in [04-schema.md](04-schema.md) first.

- **The admin log is only as complete as its callers.** Any write that bypasses `UserAdminService` (seed import, SQL, the AI-user creation in roadmap 0016) leaves no row. The log is a record of admin UI actions, not a security audit; do not promise more in the UI text.

- **Deletion is deliberately missing.** A data-removal request (GDPR) would today need hand-written SQL that keeps authored rows. When the first such request comes, plan anonymization (replace name and email, keep `Users.Id`) as its own roadmap.
