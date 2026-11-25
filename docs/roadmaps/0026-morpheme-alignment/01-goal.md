# 0026 · Goal

## The question

STEP's TAHOT data splits every Hebrew/Aramaic word into morphemes: prefixes, the root word and suffixes. Can this make the AI mapping better, and can a finer mapping sit beside the current one, so that וְהָֽעֲמָלֵקִ֥י (Num 14:25) maps its three parts separately to *und*, *die*, *Amalekiter*, without touching the existing mapping?

## What exists today

Much of this is already in the repo. The plan builds on it instead of starting again.

**STEP data, imported and aligned.** `StepHebrewWords` keeps every per-morpheme field with `/` separators:

| Column | Num 14:25 #1 |
|---|---|
| `HebrewNormalised` | `וְ/הָֽ/עֲמָלֵקִ֥י` |
| `Transliteration` | `ve./ha./'a.mal.Ki` |
| `Gloss` | `and/ the/ Amalekite[s]` |
| `Grammar` | `HC/Td/Ngmsa` |
| `ExpandedStrongTags` | `H9002=ו=and/H9009=ה=the/{H6003=עֲמָלֵקִי=Amalekite…}` |

On the dev DB the `/` split gives the same piece count in `HebrewNormalised`, `Grammar` and `ExpandedStrongTags` for all 305,634 words (0 mismatches). 68,091 words also carry `\` pieces (punctuation such as sof pasuq). The SeedDb already builds a per-word morpheme table from this: `StepHebrewWordsNormalizedToHebrewWords` (540,463 rows, with `PositionInWord`) over `StepHebrewWordsNormalized` (38,521 distinct morphemes, `IsRoot`, `Grammar`, `StrongsNumber`).

**Morpheme-level links in the main mapping (roadmap 0018).** `Elb1871HebrewMapping` and `RevisionMapping` have a `StrongsNumber` column: the morpheme a German word renders (`H9001` for *und* of וַיֹּאמֶר), NULL = main morpheme. The mapping editor has a morpheme `Select` per link, the deterministic job "Wartung > Link-Rollen" (rule 1, `LinkRoleRules`/`HebrewMorphemeHints`) fills the column, and the AI prompts `hebrew-v2`/`lemma-hebrew-v1` already send each source word's morpheme list (`["H9001","{H0559}"]`) and ask for a morpheme per link.

**But the column is empty.** On the dev DB, 0 of 497,246 real Hebrew links carry a `StrongsNumber`; `DqRefreshState.LinkRolesAppliedAt` is NULL. 0018's status says the backfill ran only on `uw_0018_test`, not on the live DB.

**How big the problem is.** 145,687 Hebrew/Aramaic words have more than one morpheme (17,234 have three or more). 143,159 of them are mapped; 105,230 of those have more than one German link (255,537 links). For these the word-level link alone does not say which German word renders which morpheme. The most frequent non-main morphemes: `H9002` ו *and* 29,708, `H9009` ה *the* 23,945, `H9001` waw-consecutive 21,328, `H9005` ל *to* 20,794, `H9003` ב *in* 15,781, `H9023` suffix 7,925.

So the honest answer to the first question: yes, and the pipeline already uses the Strong's numbers of the morphemes. What it does not use is the rest of each morpheme (surface segment, transliteration, gloss piece, grammar code), and no one has measured how well morpheme choices are made.

## End state

1. A separate **morpheme alignment layer**: per Hebrew/Aramaic source morpheme (word + position in word), which Elb1871 word(s) render it, with source (projection, rules, AI, human), author and timestamp. Its own tables; it never writes the main mapping.
2. Three producers fill it: a projection of the existing links, the 0018 rules run in "shadow" mode, and an AI morpheme pass that keeps the word links fixed and only assigns morphemes, with the full STEP segment data in the package.
3. The reader shows a Hebrew word split into its morphemes; hovering a morpheme highlights the German word(s) the layer assigns to it (behind a maintainer toggle while the layer is under evaluation).
4. An evaluation page with coverage, agreement between producers and accuracy against a hand-checked reference set, ending in a recorded decision: promote into the main mapping, keep as a separate layer, or drop.

## Deferred

- Greek. STEP Greek words have one Strong's each; crasis (κἀγώ) and article-plus-noun are not split. Revisit if the Hebrew layer is kept.
- Splitting the German side further than 0018 already does (`GermanWordPart`, `APPRART` units like *zum*).
- Revision variants (`RevisionMapping`): the layer anchors to Elb1871 words only.
- Seed export of the layer. Not before the decision gate ([04-evaluation.md](04-evaluation.md)).
- Vocab trainer use of morpheme data (prefix drills).
