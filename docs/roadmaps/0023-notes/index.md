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
- **0023.1f done (2026-11-22):** version 0.9.1, `@mdxeditor/editor` 4.5.0, components `NoteEditor` / `NoteView` (both lazy chunks) and `NoteMarker`, side panel mode `notes` (`G` cycles TSK → Grundtext → Notizen → aus), `NotesPanel` in `pages/chapter`, `lib/notes` (API, TanStack Query hooks, invalidation of every chapter list after POST/PUT/DELETE), MDXEditor themed through the tokens (`.note-md` in `index.css`), toolbar labels in de/en via the editor's `translation` prop. Bundle (Vite build): eager JS before 1,298.51 kB (gzip 383.23), after 1,312.10 kB (gzip 388.64: `index` 1,245.87 + shared `useTranslation` 65.51 + runtime 0.72); CSS 61.31 → 64.46 kB; lazy: `NoteEditor` 867.25 kB (gzip 294.88, incl. CodeMirror for the source view), shared MDXEditor chunk 497.38 kB (gzip 154.91) + CSS 49.67 kB, `NoteView` 0.80 kB. Deviations: "Notiz zum Vers" sits in the verse's hover icon group (next to eye and pen), the marker is at the verse number; below 120rem the panel is a non-modal sheet (bottom sheet on phones, drawer at the right edge from 48rem), because the side rail only exists from 120rem; collapsed notes show a plain-text excerpt and MDXEditor only renders expanded notes; the actions are visible to signed-out readers and open a sign-in prompt; `NoteView` shows the raw body (no `bodyLinked` yet); no visibility select (0023.5). The acceptance walk-through with a signed-in user (bold word and list via the toolbar, reload) is still to be done in a browser; jsdom tests cover the real editor's load/toolbar/Ctrl+S and the panel flows with a mocked editor.
