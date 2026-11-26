# 0029 · Search model and API

## Code layout

| Where | What |
|---|---|
| `Application/Features/GrammarSearch` | `GrammarVocabulary` (keys, aliases, values → `MorphologyKey` / UD value, de label, en label), `GrammarQueryParser` (pure: tokens → slots, errors), `GrammarFilterResolver` (filters → Greek/Hebrew code sets via the index), `GrammarSearchService` |
| `Infrastructure/GrammarSearch` | `MorphologyCodeIndex` (distinct codes, decoded, cached), `GrammarSearchRepository` (Dapper) |
| `Domain.WebApi/Elberfelder/SearchGrammar` | request/response contracts |
| `Tooling.WebApi/Endpoints/Elberfelder/Search/Grammar` | endpoints, validators |

## MorphologyCodeIndex

Loads `select distinct "Morphology" from "StepGreekWords"` and `select distinct "Grammar" from "StepHebrewWords"` (about 4,200 rows), decodes each with `GreekMorphologyDecoder.Decode` / `HebrewMorphologyDecoder.Decode(...).Root`, and keeps `code → MorphologyKeys` plus the Aramaic flag. Loaded on first use and re-read after 30 minutes, like `SourceFormIndex`. A filter set (all filters of one slot) resolves to the codes whose keys satisfy every filter; an empty set means "no hits" without a query.

The same index answers the facet counts' labels: the per-code counts come from SQL, the grouping by key from the index.

## Query per slot

Each slot becomes a CTE returning `(HebRefId, WordKey)`; `WordKey` identifies the original word (or the German word for a German-only slot) so that two slots cannot use the same word.

- **Greek side:** `"StepGreekWords"` with `"Morphology" = any(@codes)` and, with a lemma, the Strong's match used by `GetStrongsOccurrences`; `LxxRefId` → `HebRefId` through `Elb1871Verses`.
- **Hebrew side:** `"StepHebrewWords"` with `"Grammar" = any(@codes)` and the root Strong's expression of the existing index.
- **German side:** `"Elb1871Words"` joined with `"Elb1871Morphology"` on (`HebRefId`, `PositionInVerse`), filters as column predicates, lemma via `lower("Lemma")`.
- **Both sides in one slot:** join through `Elb1871GreekMapping` / `Elb1871HebrewMapping` (`StepWordId` ↔ `ElbWordId`).

The verse set is the intersection of the slots' `HebRefId`s (with the distinct-word rule checked in C# for verses where two slots share candidates). Total occurrences, total verses and counts per book are computed on that set; the page (canonical order, 50 verses) loads the Elb1871 words of the hit verses and the matched words' details.

All SQL follows the project's SQL-formatting skill; user text never reaches SQL (codes and Strong's are bound parameters). A migration (next free number) adds btree indexes on `StepGreekWords("Morphology")` and `StepHebrewWords("Grammar")`, plus whatever `EXPLAIN ANALYZE` of the heavy cases in [06-tasks.md](06-tasks.md) shows to be missing.

## Endpoints

All public, rate-limit group `Search`, `Cache-Control: public, max-age=60` (vocabulary: one day).

- `GET api/elberfelder/search/grammar?q=&page=&book=` →
  `{ slots: [{ text, readAs: "lemma"|"german"|"any", lemmas: [{ strongs, lemma, transliteration, gloss }], filters: [{ key, values, negated, german }] }], totalWords, totalVerses, books: [{ bibleBookId, count }], hits: [{ bibleBookId, chapter, verse, words: [{ text, position }], matches: [{ slot, germanPositions: [], original: { form, lemma, strongs, code, keys } }] }] }`.
  400 with the error codes of [03-query-language.md](03-query-language.md#errors).
- `GET api/elberfelder/search/grammar/chapter/{bibleBookId}/{chapter}?q=` → `{ verses: [{ verse, germanPositions: [], originalWordIds: [] }], total }` for one chapter: the slot CTEs restricted to the chapter's `HebRefId`s (cheap, so it runs on every chapter change while marking is on). Accepts word-only queries.
- `GET api/elberfelder/search/grammar/neighbours?q=&bibleBookId=&chapter=` → `{ previous?: { bibleBookId, chapter }, next?: {...} }`, the nearest chapters with a hit (canonical order, one row each way).
- `GET api/elberfelder/search/verses/chapter/{bibleBookId}/{chapter}?q=&level=` and `.../neighbours` → the same for the verse search: positions whose stem or lemma is in `BuiltVerseQuery.MatchStems` / `MatchLemmas`, in verses that match the whole query, at the reader level of the search.
- `GET api/elberfelder/search/grammar/vocabulary` → keys (name, aliases, german prefix allowed, languages), values (key, label de, label en, aliases). Static per deployment.
- `GET api/elberfelder/search/grammar/facets?q=&slot=&key=` → for one slot, the values of one key (or of all keys) with their word counts given the slot's other filters: `agape Kasus:` → Nominativ 36, Akkusativ 33, Dativ 29, Genitiv 18.
- `GET api/elberfelder/strongsOccurrences/strongs/{Strongs}?grammar=` (existing endpoint, new optional parameter): filter tokens of the same language, parsed by `GrammarQueryParser` as one slot bound to that Strong's.
