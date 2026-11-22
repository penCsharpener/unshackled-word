# Roadmap 0023: Notes on chapters, verses and words

Folder roadmap (see the `roadmap-folder` skill): open only the file you need.

| File | Content |
|---|---|
| [01-goal.md](01-goal.md) | What is missing today, end state, deferred items |
| [02-decisions.md](02-decisions.md) | Maintainer decisions, plan decisions, code findings |
| [03-model-and-anchoring.md](03-model-and-anchoring.md) | `Notes` table, visibility predicate, word anchors, drift, revision levels |
| [04-references.md](04-references.md) | German reference scan, linking, display |
| [05-api-and-ui.md](05-api-and-ui.md) | Endpoints, reader markers and side panel, components, pages |
| [06-export-import.md](06-export-import.md) | `temp/notes` file format, maintainer import rules |
| [07-tasks.md](07-tasks.md) | Task table (backend and frontend woven) |
| [08-risks.md](08-risks.md) | Risks worth naming before starting |

## Status (2026-11-22)

All decisions settled: the maintainer accepted the plan decisions in [02-decisions.md](02-decisions.md) on 2026-11-22.

- **0023.1 done (2026-11-22):** version 0.9.0, migration `0042_Notes.sql`, `NoteRepository` with the visibility predicate, `NoteService` + validators (kinds `chapter`/`verse` only, verse/chapter must exist in Elb1871, body 1..20000), endpoints `POST/PUT/DELETE api/notes`, `GET api/notes/chapter/{bookId}/{chapter}` (`others` already honoured), Bruno requests in `api/unshackled-word/Notes`. Deviations: anchor refs are `startHebRefId`/`endHebRefId` in the request; the anchor CHECK got explicit `IS NOT NULL` guards plus `Notes_AnchorChapter_CK` and FKs for approver/hider (see 03); foreign notes answer 404 on PUT/DELETE (no 403, nothing leaks); `MigrationsHealthCheck.ExpectedMigration` = 42.
