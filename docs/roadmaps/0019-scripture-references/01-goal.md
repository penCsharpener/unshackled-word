# 0019 · Goal

## What is missing today

- **Search cannot jump.** `SearchOverlay` only runs the verse full-text search and the originals lookup. Typing `Offb 2,1` finds nothing useful; there is no way to go to a passage except the chapter picker.
- **The only parser handles one range.** `Domain/Models/BibleStructure/BibleReferenceParser` (one regex, 18 test cases in `BibleReferenceParserTests`) parses `Joh 3,16-18` or `1Mo 1,1-2,3`, but no lists (`1-5.7.9-10`), no `;`, no several books. It is used by five MCP tools (`PassageTools`, `OriginalTextTools`, `CrossReferenceTools`, `SblTextTools`, `AgentMappingTools`).
- **Two book tables, no shared lookup.** `GermanBookNames` (German names + abbreviations, umlaut-folding `Normalize`) and `BibleBook.AllBooks` (English names + abbreviations, exact case-insensitive match) are queried one after the other; there is no prefix matching and no STEP/OSIS code table.
- **Lexicon references are thrown away.** In the dev DB (`uw_0018_test`) 5,720 of 11,035 Greek `StepStrongsLexicon` entries carry **41,473** tags like `<ref='Mat.5.29'>Mat.5:29</ref>` (G1188 δεξιός alone has dozens). `LexiconTextCleaner.Clean` strips all tags, so the popup shows `Mat.5:29, Jhn.18:10` as dead text. Hebrew and name entries (lang 0/1) have about 4,000 untagged references in plain text, e.g. `first mentioned at 1Ch.8.39`.
- **About 2,400 lexicon references point outside the 66 books** (Sir 419, 2Ma 384, Wis 382, 3Ma 201, 1Ma 175, Tob 93, Jdt 88, …).
- **STEP counts verses the English way.** `BibleVerseCountingMapping` (31,174 rows, `LxxRefId` = English counting, `HebRefId` = reader counting) differs in 2,026 rows: Psalms 1,063 (titles: English Ps 3:1 = Hebrew Ps 3:2), 1Chr 118, Mal 3:19–24 = English Mal 4:1–6, and so on. A lexicon link `Mal.4.5` must open Mal 3,23.

## End state

- Typing a reference in German citation style in the search (`Offb 2,1`, `1. Kor 13,4-7`, `Offb 2,1-5.7.9-10`, `Röm 8,28; 12,1-2`, `Joh 3,16; Röm 8,28`) shows a "Bibelstelle" card with the verse text on top; Enter opens the passage.
- The reader opens `/bible/66/2?v=1-5,7,9-10#v1`, scrolls to verse 1 and marks every selected verse.
- Every reference in the English lexicon text (word popup, header hover card) is a link to the reader in Hebrew counting; hovering or focusing it shows the verse text.
- One C# parser with a large table-driven test suite covers German citation style (search) and STEP/OSIS notation (lexicon); the MCP tools keep working through the old `BibleReferenceParser.Parse` signature.

## Out of scope

- Scanning German prose (Elberfelder notes, revision texts) for references: `am 3.`, `Hos`, `Mal` are ordinary German words; needs its own false-positive strategy.
- A TypeScript parser (decision, see [02-decisions.md](02-decisions.md)).
- English book names, English list style (`Rev 2:1-5, 7`) and English counting in the search box; ranges across books (`Mt 28 – Mk 1`); texts of Apocrypha/LXX-only books.
- New MCP tool features (lists in `get_passage`); they may adopt the new parser later.
