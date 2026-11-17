# 0018 · German lemma rules

The ground rules for every lemma, whether set by a human, the algorithm or a model. They go into the prompt instructions verbatim (0018.6) and into `AgentLemmaRules` where they are checkable (0018.5). Headword source: Duden spelling of today ([02-decisions.md](02-decisions.md)).

## Invariants

1. One German word, one lemma. Never a multi-word lemma (`sich fürchten`, `getötet werden`); `AgentLemmaRules.MultiWordLemmas` stays empty.
2. The lemma describes the German word itself, not the source word. `wurde` is `werden` even when it is part of the rendering of ἀποκτείνω; the role ([04-roles-and-counting.md](04-roles-and-counting.md)) says it is not counted.
3. Only a finite separable verb absorbs another token's meaning (its particle). No other lemma contains a neighbouring word.

## By word class

| Class | Rule | Examples |
|---|---|---|
| Verb | Infinitive, modern spelling | `ward` → `werden`, `sahe` → `sehen`, `thut` → `tun`, `gieng` → `gehen` |
| Separable verb | Finite verb gets particle + verb; particle keeps itself, STTS `PTKVZ` | `stand … auf` → `aufstehen` / `auf`; `kehrte … zurück` → `zurückkehren` / `zurück`; contiguous `hinausgegangen` → `hinausgehen` |
| Not a particle | Preposition or `zu` with an object or infinitive is never `PTKVZ` | Mk 1:29 `aus der Synagoge` → `aus` `APPR`; Mk 1:45 `zu ihm` → `APPR` |
| Periphrastic tense, passive | Each word its own lemma | `habe … getauft` → `haben` / `taufen`; `wurde getötet` → `werden` / `töten` |
| Modal / hortative | Own lemma | `laßt uns gehen` → `lassen` / `wir` / `gehen` |
| Reflexive | Verb without `sich`; pronoun lemma `sich` (3rd person) or the personal pronoun | `fürchtete sich` → `fürchten` / `sich`; `freue mich` → `freuen` / `ich` |
| `zu` before infinitive | `zu`, STTS `PTKZU` | `zu essen` → `zu` / `essen` |
| Noun | Nominative singular, capitalised; plural-only nouns as Duden lists them | `Söhnen` → `Sohn`, `Thür` → `Tür`, `Leute` → `Leute` |
| Nominalised verb or adjective | Capitalised noun headword | `das Gute` → `Gute`, `das Leben` → `Leben`; never `Rede` tagged VERB |
| Adjective | Positive, uninflected | `Heiligem` → `heilig`, `größer` → `groß` |
| Participle as adjective | `ADJA`/`ADJD` → adjective headword if Duden has one, else the verb | `geliebter Sohn` → `geliebt`; `der verdorrten Hand` → `verdorrt` |
| Article | `der` / `ein` for every form | `das`, `dem` → `der`; `einem` → `ein` |
| Personal pronoun | Nominative of the same person | `ihm` → `er`, `uns` → `wir`, `mir` → `ich`, `euch` → `ihr` |
| Possessive | Masculine nominative | `ihren` → `ihr`, `seines` → `sein` (`PPOSAT`, not the verb `sein`) |
| Contraction | The preposition | `zum` → `zu`, `im` → `in`, `vom` → `von`, `am` → `an` |
| Name | Nominative, no genitive `s` | `Simons` → `Simon` (`NE`), `Jakobus` → `Jakobus` |
| Number | Cardinal as written in modern spelling; ordinals by Duden headword | `zwölf`, `dritte` |
| Compound | Lemma of the whole compound; parts get a part lemma (0018.2) | `Gerstenernte` → `Gerstenernte`, parts `Gerste`, `Ernte` |

## Homographs (model decides)

`sein` (verb `VAFIN` vs. possessive `PPOSAT`), `Weg` / `weg`, `Arm` / `arm`, `da` (adverb vs. conjunction), `als`, `daß` / `das`, `ward` vs. `wart`, `Gott` vs. `Götter`. The tag and the lemma change together; `LemmaSttsGuard` stays the STTS ↔ POS check.

## Checkable rules (validator)

- Well-formed, capitalisation by STTS (exists in `AgentLemmaRules`).
- `PTKVZ` token: its lemma equals its text (lower case); there is a finite verb in the same verse whose lemma starts with the particle.
- No lemma of a non-verb ends with the particle of a neighbouring `PTKVZ`.
- Known 1871 spellings are never accepted as lemmas (`thun`, `Thür`, `seyn`, `gieng`, `ward`): table `LegacySpellings` in `AgentLemmaRules`, extended as reviews find more.
