# Roadmap 0022: Vocab trainer (Greek NT and Hebrew OT by frequency)

Folder roadmap (see the `roadmap-folder` skill): open only the file you need.

| File | Content |
|---|---|
| [01-goal.md](01-goal.md) | What is missing today (with numbers), end state, deferred items |
| [02-decisions.md](02-decisions.md) | Maintainer decisions and code findings |
| [03-vocab-model.md](03-vocab-model.md) | Vocab tables, generator, revision-aware renderings view, meaning derivation |
| [04-study-engine.md](04-study-engine.md) | Progress tables, scheduler, picking, quiz types, judging, distractors, API |
| [05-storage-and-audio.md](05-storage-and-audio.md) | Garage setup, object storage abstraction, audio upload and recording |
| [06-ui.md](06-ui.md) | Public vocab pages, study pages, script keyboard, maintainer editor |
| [07-ui-drafts.md](07-ui-drafts.md) | Copy rules, routes and page files, new component props, typing layout rules |
| [click-dummy.html](click-dummy.html) | UI draft of all seven screens (mock data), outlines components on demand |
| [08-tasks.md](08-tasks.md) | Task table (backend and frontend woven) |
| [09-risks.md](09-risks.md) | Risks worth naming before starting |

## Status (2026-11-21)

Planned, nothing implemented. All decisions are settled. On 2026-11-21 the maintainer changed two of them: typing uses the dictionary form with the separators pre-filled, and lexicon edits flag the learner's cards for keep or reset instead of being refused. UI drafts (click dummy + spec) added the same day.

Implemented on `feature/vocab-learning` (2026-11-21, 0.8.0): migrations 0037–0039, 0022.1, 0022.1f, 0022.1b (extra row: STEP Hebrew lexicon import fix, verb `step-lexicon-repair`), 0022.2. Next: 0022.2f. Deviations so far: `StatsVocabRenderings` has `GermanPos` (STTS) so derivation can drop function words; `VocabCards` has no FK to `VocabEntries` (cards outlive deleted entries); entry renderings capped at 100 rows; Hebrew sample verses use the braced main key only; the public list's "Anzeigen" filter only offers "Alle" so far.
