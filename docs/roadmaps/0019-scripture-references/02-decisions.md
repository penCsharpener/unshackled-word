# 0019 · Decisions

## Given by the maintainer (2026-11-18)

- The smart search recognises a typed reference and jumps there.
- References in the English Strong's lexicon text are recognised, deep-linked and previewed in a hover popup.
- **German citation style for everything a user types** (changed 2026-11-18, replaces the first reading of `Offb 2,1-5;7; 9-10` as "verses 7 and 9–10"): `,` separates chapter and verse, `.` separates verses of the same chapter, `;` starts a new chapter or a new book, `-` is a range, `f`/`ff` follow. So `Offb 2,1-5.7.9-10` = verses 1–5, 7, 9–10 of chapter 2, while `Offb 2,1-5; 7; 9-10` = Offb 2,1–5, chapter 7 and chapters 9–10. `:` is tolerated as chapter-verse separator (`Joh 3:16`), but list rules stay German. Rules in [04-parser-algorithm.md](04-parser-algorithm.md).
- **English parsing is only for the Strong's lexicon notes** (STEP/OSIS notation like `Mat.5:29`, `Mat.25.33, 34`). The search box and all displayed references are German.
- Regex is fine. Plenty of unit tests for combinations and abbreviations.

## Decided in the interview (2026-11-18)

- **C# only.** One parser and one test suite in `UnshackledWord.Domain`. The search overlay calls a parse endpoint (debounced like the verse search); lexicon text reaches the frontend already split into text and reference segments. No TypeScript port.
- **Search card + Enter.** When the input is a reference with a chapter number, the overlay shows a "Bibelstelle" card with the verse text on top and skips the full-text search; Enter (no hit selected) opens the first reference. A book name alone (`Hiob`, `Amos`, `Ruth`) still runs the full-text search, because those are also names people search for.
- **Highlight all verses.** Deep link `/bible/{bookId}/{chapter}?v=1-5,7,9-10#v1`; the reader scrolls to the first verse and marks every listed verse.
- **English counting via `BibleVerseCountingMapping`.** English/STEP references are parsed as English-counting ref ids (`LxxRefId`) and mapped to `HebRefId`. Practical choice: load only the 2,026 differing rows once into an in-memory map in the backend (the parser is backend-only, so the frontend never needs the table). Start of a span → `MIN(HebRefId)`, end → `MAX(HebRefId)`, unmapped ids stay as they are — the rule `TskRepository.GetOutgoingAsync` already uses.

## Derived decisions (confirm while reviewing)

- **Search input is in reader (Hebrew/Elberfelder) counting.** Only STEP lexicon references are mapped from English counting.
- **Search knows German book names only** (profile `German`). English names are kept solely in the wrapper behind the old `BibleReferenceParser.Parse`, so MCP calls like `John 3:16` keep working.
- **Displayed references are always German** (`Offb 2,1–5.7.9–10`), also in the English UI; lexicon links keep the STEP text as label.
- **Single-chapter books take a bare number as verse:** `Jud 3` = Jude 3, `Phlm 10`, `Ob 4`, `2Joh 5`, `3Joh 2`. The existing test `Jud 3 → 65003000–65003999` changes to `65001003`.
- **Non-canonical books are recognised but not linked.** `Sir.3.1` stays plain text (no link, no preview), so it is not mis-parsed as another book either.
- **The old API stays.** `BibleReferenceParser.Parse` becomes a wrapper over the new parser and returns its old error when the input has more than one span; the five MCP tools are untouched.
- **No STEP text in the repo.** Test fixtures contain reference tags and invented sentences only, never lexicon prose (licence rule in `CLAUDE.md`).

## Findings the plan rests on

- `PassageRange` / ref ids: `book * 1_000_000 + chapter * 1_000 + verse`; whole chapter = verse `0`–`999`. Kept for spans.
- `BibleBook.VersesPerChapterHeb` gives chapter and verse counts for validation (Hebrew counting). Note `GetVerseCountInChapter` has an off-by-one (`Length > chapter`) and reads `Heb` in the `Lxx` branch; do not use it, read the array directly.
- `CrossReferenceList` already has a hover preview card (`PreviewCard`, `role="tooltip"`, 320 px, keyboard support) fed by `crossReferenceTexts`; 0019 extracts it rather than building a second card.
- `HeaderWordInfo.LinkedStrongs` links Strong's numbers in lexicon text with a frontend regex; it keeps doing that inside text segments.
- Chapter page anchors are `id="v{verse}"` and `ChapterPage` already scrolls to `location.hash`.
