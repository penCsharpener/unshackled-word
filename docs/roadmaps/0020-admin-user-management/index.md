# Roadmap 0020: Admin menu and user management

Folder roadmap (see the `roadmap-folder` skill): open only the file you need.

| File | Content |
|---|---|
| [01-goal.md](01-goal.md) | What is missing today, end state, out of scope |
| [02-decisions.md](02-decisions.md) | Maintainer decisions and code findings the plan rests on |
| [03-admin-menu-and-pages.md](03-admin-menu-and-pages.md) | Gear menu, admin pages, proposed components |
| [click-dummy.html](click-dummy.html) | UI draft: gear menu, user list, user page, add user, admin log (mock data) |
| [04-schema.md](04-schema.md) | DDL of migration `0035_UserAdministration.sql`, pre-check, `Details` shapes |
| [05-api.md](05-api.md) | Endpoints, contracts, validation, changed existing endpoints |
| [06-logic.md](06-logic.md) | Guards and locking, set roles, deactivation, sign-in enforcement, create user, list query, frontend rules |
| [07-tasks.md](07-tasks.md) | Task table (backend and frontend woven) |
| [08-risks.md](08-risks.md) | Risks worth naming before starting |

## Status

Implemented 2026-11-18 as 0.6.0. Deviations from the plan:

- Normalized email is lowercase (the existing `EmailNormalizer`), not uppercase.
- Create user commits the account before the welcome token/mail is sent.
- A failed mail on create/update does not fail the request; password reset returns 502 `mail_failed`.
- An email change also consumes the user's open emailed tokens.
- Password reset returns 409 `ai_user` for AI accounts.
- Reissuing an emailed invite logs `invite_revoked` for the replaced codes.
- Toasts are a real `Toast` component.
- No `UserAdminService`; the logic lives in `UserAdminRepository`.
- Sign-in tests cover refusals, not a successful cookie sign-in.
