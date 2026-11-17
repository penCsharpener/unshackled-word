# 0018 · Link roles and counting

## The source unit

A **source morpheme** is one source word plus one Strong's in it:

- Greek: the word (`StepWordId`, `StrongsNumber` already on the link). Greek words have one Strong's.
- Hebrew: the word plus one morpheme of `ExpandedStrongTags`, e.g. וַיֹּאמֶר = H9001 (`und`) + H0559 (`sprechen`). Punctuation tags (H9014–H9018) are never morphemes.

## Roles

| Role | Meaning | Counted | Examples |
|---|---|---|---|
| `head` | Carries the meaning of the morpheme | yes | `getötet` → G0615, `sprach` → H0559, `und` → H9001, `ihren` → H9024 (suffix "her") |
| `unit` | Belongs to the head's German form, adds no own meaning of the source | no | `wurde` in `wurde getötet`, `habe` in `habe getauft`, `aus` in `gingen … aus`, `sich`, `zu` before an infinitive, `zum`'s article part |

Not a link at all: subject pronouns supplied by the verb ending (`er` in `und er sprach`, `sie` in Mk 1:29) and articles/words German grammar needs. They are added words with `ParentGermanWordId` = the verb or noun (existing convention, Gen 1:3 `Es`).

## Invariants

1. Every source morpheme with a real link has **exactly one** `head`, or several only when the validator flag `multi-head` is accepted by the maintainer (idioms like `zur Antwort geben`; rare).
2. A `unit` link always has a `head` link on the same source morpheme. One exception: a contraction (`APPRART`: `zum`, `im`, `vom`) is the head of the preposition and a `unit` of the article ὁ; that article instance counts as "rendered, no own lemma".
3. A German word is the `head` of at most one morpheme per source word.
4. Within one source morpheme no German lemma appears twice — follows from the lemma rules plus 1–3; checked anyway.

## Counting

The **rendering** of one source morpheme instance = the lemmas of its head links in verse order, joined by a space (almost always one lemma). The part lemma replaces the lemma when the head is a compound part.

- Source frequency: `count(DISTINCT source morpheme instance)` per Strong's.
- Rendering count: `count(DISTINCT source morpheme instance)` per Strong's and rendering. The counts of all renderings of a Strong's add up to its aligned count; nothing is counted twice.
- German lemma frequency: `count(*)` of German words per lemma — one lemma per word, so a separable verb counts once (`ausgehen` on the verb, `aus` on the particle).
- Example sentences: the verses of the instances, with head and unit words highlighted together.

Worked examples (expected after 0018):

| Verse | Source | Links | Rendering |
|---|---|---|---|
| Gen 1:3 | וַיֹּאמֶר H9001 + H0559 | `Und` head H9001, `sprach` head H0559 | H9001 `und`, H0559 `sprechen` |
| Gen 1:3 | יְהִי H1961 | `Es` added (parent `werde`), `werde` head | `werden` |
| Mk 1:8 | ἐβάπτισα G0907 | `habe` unit, `getauft` head | `taufen` |
| Mk 1:29 | ἐξελθόντες G1831 | `sie` added, `gingen` head (`gehen`) | `gehen` |
| Mk 1:29 | ἐκ G1537 | `aus` head (`aus` `APPR`, not a particle) | `aus` |
| Mk 1:35 | ἐξῆλθεν G1831 | `ging` head (`hinausgehen`), `hinaus` unit | `hinausgehen` |
| Rt 1:22 | שְׂעֹרִים / קְצִיר | `Gerstenernte` parts | `Gerste`, `Ernte` |
