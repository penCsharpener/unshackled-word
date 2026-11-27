# 0031 · Decisions

## Maintainer decisions (interview 2026-11-27)

- **Order of work is fixed:** 1) fake draft page, 2) the developer swaps the database by hand and keeps a backup, 3) remove the seed container from compose, 4) wire Bible books and Elb1871 text, 5) wire STEP Bible data, 6) wire everything else. **Implementation pauses after each of these steps** so the maintainer can click through the page.
- **Runners move into a new class library** `src/UnshackledWord.Seeding` (in the `.slnx`), referenced by the WebApi and by `Tooling.SeedDb`. Moved in slices: Elb1871 runners in step 4, STEP in step 5, the rest in step 6.
- **`BibleBooks` stay in migration 0001.** A fresh database has the 66 books; the page shows them as a green, read-only row ("aus Migration").
- **The SeedDb CLI stays** as a thin wrapper (`SeedingService` and the verbs call the library). Only the compose service goes.
- **New page `/edit/setup` ("Ersteinrichtung"), Admin only**, in the admin gear menu (roadmap 0020), not a section of `/edit/imports`.

## Plan decisions

- **Status comes from row counts, not from a job log.** The page recomputes state from `count(*)` per table on every load. A restart of the API (which forgets the in-memory `SeedJobRunner` state) therefore loses nothing that matters.
- **Only red steps can run.** A green step's button is disabled; an amber step can run only if its runner is known to be safe to resume (flag per step, [03-seed-catalog.md](03-seed-catalog.md)). No emptying in this roadmap.
- **One job at a time, shared with the existing seed jobs.** Setup jobs go through `SeedJobRunner`/`SeedExportCoordinator`, so a setup run, a seed import and a zip export never overlap.
- **A failed step fails loudly.** The runner exception ends the job with status `failed`, the step row turns red/amber with the last log lines; "Alle ausführen" stops at the first failure.
- **The SeedDb CLI exits non-zero on failure** (today `Worker` catches and stops with 0). Cheap fix, done in 0031.4.
- Version 0.16.0 at 0031.1, build number per row afterwards.

## Code findings the plan rests on

- `SeedingService._runnerTypes` (SeedDb) is the only ordering; `FilterSeedRunnerTypes` in `SeedingService.local.cs` is the local override (commented out).
- Runners resolve `IOptions<AppSettings>` with `DatabaseSeeding.SolutionAssetsPath`/`SolutionTempPath`; the WebApi container already mounts `./assets` and `./temp` at `/src/…` and sets both paths, and runs as `1000:1000`.
- `StepImportResult` is shared state between runners in one process (`InsertedGreekWords` decides the Greek-edition refresh and the verse-similarity rebuild). Running steps in separate jobs breaks that coupling; the catalog states the dependency instead ([03-seed-catalog.md](03-seed-catalog.md)).
- `SourceFormIndex` caches for 30 minutes (`MaxAge`), `StrongsLexiconIndex` and `SttsDriftCheck` (startup hosted service) read lexicon/tag data once. After a seed job they hold the empty state.
- The 0029.11 fresh run needed `Command Timeout=900` in the connection string for the word bulk insert.
- `/edit/imports` (`SeedImportsPage`) and "Wartung" (`SeedRoundTripSection`, `LxxSeedJobsSection`) already poll `api/maintainer/seed/status`; `SeedJobResultView` renders a job result.
