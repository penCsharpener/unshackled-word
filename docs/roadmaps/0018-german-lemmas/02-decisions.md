# 0018 · Decisions

## Given by the maintainer (2026-11-17)

- For a single original word there is one German lemma. `er wurde getötet` must not count `töten` three times, nor `werden` as a rendering.
- Target: a clean list of the most frequent Greek and Hebrew words with their German renderings and correct counts (`λέγω => sprechen (43x), reden (20x), sagen (12x)`); the mapping supplies example sentences.
- Use Gemma 4 and the 0016 agent tools for the lemmatisation.

## Decided in the interview (2026-11-17)

- **Role per link.** Every German word keeps its true lemma (`wurde` = `werden`). Each real link gets a role: `head` (counted) or `unit` (belongs to the head's German form, not counted). Hebrew links also store the morpheme Strong's they render (`und` → H9001). Stats count only head links ([04-roles-and-counting.md](04-roles-and-counting.md)).
- **Separable verbs: the particle keeps its own lemma.** `gingen … aus` → `ausgehen` on `gingen`, `aus` on `aus` (STTS `PTKVZ`, role `unit`). **Supersedes** the 0016 decision "both tokens get the full lemma"; the 0016 prompts and validator change with 0018.6.
- **Modern headword.** `thun` → `tun`, `Thür` → `Tür`, `ward` → `werden`, `gieng` → `gehen`. The 1871 form stays in the word text. Settles the open question of 0016 `05-lemmatisation.md`.
- **Rollout: reference set, then bulk.** Gemma 4 is measured on the hand-checked reference verses (0016.9) first. If it meets the bar below, runs go book by book and unflagged proposals are bulk-merged; flagged ones go to review.

## Accepted by the maintainer (2026-11-17)

1. **Two roles only.** No `grammar` role: every Hebrew suffix and prefix has its own Strong's in STEP (H9023 "his", H9002 "and", H9009 "the" …), so a German word rendering it is the head of that morpheme. Subject pronouns from the verb ending become added words with the verb as parent (the Gen 1:3 convention), not links.
2. **Accuracy bar for bulk merge:** on the reference set without the 10 prompt examples, ≥ 98 % of lemmas and ≥ 97 % of roles/morphemes equal the maintainer's answer, and no hard-validator failure in the run.
3. **Deterministic backfill writes directly, as an undoable change set.** Roles and morphemes that the rules in [05-data-model.md](05-data-model.md) decide unambiguously (~95 % of links, estimate) are written by a maintainer-started job with a dry-run report first, not as 400k proposals. Lemma changes keep going through `Elb1871MorphologyChangeSets`.
4. **Compound parts get a part lemma** (`Gersten` → `Gerste`) so a part that is the head of its source word counts under a real headword.
5. **Contractions keep one lemma:** `zum` = `zu` (head of the preposition); its link to a Greek article is `unit`, the one allowed unit without a head ([04-roles-and-counting.md](04-roles-and-counting.md)).

## Findings the plan rests on

- `Elb1871GreekMapping` already has `StrongsNumber` per link; `Elb1871HebrewMapping` has none, and STEP Hebrew Strong's are per word (`H9001/{H0559}`, main morpheme in braces).
- `DqWordFacts` / `StatsStrongsRenderings` (0013, 0014) group by Strong's and German lemma and count `DISTINCT ElbWordId` — the place where the double count happens.
- Added words already exclude themselves from stats (`Kind = 'added'`, 0014).
- `AgentRuns.Mode` exists (`redo`, `fill-open`); a `lemma` mode fits next to them. `LemmaLexicon` and `AgentLemmaRules` hold the lemma checks of 0016.
- H9014 (maqqef), H9015–H9018 (punctuation) are tags, never link targets.
