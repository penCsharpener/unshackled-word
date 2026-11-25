# 0025 · Decisions

## Maintainer (interview 2026-11-25)

- **Normalized n:m storage.** A catalog table of editions and a join table word ↔ edition, instead of parsing the string at read time or a bitmask column. The raw `StepGreekWords.Editions` string stays as STEP delivered it.
- **Word detail shows editions only when they differ.** A word in all eight main editions shows no edition section. Otherwise: editions that have it, editions that lack it, order shifts.
- **In scope besides the word box:** marks in the Grundtext panel and an edition filter in the reader.
- **Deferred:** MCP/agent exposure (see [01-goal.md](01-goal.md)).

## Plan decisions (accepted by the maintainer 2026-11-25)

- **One parser, in SQL.** Migration `0047_GreekEditions.sql` creates the tables, fills the catalog and a function `"unshackled-word"."RefreshStepGreekWordEditions"()` that rebuilds the join table from the strings. The migration calls it once (existing databases); SeedDb calls it after the Greek STEP import (fresh databases, where migrations run before the seed and find no words). No second parser in C#, so the two can never disagree. Precedent: SQL functions in `0032_LinkRoleStats.sql`, `0038_VocabRenderings.sql`.
- **Derived data, not seed.** The join table is rebuilt from STEP on every import; it is not exported to `assets/seed`, not edited by users, and has no audit.
- **"Main" editions are a catalog flag**, not a hard-coded list: `IsMain` for the eight printed editions. "In all editions" means "in every main edition". Rare tokens (KJV, NIV, manuscripts, versions) are catalog rows with `IsMain = false` and are shown in the word box but never count for "all" or for the filter.
- **Unknown tokens are kept, not dropped.** A token the catalog does not know gets a catalog row created by the refresh function (`Kind = 'unknown'`, `IsMain = false`) and shows up in the data-quality page, so a new STEP release cannot silently lose data.
- **Edition filter** is a reader setting stored in `localStorage` like the other reader toggles, default "Alle". It only affects the Greek Grundtext panel; the German text and its mappings never change.
- **Labels say "Ausgabe"/"edition"**, never "Handschrift"/"manuscript", except for catalog rows of `Kind = 'manuscript'`.
- **Version:** 0.11.0 with 0025.1, then a build number per row.

## Code findings the plan rests on

- `StepGreekFileStrategy` reads column 5 into `Editions`; a commented-out `EditionList` split on `+` shows the idea was there. `StepGreekWordsRepository` bulk-inserts via `unnest`.
- `WordDetailRepository.GetGreekReadingsAsync` joins `Elb1871GreekMapping` → `StepGreekWords` → `StepGreekMorphology`; one Elberfelder word yields 0–3 readings, so per-reading edition lookups are cheap.
- `OriginalReading` (`src/UnshackledWord.Web/src/lib/bible/types.ts`) is the per-reading DTO in the word box; the existing hint `textVariants` (from `SblApparatus`) is verse-level and stays.
- `GetChapterOriginals` (`ChapterOriginalsService`, contract `VerseMappingOriginalWord`) feeds the Grundtext panel (`OriginalVerse.tsx`) with all words of a chapter.
