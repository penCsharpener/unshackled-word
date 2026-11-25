# Roadmap 0025: Greek editions per word

Folder roadmap (see the `roadmap-folder` skill): open only the file you need.

| File | Content |
|---|---|
| [01-goal.md](01-goal.md) | What the `Editions` column holds today (with numbers), end state, deferred items |
| [02-decisions.md](02-decisions.md) | Maintainer decisions from the interview, plan decisions, code findings |
| [03-model.md](03-model.md) | `GreekEditions` catalog, `StepGreekWordEditions` n:m table, token grammar, refresh function |
| [04-ui.md](04-ui.md) | Word detail section, marks in the Grundtext panel, edition filter |
| [05-tasks.md](05-tasks.md) | Task table (backend and frontend woven) |
| [06-risks.md](06-risks.md) | Risks worth naming before starting |

## Status (2026-11-25)

Implemented (all rows committed except 0025.7 docs, version 0.11.8):

- 0025.1 (0.11.0) migration 0047: `GreekEditions`, `StepGreekWordEditions`, `RefreshStepGreekWordEditions()`.
- 0025.2 (0.11.1) SeedDb refreshes after the Greek STEP import.
- 0025.3 (0.11.2) data quality checks for unknown editions and words without edition rows.
- 0025.4 (0.11.3) catalog endpoint and editions per Greek reading in the word detail; 0025.4f (0.11.4) `EditionList` in the word detail box.
- 0025.5 (0.11.5) edition codes and shift flag on Greek words of the chapter originals; 0025.5f (0.11.6) dotted mark and edition note in the Grundtext panel.
- 0025.6f (0.11.7) edition filter for the Greek text in the reader side panel.
- 0025.7 (0.11.8) docs and fresh-database check. Part A on an empty Postgres 18 (throwaway container, migrations 0001-0047, then full SeedDb): `StepGreekWordEditions` = 1,100,328 rows (same as the dev DB), 22 catalog rows, no `Kind='unknown'`, log line "Greek editions refreshed: 1100328 rows", no words without edition rows. SeedDb calls the refresh on every run, also when the STEP import is skipped as "already imported" (about 12 s on a second run).
- 0025 review (0.11.9): SeedDb refreshes the word editions only when Greek words were inserted in this run or `StepGreekWordEditions` is empty while `StepGreekWords` is not (self-healing); migration 0048 widens `GreekEditions.Code` and `StepGreekWordEditions.ShiftRaw` to varchar(100), so an over-long unknown token becomes an unknown catalog row instead of failing the refresh; the Grundtext note for the 11 words in no main edition says "fehlt in" all eight instead of an empty "nur in".

Deviations from the plan:

- The chapter-originals rule is re-implemented in SQL (same rule as `ReadingEditionsBuilder`); the two can drift.
- Helper SQL function `StepGreekWordEditionTokens()`.
- Constraint `Shift_CK` on `StepGreekWordEditions`; `GreekEditions.Id` is an `int2` identity.
- `IDbWriter.ExecuteScalarAsync` got an optional command timeout; the refresh uses 900 s.
- The catalog endpoint returns `{ editions: [...] }`; the word detail route is `api/elberfelder/wordDetail/...`.
- Act 1:1 #12 is not mapped in the dev DB, so substitute words were used in the checks; the TR-only word has seven missing editions (Byz is the main text).
- 0025.3 also routes the T1/T2 TSK findings in `DqCheckRepository.GetAsync`.
- The filter select lives in the side panel rail and filters only the wide-layout Grundtext side panel (>= 120rem); the inline eye panel is unfiltered and unmarked.
- `MigrationsHealthCheck.ExpectedMigration` raised to 47.
- On the dev DB 0047 was applied via psql, so DbUp's journal does not list it yet; the next migration run re-applies it idempotently (about 20 s).
