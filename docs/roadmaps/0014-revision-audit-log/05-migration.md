# 0014 · Migration

Two scripts, so the app keeps running in between (slicing in [06-tasks.md](06-tasks.md)).

## 0023_RevisionVerseAudit.sql (additive)

- Create `RevisionVerseAudit` ([03-data-model.md](03-data-model.md)).
- Backfill, in `Id` order: one `text` row per existing `RevisionVerses` row (`ByUserId = CreatedByUserId`,
  `At = CreatedAt`, mapping snapshot built from that row's words and mapping); for each row with `MappingReviewedAt`
  one `mapping` row (`At = MappingReviewedAt`); for each ended row with no successor of the same verse and revision one
  `withdraw` row (`ByUserId = EndedByUserId`, `At = EndedAt`).
- `BasedOnVersion`/`BasedOnRevisionId` of the backfill come from the referenced parent row.

## 0024_RevisionVersesLiveOnly.sql (destructive)

Runs together with the code switch (0014.3).

1. Add `BasedOnRevisionId`, `BasedOnVersion`, `UpdatedByUserId`, `UpdatedAt`; fill from the parent row / from the
   active row's `CreatedBy/At`.
2. Drop `RevisionVerses_BasedOn_FK` and `BasedOnRevisionVerseId`.
3. Delete mapping, then words, then verses of all rows with `NOT "IsActive"`.
4. Drop `IsActive`, `EndedAt`, `EndedByUserId`, lifecycle check, version unique, active partial unique; add
   `RevisionVerses_Verse_UQ`; recreate `Search_idx` and `Unreviewed_idx` without `WHERE "IsActive"`.
5. Recreate the word/mapping FKs with `ON DELETE CASCADE`. Update table comments.
6. `VACUUM` is not possible inside the migration transaction; note in the release text to run `VACUUM (ANALYZE)` once.

Check queries at the end of the script (fail the migration if violated): audit rows ≥ old verse rows; every live verse
has an audit `text` row with its `Version`.

Docker self-hosters: their own history is preserved in the audit table; the repo seed is unaffected (it only ever held
live rows).
