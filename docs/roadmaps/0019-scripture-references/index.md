# Roadmap 0019: Recognise scripture references (search and lexicon text)

Folder roadmap: open only the file you need.

| File | Content |
|---|---|
| [01-goal.md](01-goal.md) | What is missing today (code and dev DB numbers), end state, out of scope |
| [02-decisions.md](02-decisions.md) | Maintainer decisions (interview 2026-11-18) and findings the plan rests on |
| [03-book-aliases.md](03-book-aliases.md) | Book alias catalogue: profiles, normalisation, collisions, prefix matching |
| [04-parser-algorithm.md](04-parser-algorithm.md) | Tokenizer regex, grammar, interpretation rules for lists and ranges, result model |
| [05-lexicon-and-counting.md](05-lexicon-and-counting.md) | `<ref>` tags and plain-text scanner in STEP lexicon text, segments, English → Hebrew verse counting |
| [06-api-and-ui.md](06-api-and-ui.md) | Endpoints, deep-link format, search overlay card, reader highlighting, hover preview |
| [07-test-catalogue.md](07-test-catalogue.md) | Input → expected tables the unit tests must cover |
| [08-tasks.md](08-tasks.md) | Task table |
| [09-risks.md](09-risks.md) | Risks |

## Status (2026-11-18)

Implemented 2026-11-18; all tasks 0019.1 to 0019.11 done. User input uses German citation style (`.` verses, `;` chapters); English/OSIS parsing only for the Strong's lexicon.

Not verified:

- Lexicon coverage run (>= 99.5 %) needs `UNSHACKLEDWORD_TEST_POSTGRES`; not run.
- The 2,026-row repository test needs `UNSHACKLEDWORD_TEST_POSTGRES`; not run.
- No browser check of the search overlay or hover preview.

Known issue (not fixed here): `BibleBook.VersesPerChapterHeb` and `VersesPerChapterLxx` are mislabelled. Heb holds English counts and Lxx holds Hebrew counts. The reference code works around this via `References/VerseTables`.
