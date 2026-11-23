# 0024 · Goal

## What is wrong today

`assets/tskxref.txt` (Treasury of Scripture Knowledge) has 63,682 entries on 29,055 verses, and the entries list about 306,000 target references (book groups split by `;`, before comma lists are expanded). The reader shows all of them per verse in TSK order, de-duplicated (`CrossReferenceMapper`, `GetChapterCrossReferences`). Many are weak; nobody can mark the useful ones, hide the noise or say *why* a reference matters.

Each TSK entry belongs to a KJV word (`words` column, stored as `Tsk.Scope`: "God", "beginning", "I will"). That word means nothing to a German reader of Elb1871, so the word box today shows the whole verse's list for every word (`WordDetailService`: "TSK is verse level"). The link from a reference to the word it explains is lost.

The hover card on a reference (`PassagePreviewCard`) shows only the German text. The app's aim is easy access to the original languages, so the card should show Hebrew/Aramaic or Greek too.

The `Tsk` table has no stable row identity: `Id` is a `serial` from insert order, `sort` from the file is dropped, refs are in `LxxRefId` numbering and translated on every read. Nothing can be keyed to a row across a re-seed yet.

## End state

- Every cross-reference link a reader sees (one source verse → one target verse or range) can be **highlighted**, **hidden** or left neutral, carry a **short comment** (plain text, ≤ 500 characters) and be **linked to a word range** of the Elb1871 source verse. Users can also **add** references TSK lacks.
- Logged-in users suggest; maintainer edits apply directly and maintainers merge suggestions (`/edit/tsk`).
- Readers see highlighted refs first with a marker, can filter to highlighted only, never see hidden ones, read the comment in the hover card, and see refs at the word they belong to.
- A link mode lets curators drag one or several selected refs onto a word (tap-then-tap on touch). The hover card keeps working in that mode.
- The hover card shows the German text and the original-language text side by side (underneath on phones).
- All curation is exported to and imported from `assets/seed/tsk/` as a change file on top of the unchanged `tskxref.txt`; the initial seed applies it after the TSK import.

## Out of scope (deferred)

- Automatic word linking (Strong's/gloss heuristic, AI agent). Manual only for now, decided 2026-11-23. Signal to revisit: curators complain that linking common words ("God", "Lord") is tedious.
- Personal per-user TSK views; the curated layer is shared.
- Curation of other cross-reference sources (only TSK exists today).
- Reviewer hierarchy beyond `Admin`/`Maintainer`.
