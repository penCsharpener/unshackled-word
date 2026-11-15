# Roadmap 0014: Revision audit log (live table holds only the final version)

Folder format: this file only links; each section lives in its own file to keep files small.

| File | Content |
|---|---|
| [01-goal.md](01-goal.md) | Problem with real numbers, end state, out of scope |
| [02-decisions.md](02-decisions.md) | Evaluation of the idea, proposed decisions, findings in the code |
| [03-data-model.md](03-data-model.md) | `RevisionVerses` after the change and the new `RevisionVerseAudit` table |
| [04-write-paths.md](04-write-paths.md) | Text save, mapping save, withdraw, restore, seed import, rebase |
| [05-migration.md](05-migration.md) | Moving existing history into the audit table |
| [06-tasks.md](06-tasks.md) | Task table (backend and frontend woven) |
| [07-risks.md](07-risks.md) | Risks worth naming before starting |

## Status (2026-11-15)

Decisions confirmed (0014.0). 0014.1 to 0014.3f landed (uncommitted); 0014.4 and 0014.5 open.

Landed in 0014.1, 0014.2, 0014.2f (additive dual-write):

- Migration `0023_RevisionVerseAudit.sql`: table plus backfill in one chronological insert (text, mapping per reviewed row, withdraw per ended row without successor; runs only into an empty table). Checked on a copy of the dev DB (`uw_0014_test`): 4 verse rows, 3 reviewed, 0 withdrawals gives 7 audit rows; a simulated withdraw (rolled back) gave the extra `withdraw` row.
- `RevisionAuditWriter` (Infrastructure) builds the audit row and the compact mapping snapshot in one `INSERT ... SELECT` from the live rows, so newest audit row = live state by construction. Called in the transactions of text save, mapping save, withdraw (`EndVariantAsync` now has a transaction) and seed import (`WriteVariantAsync` text, `EndActiveAsync` withdraw, `ByUserId` NULL). Rebase writes nothing.
- `GET .../history?level=` reads only `RevisionVerseAudit`; contract in [03-data-model.md](03-data-model.md). `isActive`/`endedAt` are computed in `RevisionService.GetHistoryAsync`.
- `VersionHistory` shows mapping entries as sub-line, withdraw entries, de/en i18n.
- Tests: service tests for history, SQL-shape test, and `RevisionAuditDatabaseTests` (real Postgres, skipped unless `UNSHACKLEDWORD_TEST_POSTGRES` points at a copy of the dev DB with 0023) asserting newest audit row = live state after every write path; Vitest for mapping/withdraw entries.

Deviations: seed import with a reviewed variant writes only the `text` audit row (no extra `mapping` row; the backfill does write one for reviewed rows). The audit tests need a real DB, the repo has no Testcontainers, so they are opt-in via the env var.

Landed in 0014.3 and 0014.3f (live-only table, one commit with the code switch):

