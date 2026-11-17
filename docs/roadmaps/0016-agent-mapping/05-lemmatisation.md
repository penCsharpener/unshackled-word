# 0016 · Lemmatisation

## Split of work

| Who | Decides |
|---|---|
| Algorithm | Lemma of every word a human already edited (exact word id); lemma of forms with one known lemma in the lexicon; casing (nouns capitalised); STTS ↔ POS fit |
| Model | Homographs (`sein` verb vs. pronoun, `Weg`/`weg`, `Arm`/`arm`), separable verbs (`er ging hinaus` → `hinausgehen`), archaic forms of 1871 (`ward` → `werden`, `thun` → `tun`?), reflexives, forms LemmaSharp got wrong |

## Lemma lexicon (deterministic, built once per run)

A form → lemmas table from three sources, in priority order:

1. Human edits (`Elb1871MorphologyEdits`, current value) — trusted.
2. Merged AI proposals — trusted after merge.
3. LemmaSharp output (`Elb1871Morphology`) — candidate only.

A human edit of this exact word is `fixed`; a form with exactly one trusted lemma elsewhere is a `hint`. A form with several lemmas, or only a LemmaSharp lemma, is a `hint` and the model sees all candidates (`lemma_candidates` tool for MCP agents).

## 1871 spelling

Elb1871 uses old spelling (`thun`, `Thür`, `seyn`?). Open question for the maintainer: is the lemma the 1871 headword or the modern one? The plan assumes **modern headword** (search and study tools match modern German), with the rule written once in the instructions and a small fixed table of known pairs in the lexicon.

## Separable verbs

**Superseded by roadmap 0018:** the particle keeps its own lemma (`stand … auf` → `aufstehen` / `auf`), see [0018 03-lemma-rules.md](../0018-german-lemmas/03-lemma-rules.md) (prompts `greek-v2`/`hebrew-v2`, validator `AgentLemmaRules.CheckParticle`). The old rule below is kept for the record (prompt v1).

The model returns the full lemma on the verb token and the same lemma on the particle token (decided, [02-decisions.md](02-decisions.md)). The validator checks that the particle is a prefix of that lemma. The `PTKVZ` tag is not trusted as input: `zu` alone has 10,922 `PTKVZ` tokens, many of them preposition or infinitive `zu` (Mk 1:45 `zu ihm`); the model re-decides the tag, and a particle lemma without a verb in the same clause is rejected.

## Lemma checks

Listed in [06-validation.md](06-validation.md) (section "Lemmas").
