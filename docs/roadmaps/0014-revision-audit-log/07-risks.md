# 0014 · Risks

- **Migration 0024 deletes history rows for good.** Words and mapping of old versions exist afterwards only as text +
  snapshot in the audit. The script must refuse to run when the backfill checks fail, and the release note must say
  "back up first"; self-hosters cannot go back to 0023 without restoring a dump.
- **Half-done state between 0014.1 and 0014.3.** While dual-writing, a write path that forgets the audit row produces
  a history gap that 0024 then makes permanent. Mitigation: the "newest audit row equals live state" test covers every
  writer before 0014.3 starts, and 0014.3 ships in one commit with 0024.
- **Carry-over reads its own row.** A same-level re-save now overwrites the words it carries from. If the old words are
  not read before the delete, the mapping silently starts from Elb1871 ([04-write-paths.md](04-write-paths.md) step 3).
- **`RevisionWords.Id` changes on every text save.** Anything holding a word id across a save (open mapping editor,
  bulk-replace step 3, word detail popup) gets a 409/404 instead of today's "ended version" message. Callers must
  reload by verse and position.
- **Audit size.** ~0.5 kB per row before TOAST; 500k audit rows stay well under 1 GB. Acceptable at the expected scale,
  but the snapshot format is the only thing worth keeping compact from day one.
- **Audit is not the suggestion queue.** Reusing it for "user suggests, maintainer merges" later would mix accepted and
  pending text; suggestions need their own table.