- Migration `0024_RevisionVersesLiveOnly.sql`: self-checks first (they abort before any change), new columns, `BasedOnRevisionVerseId` replaced by `BasedOnRevisionId` + `BasedOnVersion`, ended rows deleted, lifecycle columns/constraints/indexes dropped, `RevisionVerses_Verse_UQ`, partial indexes without `IsActive`, `ON DELETE CASCADE` on the word and mapping FKs, comments, final check. Run through the DbUp tool on a copy of the dev DB (`uw_0014_live`, from `uw_0014_test`): `RevisionVerses` 4 rows -> 3 = distinct (verse, revision), `RevisionWords` 99 -> 71, `RevisionMapping` 105 -> 75, `RevisionVerseAudit` unchanged (7 rows: 4 text, 3 mapping).
- Text save = `RevisionRepository.SaveVersionAsync` (renamed from `InsertVersionAsync`): `SELECT ... FOR UPDATE` on the live row, insert (version = highest audited + 1) or in-place update (`expectedVersion` checked under the lock, `Version + 1`, review reset, `UpdatedBy/At`), `DELETE` words (mapping cascades), insert words and carried mapping, audit `text`. The service no longer computes the version; `GetLastVersionAsync` and `EndActiveRevisionVerseId` are gone. The carry is computed in memory before the repository call, so a same-level re-save carries from the words it then deletes (tested on a real DB).
- Withdraw = audit `withdraw`, then `DELETE` (cascade). Mapping save: "row exists" replaces the `IsActive` check, and word ids are re-validated under the lock (409 after a concurrent text save).
- Seed import: `WriteVariantAsync` updates in place (version + 1, `Reviewed` from the seed, words replaced) or inserts (version continues from the audit); `EndActiveAsync` = withdraw; the parent is resolved at write time from `ParentLevel` (revision id + version of the parent's live row). Export joins `Revisions` by `BasedOnRevisionId` for `BasedOnLevel`. Seed export before and after 0024 on the copy DB: all files byte-identical except the `exportedAt` timestamp in `manifest.json` (same content version `d0229b6b3149`).
- All `IsActive`/`EndedAt`/`EndedByUserId` uses removed (`RevisionSql.EffectiveVerses`, `RevisionRepository`, `RevisionSeedRepository`, `RevisionExportRepository`, `RevisionBaseRealigner`, `BulkReplaceSearchRepository`, `RevisionOverviewRepository`, `RevisionAuditWriter`, `RevisionVerseDbo`, header row).
- Frontend (0014.3f): `RevisionVerseMappingResponse.isActive` removed from contract and types (always true now), `toMappingData` callers pass `true` / `canRevise`; `useSaveRevision` invalidates every cached mapping (`revisionKeys.mappings`) because the verse id stays and the word ids change, `useWithdrawRevision` removes cached mappings. `Components.md` note on `RevisionEditor`. Vitest `lib/revision/queries.test.tsx` covers both.
- Tests: service tests (expected version passed through, version from the repository, parent revision + version), repository SQL-shape tests, real-DB tests (`RevisionAuditDatabaseTests`, now needing 0024) for in-place save (stable id, rewritten words, review reset), stale `expectedVersion` 409 without audit row, withdraw + re-create continuing the version, stale word ids on mapping save, same-level re-save keeping the carried mapping, seed in-place write and re-create.

Deviations from the plan: (1) the migration self-checks run before the destructive steps instead of only at the end (a final check still runs at the end); (2) `CreatedBy/At` stay the first creation and the reads that show "author/date of this text" use `UpdatedBy/At` under the unchanged contract names `createdBy`/`createdAt`; (3) `RevisionVerses_BasedOn_FK` now references `Revisions`, plus a both-or-neither check on `BasedOnRevisionId`/`BasedOnVersion`; (4) `isActive` dropped from the mapping response instead of being kept always-true; (5) the SeedDb CLI could not start (`RevisionBaseRealigner` needs `IRevisionRepository`, not registered there; pre-existing), fixed with one registration in `UnshackledWord.Tooling.SeedDb/Extensions/ServiceRegistrationExtensions.cs`.

Open: release note must say "back up first" and "run `VACUUM (ANALYZE)` on the three revision tables once after 0024"; 0014.4 (docs/CLAUDE.md wording) and 0014.5 (first real run) are not started; the manual browser check of 0014.3f (edit, map, withdraw, re-create in the chapter page and bulk replace) was not done, only Vitest.


Landed in 0014.4 (docs): CLAUDE.md, README and roadmap 0011 (decision "Mapping history", risk, data-model note) point to
0014. Also: the migration runner now uses `WithTransactionPerScript()`, so a failing script (notably the destructive 0024)
rolls back completely; checked on a fresh copy of `uw_0014_test` (0023 + 0024 in one run: 3 live rows, 7 audit rows).

Dev DB `postgres` migrated 2026-11-15 (backup `temp/backups/dev-before-0014-20261115-1948.dump`), vacuumed. Browser pass
of 0014.3f done on Gen 1:1: create (v1), mapping save (audit `mapping`), same-level re-save in place (same id, v2, mapping
carried), stale `expectedVersion` 409, withdraw 204 / again 404, re-create continues at v3, history panel shows text,
mapping and withdraw entries; bulk-replace page and search see the live variant. Test variant withdrawn afterwards.
Side note: saving the mapping editor without any change does not mark the verse reviewed (pre-existing behaviour).

Not yet: 0014.5 (real run with row counts and Strong's-lookup timing on a DB that has a bulk-replace pass). Release note: back up first; run `VACUUM (ANALYZE)` on the three revision tables after 0024.
