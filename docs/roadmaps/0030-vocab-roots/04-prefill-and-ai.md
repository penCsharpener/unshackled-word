# 0030 · Prefill and AI run

## Deterministic prefill (`VocabRootPrefill`, Application)

Runs on demand from the vocab jobs section on Wartung and after SeedDb's vocab generation. It only writes keys that have no row yet, or rows with `Origin = 'prefill'`.

**Greek compounds (≈1,491 entries).** Take the Abbott-Smith headword (`^<b>\s*([^<,]*)`) and split it at `-`: `συν-επι-μαρτυρέω` → `συν`, `επι`, `μαρτυρέω`. Each leading part is matched against a closed table of the 18 proper prepositions in their elided and assimilated forms (`ἐκ/ἐξ`, `συν/συμ/συλ/συγ`, `ἀπ/ἀφ`, `κατ/καθ`, `ἐν/ἐμ/ἐγ` …) to give the preposition's Strong's key and its dictionary form (`σύν`). The last part is looked up as a lemma in `StepStrongsLexicon` (Greek, `OriginalWordNoDiacritics`). The middle voice is normalised (`πορεύω` ↔ `πορεύομαι`) and the vocab lemma wins, so the root matches the root's own vocab entry. The prefill writes the row only when **every** part resolves to exactly one key. Anything else goes to the AI with the split as a hint.

**Greek simple words.** These are not prefilled. "No hyphen" does not prove "no compound" (Abbott-Smith is not consistent), so the AI decides.

**Hebrew/Aramaic verbs (≈1,343 entries).** An entry with `Pos` `H:V`/`A:V` is its own root: root = the lemma's consonants (niqqud and cantillation stripped, final forms kept), root key = the entry's main key. Two verbs with the same letters become `Homograph` 1 and 2 in key order. The maintainer merges real duplicates later.

**Morphemes and proper names** (`IsMorpheme`, `IsProperName`) are skipped. They get no root and count as "nicht zutreffend", not as "ohne Wurzel".

## AI run, mode `vocab-root`

Shape copied from `vocab-form`: `VocabRootRunService` (estimate, create), `VocabRootRunExecutor`, endpoints under `api/maintainer/vocab/root-runs`, a course plus a frequency-rank range, the usual token/cost limits and profiles. Prompts: `assets/agent-prompts/vocab-root-greek-v1.md` and `vocab-root-hebrew-v1.md`.

Package per entry: lemma, main key, POS, gloss, the first 400 characters of the lexicon text (with its `from G…`/headword split), and the prefill row if there is one. The answer per entry is `{ key, prefixes: [key…], rootKey | rootLetters, confidence, note }`.

The validator (`VocabRootAnswerValidator`) accepts only:

- Greek: every prefix key is in the preposition table; `rootKey` exists in `StepStrongsLexicon`; the root is not the word itself while prefixes are listed; prefix + root forms plausibly concatenate (normalised, elision allowed), otherwise the entry is flagged instead of written.
- Hebrew: `rootLetters` is 2–4 consonants; an optional `rootKey` must be a verb key whose consonants equal the letters.
- An answer for a key that is `manual` is dropped. A `prefill` row is replaced only when the run option "Vorbefüllung prüfen" is set, and the replacement is listed in the run report.

Accepted answers are written right away with `Origin = 'agent'` and `AgentRunId`. Roots that are not there yet are created in `VocabRoots`. The run report lists rejected and flagged entries, linked to the editor.
