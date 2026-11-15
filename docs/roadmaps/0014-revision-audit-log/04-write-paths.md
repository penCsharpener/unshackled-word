# 0014 · Write paths

Every path runs in one transaction and ends with exactly one audit insert (except rebase). Table shapes:
[03-data-model.md](03-data-model.md).

## Text save (`RevisionService.SaveTextAsync`, `RevisionRepository`)

1. `SELECT … FROM "RevisionVerses" WHERE ("HebRefId","RevisionId") = … FOR UPDATE`.
2. No row: `Version` = highest audited version + 1 (so a re-created withdrawn verse continues), `INSERT` the verse.
   Row exists: `expectedVersion` must equal `Version`, else 409; `UPDATE` text, note, hash, parent, `Version + 1`,
   `UpdatedBy/At`, reset `MappingReviewedAt`.
3. Carry-over: when the carry source is this same row (re-save at the same level), its words and mapping must be read
   **before** step 4. `RevisionService.SaveTextAsync` computes the carry (`CarryFromVariantAsync` / `CarryFromBaseAsync`)
   in memory before it calls the repository, and the repository only receives the carried words and rows, so the order
   holds by construction; `RevisionAuditDatabaseTests` has a same-level re-save test that fails if it breaks.
4. `DELETE` the verse's words (mapping cascades), `INSERT` new words and carried mapping.
5. Audit `text` with the mapping snapshot.

The unique-violation catch for concurrent first saves stays (two inserts of the same verse → one 409).

The repository assigns the version under the lock (`SaveVersionAsync`; the service no longer computes it) and returns
`(revisionVerseId, version)`; a stale `expectedVersion`, an `expectedVersion` for a verse without a live row, and the
unique-violation of a concurrent first save all return null (409).

## Mapping save (`ReplaceMappingAsync`)

Lock + delete + insert, then audit `mapping` with the same `Version` and the new snapshot. `header.IsActive` checks in
`RevisionMappingService` are gone ("row exists" = header found). New: under the lock every word id (and parent id) of
the request must still belong to the verse; a text save between the service's validation and the lock replaces the
words, so the save answers 409 instead of failing on a foreign key.

## Withdraw (`EndVariantAsync`)

Audit `withdraw` (text of the removed row, no mapping), then `DELETE` the row. Request keeps `revisionVerseId` (stable id).
A second withdraw finds no header: 404 (`NotFound`); a concurrent one that deletes the row between header read and delete: 409.

## Restore (history "wiederherstellen")

Stays a client action: it loads the audited text into the editor and saves as a normal text save. Optional later:
restore the audited mapping snapshot too (not in this roadmap).

## Seed import (`RevisionSeedRepository.WriteVariantAsync`, `EndActiveAsync`)

Same as text save with `ByUserId = NULL`, mapping origin `seed`; `EndActiveAsync` (variant left the seed) becomes
withdraw. Unchanged seed lines (hash equal) write nothing, as today. Export reads the live table without filters.

## Rebase (`RevisionBaseRealigner`)

Updates `BasePositionInVerse`/`Change` in place, no audit row: these are derived from Elb1871, not authored.
