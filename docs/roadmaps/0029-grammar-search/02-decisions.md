# 0029 · Decisions

## Maintainer (interview 2026-11-26)

- **Search overlay, Grundtext tab.** Grammar queries are typed in the existing search box and answered in the Grundtext tab as a verse hit list. The Strong's page gets grammar facets later in the roadmap.
- **Grammar without a lemma is allowed**, paged and counted per book (`Tempus:aor Diathese:pass`).
- **All three grammars:** Greek NT (Robinson), Hebrew/Aramaic OT (OSHB), German Elb1871 (UD/STTS).
- **German grammar uses a prefix in the same box:** `dt.Kasus:akk` constrains the German word, `Kasus:akk` the original word.
- **German labels first.** Users type the grammatical terms in German; English is low priority.
- **The query stays in the box until it is cleared** (2026-11-26), and a switch marks the hits of the current query in the chapter being read, also after navigating. The reader can type a query, ignore the result list and read with marks instead.
- **Numbering (2026-11-26):** continue from `feature/lxx` (0.13.18, migrations to 0053): version 0.14.0, migrations 0054 (Greek index) and 0055 (Hebrew index).

## Plan decisions

- **One word = one slot.** A filter binds to the nearest word before it; filters before any word form a slot of their own ("any word"). Slots are AND-ed per verse, without order or distance. Details in [03-query-language.md](03-query-language.md).
- **Filters resolve to code sets in memory, not to new columns.** All distinct codes (1,161 Greek, 3,081 Hebrew) are decoded once by the existing decoders into a cached `MorphologyCodeIndex`; a filter becomes `"Morphology" = any(@codes)`. No derived table, nothing to rebuild, and the decoders stay the single source of what a code means. German filters use the `Elb1871Morphology` columns directly.
- **The backend owns the search vocabulary** (keys, aliases, values, German labels; English labels as data only) in `Application/Features/GrammarSearch/GrammarVocabulary`, served to the frontend for autocomplete. The labels must equal `de.ts` `grammar.*`; a test enforces it ([07-risks.md](07-risks.md)).
- **Value matching by unique prefix**, case and umlaut folded (`Kasus:nom` = Nominativ). An ambiguous prefix (`Tempus:p`) does not search; it answers with the candidates.
- **Hebrew filters apply to the root segment** (`HebrewMorphologyResult.Root`), Hebrew lemmas match the root Strong's (`split_part("RootDisambiguatedStrongsInstance", '_', 1)`, already indexed).
- **Deponents follow the decoder:** Robinson `D` counts as Medium, `O` as Passiv, `E`/`N` as "Medium oder Passiv". `Diathese:pass` therefore includes passive deponents; the raw code stays reachable via `Code:`.
- **`dt.Kasus` is not offered** until the German case tags pass a check: the tagger marks 125,193 nouns nominative and 2,361 accusative, and John 3:16 tags "die Welt" and "Sohn" as nominative ([07-risks.md](07-risks.md)). The other German keys come from STTS-derived features that look sound.
- **Hits key on Elb1871 verses** (`HebRefId`); Greek words map via `LxxRefId` → `HebRefId` through `Elb1871Verses` as in roadmap 0027.
- **Marking applies to both tabs:** the tab the query was typed in decides the engine (grammar search in Grundtext, verse search in Verse). A word-only Grundtext query (`agape`) marks too, without being a grammar query.
- **Query and switch are kept in `localStorage`** (per-viewer convenience, wrapped in try/catch); the visible reader chip is the reminder that marks are on.
- **Public read**, rate-limit group `Search`, like the other search endpoints.

## Findings in the code

- No combobox/autocomplete component exists (`Components.md`); the autocomplete is a new component, proposed in chat first.
- Search endpoints live in `Tooling.WebApi/Endpoints/Elberfelder/Search/*`; contracts in `Domain.WebApi/Elberfelder/Search*`.
- Indexes today: `StepGreekWords` on `DisambiguatedStrongs`, `LxxRefId`; `StepHebrewWords` on root Strong's, `LxxRefId`; none on `Morphology`/`Grammar`. `Elb1871Morphology` is unique on (`HebRefId`, `PositionInVerse`).
