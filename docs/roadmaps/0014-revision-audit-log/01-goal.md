# 0014 · Goal

## Today

Every text save inserts a new `RevisionVerses` row and ends the old one (`IsActive = false`, `EndedAt`), see
`RevisionRepository.InsertVersionAsync`. The expensive part is not the verse row: each version also gets a full copy of its
`RevisionWords` and its carried `RevisionMapping`. History lives in the same three tables that the reader, the mapping
editor, the overview, bulk replace and the seed export query.

Numbers (dev DB 2026-11-15): Elb1871 has 31,169 verses and 722,753 words (23 per verse); mapping is about 1.06 rows per
word. One revision that touches every verse once = 31k verse rows, 720k word rows, 760k mapping rows. Bulk replace
(roadmap 0013) creates one version per hit, so a few passes per revision and three revisions give roughly 5 versions per
verse: about 11M word rows and 11.5M mapping rows, of which 80 % are history.

Every hot query has to filter that history out: `IsActive` partial indexes on `RevisionVerses`, but `RevisionWords` and
`RevisionMapping` have no lifecycle column, so `RevisionMapping_Step_idx` ("where is G3056 translated") and all word
queries carry the dead rows and need a join back to `RevisionVerses` to drop them.

Mapping saves on the other hand replace rows in place with no history at all (0011 decision "Mapping history").

## End state

- `RevisionVerses`, `RevisionWords`, `RevisionMapping` hold exactly one row set per (verse, revision): the current one.
  No `IsActive`, no `EndedAt`; unique `(HebRefId, RevisionId)`.
- A new append-only `RevisionVerseAudit` records every text save, mapping save and withdrawal with author, time, the
  text and a compact mapping snapshot, so the version history (0011.8f) and "restore" keep working and mapping saves
  gain a history.
- Reader, editor, overview, bulk replace, seed export/import behave as before.

## Out of scope

- Suggestions / maintainer merge (Linux-style editing model): pending proposals are not audit rows, they get their own
  table later.
- Auditing the legacy mapping tables (`Elb1871WordsSrGntWords` …) and `Revisions` itself.
- Shipping the audit log in the repo seed.
