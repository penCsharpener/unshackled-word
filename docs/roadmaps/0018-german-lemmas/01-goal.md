# 0018 · Goal

## What is wrong today

Every German word has one lemma (`Elb1871Morphology`), but the renderings of a source word are counted per linked German word, not per source word. Numbers from the dev DB (`uw_0016_test`, stats of 2026-11-13):

- **Auxiliaries count as renderings.** G0615 ἀποκτείνω (75 aligned source words): `töten` 72, `werden` 8, `haben` 4, `sein` 1, `sollen` 1. `er wurde getötet` produces `werden` *and* `töten` for one Greek word.
- **Hebrew prefixes count as the main word.** H0559 אָמַר (5,283 source words): `sprechen` 3,944, **`und` 3,005**, `sagen` 703, `er` 526, `sie` 265. `und` renders the prefix H9001 of וַיֹּאמֶר, not "to say"; the link only knows the whole word.
- **Subject pronouns are linked instead of added.** `er` 526× under H0559; Gen 1:3 `Es werde` already does it right (`Es` is an added word on `werde`).
- **Separable verbs double-count.** 0016 decided `gingen … aus` → `ausgehen` on both tokens; 27,354 tokens are tagged `PTKVZ` today (many wrongly, `zu` alone 10,922).
- **Same lemma twice on one source word:** 391 Greek and 3,609 Hebrew source words have two linked German words with the same lemma (406 / 3,917 extra counts).
- **Wrong lemmas leak into counts:** G3004 shows `sageen` (23), `Rede` as VERB (15).

Scale: 722,753 German words, 128,262 linked Greek and 291,910 linked Hebrew source words; 7,806 Greek and 147,250 Hebrew source words have more than one linked German word.

## End state

1. Every German word has exactly one correct lemma (rules in [03-lemma-rules.md](03-lemma-rules.md)); a German lemma is never counted twice for one source morpheme.
2. Every real link says whether it is the **head** (carries the meaning, counted) or part of the head's **unit** (auxiliary, particle, reflexive, `zu`). Hebrew links name the morpheme they render ([04-roles-and-counting.md](04-roles-and-counting.md)).
3. A per-source rendering count is correct by construction: λέγω → `sagen (639), sprechen (571), nennen (35) …`, every source word counted once.
4. Gemma 4 (and every other 0016 profile) does the remainder the algorithm cannot decide, through the existing run / proposal / review pipeline ([06-ai-runs.md](06-ai-runs.md)).

## Out of scope

- The vocab trainer and the vocab list UI itself (only the counts it will read).
- Lemmas of revision variants (`RevisionWords` has no morphology yet); the role column is added to `RevisionMapping` so it does not drift.
- Changing which source words a German word links to (that is mapping, 0016); this roadmap only touches role, morpheme, lemma and the added-word flag of subject pronouns.
