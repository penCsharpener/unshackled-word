# 0029 · Goal

## What is missing

The search overlay (`F`) finds German verses (tab "Verse") and Greek/Hebrew lemmas by script, Strong's number, transliteration or gloss (tab "Grundtext", `api/elberfelder/search/sourcewords`). It cannot answer a grammar question: "where does ἀγάπη stand in the nominative?", "all aorist passives in the NT", "Qal of ברא".

The data is there, as codes nobody types:

- `StepGreekWords.Morphology`: 142,095 NT words, 1,161 distinct Robinson codes (`N-NSF`, `V-AAI-3S`, `V-2APS-3P`).
- `StepHebrewWords.Grammar`: 305,648 OT words, 3,081 distinct OSHB codes, several segments per word (`HR/Ncfsa`, `HTd/Ncmsa`, `HVqp3ms`).
- `Elb1871Morphology`: 721,542 German words with UD features (`Case`, `Gender`, `Tense`, `Mood`, `VerbForm` …) and 43 STTS tags.

`GreekMorphologyDecoder` and `HebrewMorphologyDecoder` (`Domain/Models/Grammar`) already turn these codes into the keys of `MorphologyKey` (`aorist`, `passive`, `nominative` …), and `de.ts` (`grammar.*`) already holds their German labels (Aorist, Passiv, Nominativ, maskulin). Search does not use either.

Real numbers from the dev database: G26 ἀγάπη occurs 116 times (36 nominative, 33 accusative, 29 dative, 18 genitive); the decoder's "aorist passive" (Robinson `AP` and passive deponent `AO`) covers 1,706 NT words.

## End state

In the Grundtext tab a reader types `agape Kasus:nom` and, while typing, sees "Nominativ" offered; `Geschlecht:mas` offers "maskulin". The result is a paged verse list (Elb1871 text, the rendering German words highlighted, the Greek form with its grammar in words) with counts per book. Grammar alone works (`Tempus:aor Diathese:pass`), Hebrew works (`bara Stamm:qal`), and the German side of the same word can be constrained with the prefix `dt.` (`Tempus:aor Modus:partizip dt.Verbform:finit`). The query stays in the box until the reader clears it; with "Treffer im Text markieren" on, the words it finds are marked in whatever chapter the reader opens, with a jump to the previous and next chapter with a hit. The Strong's page shows how often each form occurs and filters its occurrence list by it.

## Out of scope (deferred)

- English keys and values (`case:nom`); the vocabulary carries English labels but the parser accepts German only.
- Word order and distance between slots ("ἀγάπη followed by θεοῦ"), syntax relations.
- Filters on Hebrew prefix/suffix segments (article, conjunction, suffix person); only the root segment is searchable.
- Search over revision variants (reader level); hits show Elb1871 text and its mapping.
- LXX (roadmap 0028) and an MCP tool for grammar search.
