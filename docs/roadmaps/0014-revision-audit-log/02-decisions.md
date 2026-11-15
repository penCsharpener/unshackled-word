# 0014 · Decisions

## Evaluation of the idea

Worth doing, and the gain is larger than "RevisionVerses gets smaller":

- `RevisionVerses` alone is cheap: its hot indexes are already partial on `IsActive`. The real growth is `RevisionWords`
  and `RevisionMapping`, 23 and 24 rows per verse version, without any lifecycle column (numbers in [01-goal.md](01-goal.md)).
  The split removes that history from the tables that word- and Strong's-level queries scan.
- Every query loses its `IsActive` filter; "one row per (verse, revision)" becomes a plain unique constraint instead of a
  partial index plus a lifecycle check.
- Ids of the live verse row become stable across saves (bookmarks, queue entries of bulk replace, URLs).
- Mapping saves get a history for free, which closes the gap 0011 accepted ("no mapping history inside a version").

Costs: text saves become UPDATE + rewrite of words and mapping instead of pure INSERT; `RevisionWords.Id` changes on every
text save; the migration is one-way. A cheaper alternative (keep the model, add `IsActive` to words/mapping or partition
by it) fixes indexes but not the duplication; rejected because the project prefers simple tables over partitioning.

## Proposed decisions (wait for the maintainer)

| Topic | Proposal |
|---|---|
| Audit content | State *after* the action: text, note, base hash, parent revision, mapping snapshot. The newest audit row of a live verse equals the live row, so history is one single-table query. |
| Mapping snapshot | Compact `jsonb` keyed by word position (not `RevisionWords.Id`, which changes), on every audit row. ~0.5 kB per row before TOAST. |
| Words in the audit | Not stored: they are the tokenizer output of the text; `BasePositionInVerse`/`Change` are recomputable (carry-over). |
| Version number | Stays on the live row (optimistic concurrency `expectedVersion`) and is copied into the audit. Text save +1; mapping save keeps the version. |
| Withdraw | Deletes the live row (words, mapping by cascade) and writes an audit row `withdraw`. A later save of the same verse continues from the highest audited version. |
| Parent link | `BasedOnRevisionVerseId` (points at a historic row) becomes `BasedOnRevisionId` + `BasedOnVersion`, matching the seed column `BasedOnLevel`. |
| Seed | Audit is local data, not exported. Seed import writes audit rows with `ByUserId = NULL`. Rebase (`RevisionBaseRealigner`) only recomputes derived columns and writes no audit row. |
| Retention | Keep everything; pruning is a later decision. |

## Findings in the code the plan rests on

- Writers of `RevisionVerses`: `RevisionRepository` (save, withdraw, mapping), `RevisionSeedRepository` (seed import,
  `WriteVariantAsync` appends a version), `RevisionBaseRealigner` (updates words of active rows).
- `IsActive` is read in `RevisionSql`, `RevisionRepository`, `RevisionExportRepository`, `RevisionSeedRepository`,
  `RevisionBaseRealigner`, `BulkReplaceSearchRepository`, `RevisionOverviewRepository`.
- `BasedOnRevisionVerseId` is only read by the seed export (to get the parent level).
- History UI: `VersionHistory.tsx` uses `revisionVerseId`, `version`, `text`, `note`, `isActive`, `createdAt`,
  `createdBy`, `endedAt` from `GET …/history?level=`.
- Mapping editor refuses saves on ended versions (`RevisionMappingService`, `header.IsActive`).
