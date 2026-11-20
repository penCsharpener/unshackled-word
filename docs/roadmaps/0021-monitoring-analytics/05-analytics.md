# 0021 · Analytics (Umami)

## Database

- Separate database `umami` with its own login role `umami` on the existing Postgres server. It's created once by the operator (documented SQL in deployment.md), because the volume already exists and init scripts don't run again.
- Umami runs its own Prisma migrations on startup. The app's migration tool never touches that database.
- Backups: the nightly `pg_dump` in deployment.md §8 covers only the app database. Add a second dump line for `umami`, or accept that losing analytics is fine (decide in 0021.9).

## Same-origin tracker

- The proxy maps `<domain>/stats/script.js` and `<domain>/stats/api/send` to Umami. Umami's env vars `TRACKER_SCRIPT_NAME` and `COLLECT_API_ENDPOINT` are set to match.
- `index.html` gets the tag only when `VITE_UMAMI_WEBSITE_ID` is set at build time (Vite `%VITE_…%` HTML replacement plus a build-time check, so the dev server never loads it):

```html
<script defer src="/stats/script.js" data-website-id="%VITE_UMAMI_WEBSITE_ID%"
        data-host-url="/stats" data-do-not-track="true" data-before-send="uwBeforeSend"></script>
```

- `data-do-not-track` respects the browser's DNT setting. `uwBeforeSend` (defined in `lib/analytics.ts`, attached to `window`) drops events on maintainer and admin routes (`/wartung…`, `/admin…`, mapping editor, proposal review) and drops everything for logged-in maintainers. Their clicks would skew reader numbers, and staff activity is already in the audit logs.
- Umami is cookieless and doesn't store IPs (it uses a daily rotating salted hash). No consent banner is needed. The privacy page still has to say what is collected.

## Custom events (`lib/analytics.ts`)

`track(name, data)` is a no-op when `window.umami` is missing (dev, blocked, DNT). Only these events exist, with bucketed or enumerated values and never free text:

| Event | Data | Why |
|---|---|---|
| `search` | `kind`: `verses` / `originals` / `references`, `results`: `0` / `1-10` / `>10` | Which search is used, how often it finds nothing (no query text) |
| `revision-level` | `level`: bucket (`1-99`, `100-199`, `200`, `201-500`, `>500`) | Where readers set the literalness slider |
| `word-detail` | `language`: `grc` / `hbo` | Use of original-language insights |
| `chapter-open` | `testament`: `ot` / `nt` | Rough reading spread (book and chapter come from the page view URL) |
| `login` | — | Rough count of how often logins happen |

## Privacy page

New route `/datenschutz` (en: `/privacy`), linked from the footer, with texts in `de.ts`/`en.ts`. Content: what Umami collects (page, referrer, browser, country, the events above, no cookies, no IP), server logs (shortened IPs from rate limiting, retention 14 days in Loki, rolling files), and that monitoring stays on the operator's host. The operator's legal details (Impressum, contact) are config or a placeholder text, not hardcoded for one person.
