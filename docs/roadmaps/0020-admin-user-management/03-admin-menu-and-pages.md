# 0020 · Admin menu and pages

Interactive draft: [click-dummy.html](click-dummy.html) (open in a browser; mock data, the dashed bar switches admin/maintainer and theme). Frontend behaviour rules: [06-logic.md §8](06-logic.md#8-frontend-rules-mirror-of-the-guards-for-disabling-only).

## Header

```
[Unshackled Word]  Bibel  …                 [◐ theme] [⚙ admin] [avatar ▾]
```

- `⚙` is shown only when `useAuth().user.roles` contains `admin`. Same button size and focus ring as `ThemeToggle`; inline SVG gear, `aria-label` "Administration".
- Menu entries, in order: Benutzer (`/admin/users`), Benutzer anlegen (`/admin/users/new`), Einladungen (`/admin/invitations`), Admin-Protokoll (`/admin/log`). Active route highlighted.
- The invitations entry is removed from `UserMenu`.
- Mobile: the gear stays in the header row; the menu opens as the same popover the user menu uses.

## Pages

**`/admin/users`** – table: display name, email, roles (badges), status (aktiv / deaktiviert / AI), created, last sign-in. Search box (name or email, debounced), role filter, status filter, paging (50 per page). Row click opens the detail page. Button "Benutzer anlegen".

**`/admin/users/:id`** – sections:
1. *Details*: display name, email (form, save). Button "Passwort-Reset-Mail senden".
2. *Rollen*: checkboxes admin / maintainer / reviewer; `user` shown fixed. Save applies the full set. Disabled with a hint where a guard applies (own admin role, last admin).
3. *Status*: "Deaktivieren" opens a confirm dialog with optional reason; a deactivated user shows reason, by whom and when, and "Reaktivieren".
4. *Protokoll*: last 20 admin-log entries for this user, link to the full log filtered by user.
A notice says that saving roles, email or status signs the user out.

AI users open read-only (no forms).

**`/admin/users/new`** – email, display name, role checkboxes; on success redirect to the new user's page with a toast "Willkommensmail gesendet". Duplicate email shows a field error with a link to the existing user.

**`/admin/log`** – table: time, actor, action, target user, details; filters by action and target user (`?userId=`), paging.

All pages: anonymous → sign-in, signed in without admin → the existing `admin.forbidden` note. All strings in `de.ts` and `en.ts`.

## Proposed components (need approval before implementation, CLAUDE.md)

| Component | Why | Instead of |
|---|---|---|
| `HeaderMenu` | Icon/avatar button with popover menu of links, extracted from `UserMenu`; props `trigger`, `label`, `items`. `UserMenu` and the admin menu both render it. | a second copy of the popover logic |
| `AdminMenu` | The gear trigger plus the admin items, admin-only. Could also be a `variant` of `HeaderMenu` used from `AppShell`. | – |
| `RequireRole` | Route wrapper: sign-in redirect / forbidden note / children. Replaces the inline check in `AdminInvitationsPage`. | repeating the check in four pages |
| `ConfirmDialog` | Only if `Components.md` has no dialog yet; used for deactivate. | – |

The user table reuses the existing table/paging components listed in `Components.md`; check there first.
