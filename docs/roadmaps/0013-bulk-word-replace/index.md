# Roadmap 0013: Bulk word replace for the revision

Folder format: this file only links; each section lives in its own file to keep files small.

| File | Content |
|---|---|
| [01-goal.md](01-goal.md) | Problem, end state, out of scope |
| [02-decisions.md](02-decisions.md) | Decisions taken with the maintainer and findings in the code |
| [03-form-design.md](03-form-design.md) | Form v2: find, review text, review mapping (first draft by Opus, reworked) |
| [click-dummy.html](click-dummy.html) | Interactive click dummy of the form (mock data, open in a browser) |
| [04-tasks.md](04-tasks.md) | Task table (backend and frontend woven) |
| [05-risks.md](05-risks.md) | Risks worth naming before starting |

## Status (2026-11-15)

Implemented: 0013.1, 0013.1f, 0013.2, 0013.2f, 0013.3f, 0013.4f and most of 0013.5f: search endpoint with tests, page `/edit/revision/replace`,
menu link, facets, queue in `localStorage`, text step, mapping step (the "Zuordnung bestätigen" button only shows while the editor has no
pending edits), arrow-key navigation in the hit list, `Ctrl+Enter` for the next step, i18n, Vitest. Migration `0022_BulkReplaceIndexes.sql`
adds `lower("PlainWord")` indexes and the search query pushes its predicates down: `Weib` now takes about 30 ms instead of 1.4 s.

Not yet: the virtualised list (the list renders all rows, max 1000), a separate compact quick-review mode (the toolbar filter "Nur
Treffer ohne Artikel davor" plus `Ctrl+Enter` stand in for it) and the real run 0013.6 (`06-first-run.md`).
