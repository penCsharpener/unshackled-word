# 0030 · Decisions

## Maintainer (interview 2026-11-27)

- **Depth: one level, prepositions stripped.** συνεπιμαρτυρέω → prefixes σύν, ἐπί; root μαρτυρέω. No deeper etymology.
- **Columns:** the root column holds the bare stem alone (`μαρτυρέω`). The prepositions go in their own column. The root's Strong's number gets a third column.
- **Languages: Greek and Hebrew/Aramaic.** Hebrew root = triliteral root letters, plus the Strong's number of the verb of that root when the corpus has one (H3789 for כתב). The Strong's column is empty otherwise.
- **Source:** a deterministic prefill first, then an AI run for the rest. The AI also checks the prefill.
- **AI values are visible immediately**, marked as AI. There is no proposal state. The maintainer corrects them afterwards, and a manual value is never overwritten.
- **Placement:** the public list, the editor list with editing, a filter by root (word family), the vocab detail page and the trainer card.
- **Seed:** roots are part of the vocab seed. Like the rest of the vocab seed, it never runs at startup and is imported only via "Importe".
- **Purpose:** users will later learn vocab by root. Roots therefore need stable ids and a family relation, not just display text.
- **Simple words show their own root, dimmed** (confirmed 2026-11-27).
- **Deeper families through a parent root** (2026-11-27). `VocabRoots."ParentRootId"` (μαρτυρία → μαρτυρέω, πορεύομαι → πόρος). The word's root column stays one level; the parent only widens the family. This roadmap ships the column, manual editing and a family filter that includes sub-roots. Filling parents by AI is deferred.

## Plan decisions

- **Link through the word's Strong's key, not through `VocabEntries."Id"`.** `VocabStrongs` already moves keys between entries on merge and split, and the generator keeps entries by lemma. A root row keyed by `(Lang, StrongsKey)` follows the word through every rebuild with no extra step. The entry's root comes from its main key (the most occurrences in `VocabStrongs`). Disagreeing keys are a data-quality finding, not a silent pick.
- **Simple words get a row too** (root = the word itself, no prefixes). Otherwise "no root known yet" and "no compound" would look the same, and a family list would miss its own head word. The list shows the root of a simple word dimmed.
- **Root ids are stable.** Greek identity is the root's Strong's key. Hebrew identity is `(Track, RootNormalized, Homograph)`, because homograph roots exist (ענה I/II) and many roots have no verb.
- **Roots do not bump `ContentVersion`.** A root is extra information and does not change what counts as a correct answer, so it must not set off the "changed words" notice for learners.
- **Origins:** `prefill` | `agent` | `manual`. Only `agent` shows the KI badge. `prefill` comes from Abbott-Smith's split or from the verb rule, so it is source data.
- **Writes are Admin/Maintainer only**, as for all vocab content (CLAUDE.md, Access).
- **Version:** 0030.1 bumps to 0.15.0. Each later row bumps the build number.

## Code findings the plan rests on

- `VocabLexiconGeneratorSql` is incremental: entries are matched by `(Track, Lemma)`, keys are upserted into `VocabStrongs` (`ON CONFLICT DO NOTHING`), and retired entries keep their id.
- `assets/seed/vocab/*.csv` identifies entries by `(Track, Lemma, Homograph)` and keys by `StrongsKey`, so seed files keyed by `StrongsKey` import on any install.
- The `vocab-form` agent mode (migration 0040, `VocabFormRunService`, `VocabFormRunExecutor`, prompts `vocab-form-*-v1.md`) runs over a course and a frequency-rank range. `vocab-root` copies that shape.
- STEP's Greek `StepStrongsLexicon` keys are `G` + four digits + optional extra (`G0001G`), the same form as `VocabStrongs."StrongsKey"`.
