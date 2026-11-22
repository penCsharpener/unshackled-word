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

## Status (2026-11-22)

All decisions are settled. On 2026-11-21 the maintainer changed two of them: typing uses the dictionary form with the separators pre-filled, and lexicon edits flag the learner's cards for keep or reset instead of being refused. UI drafts (click dummy + spec) added the same day.

Implemented on `feature/vocab-learning` (2026-11-21/22, 0.8.0): all rows 0022.1–0022.15 (migrations 0037–0040, 0022.1b as an extra row: STEP Hebrew lexicon import fix, verb `step-lexicon-repair`; 0022.12 backend came with 0022.8b; 0022.14 seed; 0022.15 docs gate). The Opus review is done (2026-11-22): fixes below, nothing committed by the review.

Deviations so far:
- `StatsVocabRenderings` has `GermanPos` (STTS) so derivation can filter by word class; `VocabCards` has no FK to `VocabEntries` (cards outlive deleted entries); entry renderings capped at 100 rows; Hebrew sample verses use the braced main key only; the public list's "Anzeigen" filter only offers "Alle".
- Derivation: content words drop function-word STTS tags, function-word entries keep only their own class; fallback to the top key when nothing passes the thresholds; first meanings of an entry bump no `ContentVersion`. ἀγαπάω keeps "Liebet"/"Liebe" because the STTS data tags them as nouns.
- Editor: unaccepting a derived meaning also pins it (otherwise re-derivation re-accepts it); no routes yet for lemma edit or un-retire; a split's new entry has no meanings until the next derivation. Editor list has no default rank range.
- Derive report's changed-top list is preformatted strings (`{id} {lemma}: {old} -> {new}`) that the UI parses: the list lives in the shared `SeedStepReport.Lists` (strings only), a structured list would change the shared job contract.
- Scheduler uses `PenCsharpener.FsrsSharp` 6.0.0's legacy `Fsrs` API (fits the `VocabCards` columns); strength is a double interpolated on Lexio's ladder; 30 days of reviews only when due reach strength 4.65, daily reviews reach 5.
- Session: every answer is rated (as Lexio); introductions are logged with `Rating` null; due reviews are asked once each before the first introduction (0022.8f wording, Lexio does it the other way round); "nothing to study" returns 200 `finished`.
- Audio: MP3 only (magic bytes, 1 MB); presigned URLs expire after 1 h; recording encodes in the browser with lamejs (30 s cap, no worker).
- Typing cannot be turned off (0039 check constraint); listening and verse context fall back to the next type when no audio/verse exists.
- Lexicon changes: cards still in state `new` are not listed as changed; the `form` change row uses `{form}` while the generator writes `{dictionaryForm}`.
- Agent form runs reuse `AgentRuns` (migration 0040: mode `vocab-form`; `BibleBookId` = course id, From/ToHebRefId = rank range); counts only in `StatusNote`; accept-form is its own route.
- Script keyboard follows Lexio's QWERTY-position rows (not the draft's alphabetical rows), no Shift.
- Seed identity is `(Track, Lemma, Homograph)`, not `(Track, Lemma)`: a split keeps the lemma (H1254B gets its own בָּרָא), so all three seed files carry a `Homograph` column (1 = first live entry with that lemma, numbered by lowest Strong's key, then id; optional when reading). Before the review the exporter refused such a database.
- The vocab seed is not in `assets/seed/manifest.json` (like the revision seed): the importer is an idempotent sync and needs no seed version.
- The first question of a due review word follows the age ladder (any enabled type), not always multiple choice.
- Merge and delete leave the source entry's audio object in the bucket (orphaned, harmless); a merge does not carry the source's audio over to the target.
- Learner counts (`…/learners`, `Learners` in edit responses) and the change list ignore cards still in state `new`.

Review fixes (2026-11-22): all vocab DB test classes share `[Collection("VocabDatabase")]` (the generator and renderings classes ran in parallel with the others and deadlocked, which looked like a hang); a pending card whose entry was merged away, deleted or retired no longer wedges the session (`next` drops it, `answers` answers 409); a concurrent session start resumes the session the other request opened instead of failing on the open-session index; seed homograph identity with a split round-trip test and a test that the committed seed re-serializes byte for byte.
