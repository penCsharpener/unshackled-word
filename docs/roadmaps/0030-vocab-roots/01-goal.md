# 0030 · Goal

## What is missing

The vocab list (`/vocab`, `/edit/vocab`) shows lemma, meaning, rank and occurrences, but says nothing about how a word is built. In Greek most of the vocabulary hides a few frequent verbs behind prepositions: ἐκπορεύομαι, εἰσπορεύομαι, διαπορεύομαι and προπορεύομαι are all πορεύομαι, and συνεπιμαρτυρέω is σύν + ἐπί + μαρτυρέω. Learners who see the stem learn four words for the price of one. In Hebrew the same holds for the triliteral root (מִכְתָּב, כְּתָב and כָּתַב all come from כתב).

Numbers from the dev DB (2026-11-27):

- 5,407 active Greek and 8,081 Hebrew/Aramaic vocab entries.
- 1,491 of 5,405 Greek entries carry a hyphenated Abbott-Smith headword in `StepStrongsLexicon."Lexicon"` (`<b>ἐκ-πορεύω </b>`). That is the deterministic compound split. The lexicon gives no Strong's number for the parts.
- 1,343 Hebrew entries are verbs (`Pos = 'H:V'`). Each of them is its own root.
- No table holds root, prefix or word family data today.

## End state

- Three new columns in the vocab list: **Präfixe** (`σύν + ἐπί`), **Wurzel** (`μαρτυρέω`, the bare stem alone) and **Wurzel-Strong's** (`G3140`, linked to that entry). Hebrew shows the root letters (`כתב`) and the Strong's number of the verb, when one exists.
- Roots are first-class rows (`VocabRoots`), linked to words through the word's Strong's key, so they survive every vocab rebuild, merge and key move ([03-data-model.md](03-data-model.md)).
- Filled by a deterministic prefill plus an AI run (`vocab-root`). AI values show right away with an "KI" badge, and the maintainer corrects them on the entry page ([04-prefill-and-ai.md](04-prefill-and-ai.md)).
- Filter by root (word family) on both lists, a family block on the detail page, and the root on the back of the trainer card ([05-ui.md](05-ui.md)).
- Part of the vocab seed (`assets/seed/vocab/roots.csv`, `word-roots.csv`).

## Purpose beyond this roadmap

Later, users learn vocab by root: a course of the πορεύομαι family, or "unlock every compound of a stem you know". The data model is built so that this course needs no change of shape, only a new `VocabCourses."Kind"`.

## Out of scope

- The root-based course itself and any scheduler change (a later roadmap).
- Filling parent roots by AI (πορεύομαι → πόρος, μαρτυρία → μαρτυρέω). The `ParentRootId` column, manual editing and the wide family filter are in scope, see [02-decisions.md](02-decisions.md).
- Roots in the reader, the word detail panel or the Strong's page.
- Hebrew prefixes (prepositions and articles are separate morphemes there, H9001–H9049).
