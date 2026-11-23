# 0024 · Decisions

## Maintainer (interview 2026-11-23)

- **One link = one row.** The unit of every curation action is one link the reader sees: source verse → target verse/range, i.e. one de-duplicated `Tsk` row. The KJV word (`Scope`) plays no role in curation or display.
- **Shared layer, Linux-style.** One curated TSK for everyone. Logged-in users suggest, `Admin`/`Maintainer` edits apply directly and maintainers accept or reject suggestions.
- **Actions:** highlight, hide (irrelevant), comment, word link, add a reference TSK does not have.
- **Comment:** short plain text, max 500 characters, German scripture references auto-linked (as in notes).
- **Display:** highlighted refs are marked and sorted first; a reader toggle "Nur hervorgehobene" hides the rest; the comment shows in the hover card.
- **Word links: manual only.** No heuristic or AI pre-linking in this roadmap.
- **Link mode:** refs are dragged onto a word of the source verse; several selected refs can be dragged at once. Hover on a ref still shows the verse.
- **Hover card:** German text plus the original-language text, in a second column, underneath on phones.
- **Seed:** default seed stays `assets/tskxref.txt`. Only changes go to `assets/seed/tsk/` (in the repo, rolling like the mapping seed), exported and imported on demand from the maintainer pages and applied once by the initial seed.

## Plan decisions (accepted by the maintainer 2026-11-23)

- **Key in Elberfelder numbering.** A link is keyed by `(SourceHebRefId, TargetStartHebRefId, TargetEndHebRefId)`, normalised exactly as `CrossReferenceMapper.Build` does today (backward ranges swapped, single-verse ranges collapsed, cross-chapter ranges cut to their first verse). It never uses `Tsk.Id`. See [03-model.md](03-model.md).
- **Precomputed keys on `Tsk`.** A migration adds `Sort`, `SourceHebRefId`, `TargetStartHebRefId`, `TargetEndHebRefId` to `Tsk` and backfills them, so reads stop translating `LxxRefId` per request and curations can join on the key.
- **Word link = Elb1871 word range in the source verse** (`StartPosition..EndPosition`, one word = start = end) plus `QuotedText`, with the same drift rule and revision-level mapping (`BasePositionInVerse`) as notes (0023). A drifted link falls back to verse level.
- **Hidden refs** are not sent to anonymous readers or users; curators see them greyed in curate mode.
- **Suggestions** carry the full desired state of one link; accepting overwrites the current state. The suggester sees their pending suggestion, everyone else the merged state.
- **No personal data in the repo seed:** the change file has no emails or user ids; authorship stays in the local audit table.
- **Original text in the hover card** is plain surface text of the verse (Hebrew/Aramaic OT, Greek NT) from the same source as the "Grundtext" side panel (`GetChapterOriginals`), `dir="rtl"` for Hebrew.
- **Version:** 0.10.0 with 0024.1, then a build number per row.

## Code findings the plan rests on

- `Tsk` (`0001_InitialMigration.sql`): `Id serial`, `LxxRefId`, `Scope`, `RelatedStartLxxRefId`, `RelatedEndLxxRefId`; no sort, no key. `TskStrategy` inserts one row per (entry, target) and drops `sort`.
- `TskRepository.GetOutgoingAsync` maps `LxxRefId` → `HebRefId` with sub-selects per row; `CrossReferenceMapper` normalises; `GetChapterCrossReferences` de-duplicates "first wins". The same list feeds the word box (`WordDetailService`, max 20) and `Tooling.McpServer/Tools/CrossReferenceTools`.
- File: 63,682 entries, 29,055 verses, max 12 entries per verse, median 2; `(book, chapter, verse, sort)` is unique.
- `Elb1871Words.Id` is not stable (`RebaseRepository` deletes all rows): anchors must use `HebRefId` + `PositionInVerse`, as notes do.
- `PassagePreviewCard` is presentational and fixed at `w-80`; texts come from `GetVerseCrossReferenceTexts` (one request per source verse).
- Seed jobs run on the shared `SeedJobRunner`; `SeedImportsPage` (`/edit/imports`) already hosts vocab and notes imports; `assets/seed/manifest.json` lists seed parts with sha256.
