# Roadmap 0024: Curated TSK cross references

Folder roadmap (see the `roadmap-folder` skill): open only the file you need.

| File | Content |
|---|---|
| [01-goal.md](01-goal.md) | What is missing today (with numbers), end state, deferred items |
| [02-decisions.md](02-decisions.md) | Maintainer decisions from the interview, plan decisions, code findings |
| [03-model.md](03-model.md) | Stable link key, `TskCurations`, proposals, audit, word links and drift |
| [04-ui.md](04-ui.md) | Hover card with original text, reader display, curation, link mode, review page |
| [05-seed-diff.md](05-seed-diff.md) | `assets/seed/tsk` change file, export, import, initial seed |
| [06-tasks.md](06-tasks.md) | Task table (backend and frontend woven) |
| [07-risks.md](07-risks.md) | Risks worth naming before starting |

## Status (2026-11-23)

Planned, nothing implemented. Interview held on 2026-11-23; the decisions are in [02-decisions.md](02-decisions.md). The maintainer accepted the plan decisions on 2026-11-23; 0024.1 can start. Proposed components (`CrossReferenceEditor`, token `--uw-mark-link`) are confirmed in chat with the row that builds them.

- **0024.1 done (2026-11-23, backend only):** version 0.10.0, no migration. `GetVerseCrossReferenceTexts` verses carry optional `original { language, text }` from `StepHebrewWords`/`StepGreekWords.Original` (same source as `GetChapterOriginals`; Elberfelder HebRefId = STEP `LxxRefId`), one query per testament via `IVerseMappingRepository.GetOriginalWordsOfVersesAsync`; `CrossReferenceOriginals` joins words in order and sets `aramaic` when more than half of an OT verse's grammar codes start with `A`. Deviations: `Original` sits on the shared `ChapterVerseResponse` (null and omitted outside this endpoint); no Bruno request existed for the endpoint. Tests: `CrossReferenceOriginalsTests` (Gen 1,1 Hebrew, Joh 1,1 Greek, Dan 2,4 Aramaic, Spr 8,22–24 three verses, unknown verse none).
- **0024.1f done (2026-11-23):** version 0.10.1. `PassagePreviewCard` verses carry optional `original` (no separate prop); with any original the card is `w-80 md:w-160` with a grid (`grid-cols-1 md:grid-cols-2`, one row per verse: German, original; empty cell keeps rows aligned), verse numbers in both columns, Hebrew `lang="he"`/Aramaic `lang="arc"` with `dir="rtl"`, Greek `lang="grc"`, font token `font-original` one size smaller than the Grundtext panel. `CrossReferenceList` clamps the card after the texts load (`cardLeft(linkLeft, wide)`, `PREVIEW_CARD_WIDTH`/`_WIDE`, md via `useMediaQuery`). Checked live on `/bible/1/1` at 2000 px: hover on Sprüche 8,22–24 gives a 640 px card with three German/Hebrew rows. Deviation: no screenshot (pane did not render), the check read the DOM; phone layout covered by tests only. Tests: `PassagePreviewCard.test.tsx`, `CrossReferenceList.test.tsx` (10 tests); full suite in a node container 136/137 files, only the 2 known NoteReviewPage failures.
