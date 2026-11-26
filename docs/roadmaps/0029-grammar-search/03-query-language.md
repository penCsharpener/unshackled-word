# 0029 · Query language

## Tokens

The query is split at whitespace ("quoted phrases" are not supported in grammar queries). Each token is one of:

| Token | Example | Meaning |
|---|---|---|
| filter | `Kasus:nom`, `Kasus:nom,akk`, `-Modus:partizip` | key `:` value; comma = OR within the key; leading `-` excludes |
| German filter | `dt.Verbform:finit` | same, applied to the German word of the slot |
| explicit word | `Strong:G26`, `Lemma:agape`, `dt.Wort:Liebe` | forces how a word is read (see below) |
| word | `agape`, `ἀγάπη`, `G26`, `ברא`, `Liebe` | opens a new slot |

Limits: at most 4 slots, 12 filters, 200 characters (the verse search limit).

## Slots

A **slot** is one word position in a verse: an original word, optionally together with the German word(s) mapped to it. Every word token opens a slot; every filter binds to the nearest word before it. Filters before the first word form a slot without a lemma.

```
agape Kasus:nom pistis Kasus:gen      → slot 1: G26 + nominative, slot 2: G4102 + genitive
Tempus:aor Diathese:pass              → slot 1: any word + aorist + passive
Liebe dt.Numerus:plural               → slot 1: German lemma Liebe, German plural
Tempus:aor Modus:partizip dt.Verbform:finit
                                      → slot 1: Greek aorist participle rendered by a finite German verb
```

A verse is a hit when every slot is satisfied by some word of the verse (AND, no order, no distance; two slots may not be satisfied by the same word).

## Reading a word token

In this order, first match wins:

1. Strong's number (`G26`, `H1254`) → that lemma.
2. Greek or Hebrew script → lemmas whose folded lemma or inflected form equals it (`SourceWordSearchService`, tiers 1–2).
3. Latin letters that equal a transliteration (folded, `TranslitFolding`) → those lemmas (`agape` → G26).
4. Otherwise → German lemma (`Elb1871Words.Lemma`, case-insensitive): the slot needs a German word with this lemma.

The response reports for each slot how it was read (lemmas with Strong's, or "deutsches Wort"), so a wrong guess is visible; `Lemma:` and `dt.Wort:` force step 3 or 4.

## Keys

Key names and aliases are matched exactly after folding (case, umlauts, ß). Values are labels from `de.ts` `grammar.*`; a unique folded prefix is enough, an exact label beats a longer one (`Tempus:futur` = Futur, not Futur II), an ambiguous prefix (`Tempus:p`) returns the candidates instead of hits.

| Key (aliases) | Values | Applies to |
|---|---|---|
| `Wortart` (`Wa`) | Substantiv, Eigenname, Verb, Adjektiv, Adverb, Artikel, Pronomen, Präposition, Konjunktion, Partikel, Interjektion, indeklinabel | Greek, Hebrew |
| `Tempus` (`Zeit`) | Präsens, Imperfekt, Futur, Aorist, Perfekt, Plusquamperfekt, Futur II | Greek |
| `Diathese` (`GenusVerbi`) | Aktiv, Medium, Passiv, Medium oder Passiv | Greek |
| `Modus` | Indikativ, Konjunktiv, Optativ, Imperativ, Infinitiv, Partizip | Greek |
| `Person` | 1, 2, 3 (labels "1. Person" …) | Greek, Hebrew |
| `Kasus` (`Fall`) | Nominativ, Genitiv, Dativ, Akkusativ, Vokativ | Greek |
| `Numerus` (`Zahl`) | Singular, Dual, Plural | Greek, Hebrew |
| `Geschlecht` (`Genus`) | maskulin, feminin, neutrum, beiderlei | Greek, Hebrew |
| `Steigerung` | Komparativ, Superlativ | Greek |
| `Stamm` | Qal, Nifal, Piel, Pual, Hifil, Hofal, Hitpael, anderer Stamm (also `niphal`, `hiphil` …) | Hebrew |
| `Form` | Perfekt (Qatal), Imperfekt (Jiqtol), Weqatal, Wajjiqtol, Kohortativ, Jussiv, Imperativ, Infinitivus absolutus/constructus, Partizip aktiv/passiv (also `qatal`, `jiqtol`) | Hebrew |
| `Status` | absolutus, constructus, determiniert | Hebrew/Aramaic |
| `Sprache` | Griechisch, Hebräisch, Aramäisch | all |
| `Code` | raw code prefix, `*` as wildcard (`Code:V-2A*`) | Greek, Hebrew (root segment) |

A key that does not exist in a language simply matches nothing there; the autocomplete marks the language ([05-ui.md](05-ui.md)).

German keys (prefix `dt.`), from `Elb1871Morphology`:

| Key | Values | Column |
|---|---|---|
| `dt.Wortart` | Substantiv, Verb, Adjektiv, Artikel, Pronomen, Präposition … (UD → `grammar.partOfSpeech` labels) | `PartOfSpeech` |
| `dt.Tempus` | Präsens, Präteritum | `Tense` |
| `dt.Modus` | Indikativ, Konjunktiv, Imperativ | `Mood` |
| `dt.Verbform` | finit, Infinitiv, Partizip | `VerbForm` |
| `dt.Person`, `dt.Numerus`, `dt.Geschlecht`, `dt.Steigerung` | as above | `Person`, `Number`, `Gender`, `Degree` |
| `dt.STTS` | raw STTS tag (`VVPP`, `NN` …) | `Stts` |

`dt.Kasus` is withheld ([02-decisions.md](02-decisions.md)). Labels missing from `de.ts` (Präteritum, finit) are added there.

## Errors

Unknown key, unknown or ambiguous value, too many slots/filters and a query without any slot are 400 problems with a machine-readable code and the offending token (`{ code: "ambiguousValue", token: "Tempus:p", candidates: [...] }`), so the UI can mark the token instead of showing a generic error.
