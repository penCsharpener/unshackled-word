# Roadmap 0008: Mapping stats snapshot (pre-aggregated German ↔ Strong's frequencies)

Carved out of `0007-smart-search.md` (the "snapshot table" idea). 0007 consumes it; it can be built and shipped alone.

## Goal

Pre-aggregate, once per refresh, how often each original word (Strong's) is rendered by which German lemma + PoS and
the other way round, so that readers never aggregate ~0.75 M mapping facts per request.

- **Smart search (0007):** for every token of the query, resolve the German form to its lemma and list the mapped
  Greek/Hebrew originals by frequency in one indexed read.
- **Strong's page `/bible/strongs/{strongs}`** (`strongsSummary`) and **word stats** (`wordStats`): renderings,
  mapped count and book counts from the snapshot instead of live joins (today slow for very frequent words such as
  `und`, `der`, H0853).
- **Correct German key:** the snapshot keys German words by the `Elb1871Morphology` lemma + PoS. The Strong's page
  today keys by the obsolete `Elb1871Words.Lemma` (empty, so it really groups by the lower-cased form); switching the
  readers to the snapshot fixes that as a side effect.
- **Deprioritise very frequent words** in search (cost and noise), while the Strong's page still shows them.

Out of scope: the search overlay UI itself (0009), the `Elb1871Morphology` export/import design (prerequisite 0008.0,
roadmap 0010), Mapping v2 (0005; it only changes the source of `DqWordFacts`), STTS tag table and the repo seed format
(both roadmap 0010), any write path to mappings, automatic refreshes.

## Decisions

| Topic | Decision | Source |
|---|---|---|
| Data source | Legacy mapping tables (`Elb1871GreekMapping` / `Elb1871HebrewMapping`) for now, through `DqWordFacts` | maintainer, 2026-11-12 |
| German lemma / PoS / morphology | Only from `Elb1871Morphology`, joined on `HebRefId` + `PositionInVerse` (as `DqWordFacts` does). `Elb1871Words.Lemma` is obsolete and never read | maintainer, 2026-11-12 |
| Strong's from | The STEP word tables (via `DqWordFacts` / `StepWordId`), never `Elb1871GreekMapping.StrongsNumber` (stale) | 0012 header |
| Storage | Materialized views chained on `DqWordFacts` | maintainer, 2026-11-12 |
| Strong's key | `DqWordFacts` keys are canonical (`StrongsFull` for grouping, `StrongsBase` for the number without letter); exact STEP strings stay reachable through `StepWordId` (see "Strong's keys and the way back") | maintainer, 2026-11-12 |
| `Elb1871Morphology` | Its export and import (`CREATE TABLE` + seed) are still to be done; explicit prerequisite slice 0008.0, designed in roadmap 0010 (0010.1, 0010.4) | maintainer, 2026-11-12 |
| Rendering grain | Per Strong's × German lemma × PoS | maintainer, 2026-11-12 |
| Frequent words | Lemma (summed over PoS) with `Occurrences >= 2000` is "high frequency"; final value, a C# constant evaluated at read time (no refresh needed to change it) | maintainer, 2026-11-12 |
| Refresh | Manual only: the existing "Neu berechnen" button / `POST api/dataQuality/refresh` on the data-quality page, plus a "Neu berechnen" button on the Strong's page for users with write permission (same endpoint and roles). No automatic, debounced or startup refresh | maintainer, 2026-11-12 |
| Morpheme column | Stays deferred; if built, as a side view `DqSourceMorphemes` in its own script, not as a `DqWordFacts` column (see "Deferred") | maintainer, 2026-11-12 |
| `wordStats` | In scope | maintainer, 2026-11-12 |
| Fuzzy search | No `pg_trgm` for now; exact form/lemma and prefix lookups only | maintainer, 2026-11-12 |

## How to use this roadmap

Work in table order or as *Depends on* allows; one task = one commit; tick `[ ]` → `[x]`. A slice is the backend row plus its `f` row of the
same number; both land before the slice counts as done, and every slice leaves `dotnet build UnshackledWord.slnx`,
`dotnet test` and `docker compose run --rm --no-deps unshackledword.web bun run check` green. Conventions of 0004
("Conventions for all steps") apply: Dapper only, `*Sql` constants in repo SQL style, FakeDbReader tests, design
tokens, i18n de/en, `Components.md`, Bruno files updated in the slice that changes a contract. DB experiments on a
scratch copy only, never on the shared DB copy.

## Tasks

| ✔ | # | Task | Depends on | Done when |
|---|---|---|---|---|
| [x] | 0008.0 | **Prerequisite, owned by roadmap 0010 (`0010-export-import-seed.md`), rows 0010.1 (schema-only migration with the `CREATE TABLE`, ordered before 0010/0012) and 0010.4 (`MorphologySeedRunner`):** `Elb1871Morphology` export to the repo and import on fresh installs | – | A fresh compose stack has a filled `Elb1871Morphology` before `0012_DataQuality.sql` runs; `DqWordFacts` can be refreshed there |
| [x] | 0008.1 | Migration `scripts/0013_MappingStats.sql`: the four `Stats*` materialized views (`WITH NO DATA`, idempotent guard as in 0012) with unique and lookup indexes; `ALTER TABLE "DqRefreshState" ADD COLUMN IF NOT EXISTS "StatsRefreshedAt" timestamptz NULL` | 0008.0 | Runs on a fresh scratch DB after 0001–0012 and twice in a row; DbUp journal shows `0013`; manual `REFRESH` of DqWordFacts then all four views succeeds on the full copy; measured durations and row counts written into "Background" |
| [x] | 0008.2 | Refresh chain: `DqOverviewService.RefreshAsync` refreshes `DqWordFacts` and then the `Stats*` views (non-concurrent while unpopulated, `CONCURRENTLY` afterwards), writes `StatsRefreshedAt`; `overview` returns `statsRefreshedAt`. Trigger stays the existing manual endpoint, nothing else starts a refresh | 0008.1 | `DqFactsRepository` SQL tests for both refresh variants and the order; service test: a failing stats refresh leaves `RefreshedAt` updated and `StatsRefreshedAt` unchanged, error logged, coordinator flag released |
| [x] | 0008.2f | Data-quality header shows the stats timestamp next to the facts timestamp ("Statistik: Stand …"); "Neu berechnen" label/tooltip says it recomputes both; "wird berechnet …" covers both | 0008.2 | Vitest on the header with and without `statsRefreshedAt`; i18n de/en keys present |
| [x] | 0008.3 | `IStrongsStatsRepository` gets snapshot readers for renderings, mapped count, key counts and book counts (`StatsStrongs`, `StatsStrongsRenderings`); `StrongsSummaryService` uses them when `StatsRefreshedAt` is set and falls back to the live SQL otherwise; response gains `statsRefreshedAt`; renderings carry `pos` | 0008.2 | Service tests: snapshot path, fallback path, multi-key variants summed (`StrongsMatchKeys`), same lemma with two PoS = two rows; on the refreshed scratch copy `strongsSummary` for G2424, G2316, H0430, H0853, H8628 equals the live path except the documented lemma-vs-form differences and the 34 compound Hebrew words; p95 for H0853 and G2532 < 100 ms |
| [x] | 0008.3f | Strong's page: rendering rows show lemma + PoS label (`gehen` Verb, `Gang` Nomen) instead of the form; footer line "Statistik: Stand …" | 0008.3 | `StrongsPage.test.tsx` fixture with `statsRefreshedAt` and lemma/PoS renderings; `bun run check` green |
| [x] | 0008.4 | `wordStats` (German word → books and originals) from `StatsGermanLemma` and `StatsStrongsRenderings`; the query word is resolved form → lemma(s) + PoS through `StatsGermanForms`; optional `pos` filter, otherwise summed over PoS | 0008.2 | `WordStatsService` tests: lemma hit, form hit (`ging` → `gehen`), homograph with two PoS, `pos` filter, unknown word, fallback while unpopulated; scratch copy: `wordStats?word=gehen` lists the same originals as the live path summed over all forms of `gehen` |
| [x] | 0008.4f | Word stats view states which lemma a typed form was resolved to ("ging → gehen, Verb") and offers a PoS switch when the lemma has several | 0008.4 | Vitest for the resolved-lemma line and the PoS switch; i18n de/en |
| [x] | 0008.5 | Lookup endpoint for smart search `GET api/elberfelder/search/originals?q=…`: tokenises, resolves each token via `StatsGermanForms`, returns per token and lemma+PoS the originals by frequency (top N, Strong's, lang, lemma, gloss from `StepStrongsLexicon`, count, share); lemmas at or above `StatsRules.HighFrequencyThreshold` (2,000) come back with `highFrequency: true` and no originals unless `includeFrequent=true` | 0008.4 | Validator tests (empty, too long, too many tokens); service tests for tokenising, the threshold (1,999 / 2,000 boundary) and `includeFrequent`; Bruno `SearchOriginals.bru`; p95 < 50 ms for a 5-token query on the scratch copy |
| [x] | 0008.5f | Typed client + hook `useOriginalsLookup` (no UI; the overlay is 0007) | 0008.5 | Vitest for the hook against a contract fixture; `bun run check` green |
| [x] | 0008.6 | Refresh state for the Strong's page: `strongsSummary` returns `statsRefreshing` (from `DqRefreshCoordinator.IsRunning`) next to `statsRefreshedAt`; no new write endpoint, the button reuses `POST api/dataQuality/refresh` (`[RequiresLogin]`, roles `Admin`/`Maintainer`) | 0008.3 | Service test: `statsRefreshing` true while the coordinator runs; endpoint test: anonymous and non-writer POST still 401/403 |
| [x] | 0008.6f | Strong's page: `Button` ghost "Neu berechnen" next to "Statistik: Stand …", rendered only for users with write permission (same roles as the endpoint, from the auth session); POST, then "wird berechnet …" and polling `strongsSummary` every 5 s until `statsRefreshedAt` changes; 409 = already running (same hint); 403 = hide button | 0008.6 | Vitest: hidden for anonymous / reader, visible for maintainer, 202 → refreshing state → new timestamp, 409 handled; i18n de/en; `bun run check` green |
| [x] | 0008.7 | Final verification and docs: README "data quality / stats" section (what is a snapshot, that it is refreshed only by the buttons, fallback while never refreshed); keep the live path | 0008.3–0008.6 | Comparison queries ("Verification") documented with results on the scratch copy; `dotnet test` and `bun run check` green |

Ordering: 0008.0 → 0008.1 → 0008.2 is the hard start (0008.1/0008.2 may be developed on the maintainer's DB copy, which
already has `Elb1871Morphology`, but are not done before 0008.0 lands). 0008.3 and 0008.4 are independent after 0008.2;
0008.5 needs 0008.4 (forms resolution), 0008.6 needs 0008.3.

## Design

### What is aggregated

Four materialized views, all built **from `DqWordFacts` only** (no join back to the raw tables):

| View | Grain (unique key) | Columns | Main reader |
|---|---|---|---|
| `StatsStrongsRenderings` | (`Lang`, `StrongsKey`, `GermanKey`, `Pos`) | `StrongsBase`, `GermanDisplay` (mode of lemma/form), `KeyIsForm`, `SourceCount` (distinct STEP words), `GermanCount` (distinct German words), `VerseCount` | Strong's page renderings, smart search, wordStats originals |
| `StatsStrongs` | (`Lang`, `StrongsKey`) | `StrongsBase`, `Occurrences` (all STEP words: aligned + unmappedSource), `MappedCount`, `VerseCount`, `BookCounts` jsonb `[{b, c, v}]`, `TopRenderings` jsonb (top 10 `{k, p, d, c}`) | Strong's page header, book chart |
| `StatsGermanLemma` | (`GermanKey`, `Pos`) | `GermanDisplay`, `KeyIsForm`, `Occurrences`, `UnmappedCount` (`Kind` added/none), `VerseCount`, `BookCounts` jsonb | wordStats, smart search frequency rule |
| `StatsGermanForms` | (`PlainLower`, `GermanKey`, `Pos`) | `Count` | form → lemma resolution (search, wordStats) |

Keys:

- `GermanKey` = `LemmaLower` from `DqWordFacts` (i.e. `Elb1871Morphology.Lemma`); words without a morphology row
  (`HasMorphology = false`) fall back to `PlainLower` with `KeyIsForm = true` and `Pos` null (should be ~0 once 0008.0
  is done). Empty `Plain` is skipped (as today). `Pos` is `DqWordFacts."Pos"` (`Elb1871Morphology.PartOfSpeech`);
  unique indexes use `NULLS NOT DISTINCT` because `Pos` can be null.
- `StrongsKey` = `DqWordFacts."StrongsFull"` for both languages (see next section), `StrongsBase` alongside.
- German counts use `GermanRowNo = 1` rows; rendering counts use `Kind = 'aligned'` rows. **Added-word links are not
  aligned:** the mapping tables hold rows with `IsAddedWord = true` that still carry a `StepWordId` (49,852 Hebrew, 624
  Greek: the added German word, e.g. `der`, is attached to a neighbouring original word, e.g. H0853). The first version of
  0012 wrongly assumed such rows have no `StepWordId` and counted them as aligned (H0853 mapped 6,876 instead of the live
  5,697). Since `0014_AddedWordLinks.sql`, `DqWordFacts` takes only real links (`StepWordId` set and `NOT IsAddedWord`) as
  `Kind = 'aligned'`; a German word with only added-word links is `Kind = 'added'` (one row, `StepWordId` NULL, new column
  `IsAddedWord = true`), and an original word that is only the anchor of added-word links is `unmappedSource`. So every
  `Kind = 'aligned'` consumer (Stats* views, checks M1-M4, explorer) is correct without extra filters, matching the live
  `NOT IsAddedWord`.
- The Aramaic share the page shows today (`Grammar LIKE 'A%'`) is not in `DqWordFacts`; it stays on the live query
  (cheap, one Strong's) unless `OrigClass`'s source string is added later (see "Deferred").

Sketch (repo style; final SQL in 0008.1):

```sql
CREATE MATERIALIZED VIEW "unshackled-word"."StatsStrongsRenderings" AS
SELECT f."Lang"                                                       AS "Lang"
     , f."StrongsFull"                                                AS "StrongsKey"
     , min(f."StrongsBase")                                           AS "StrongsBase"
     , COALESCE(f."LemmaLower", f."PlainLower")                       AS "GermanKey"
     , f."Pos"                                                        AS "Pos"
     , bool_and(f."LemmaLower" IS NULL)                               AS "KeyIsForm"
     , mode() WITHIN GROUP (ORDER BY COALESCE(f."Lemma", f."Plain"))  AS "GermanDisplay"
     , count(DISTINCT f."StepWordId")::int                            AS "SourceCount"
     , count(DISTINCT f."ElbWordId")::int                             AS "GermanCount"
     , count(DISTINCT f."HebRefId")::int                              AS "VerseCount"
FROM "unshackled-word"."DqWordFacts" f
WHERE 1=1
    AND f."Kind" = 'aligned'
    AND f."Plain" <> ''
    AND f."StrongsFull" IS NOT NULL
GROUP BY f."Lang", f."StrongsFull", COALESCE(f."LemmaLower", f."PlainLower"), f."Pos"
WITH NO DATA;

CREATE UNIQUE INDEX IF NOT EXISTS "StatsStrongsRenderings_UQ" ON "unshackled-word"."StatsStrongsRenderings" ("Lang", "StrongsKey", "GermanKey", "Pos") NULLS NOT DISTINCT;
CREATE INDEX IF NOT EXISTS "StatsStrongsRenderings_GermanKey_idx" ON "unshackled-word"."StatsStrongsRenderings" ("GermanKey", "SourceCount" DESC);
```

### Strong's keys and the way back to the exact STEP numbers

Checked against `0012_DataQuality.sql`, `StrongsStatsRepository` / `StrongsMatchKeys` and the maintainer's DB copy
(2026-11-12, 142k Greek / 305,652 Hebrew STEP words):

| | Strong's page today (`StrongsStatsRepository`) | `DqWordFacts` | Same? |
|---|---|---|---|
| Greek | `DisambiguatedStrongs` (`G2424G`) | `StrongsFull` = `DisambiguatedStrongs`; `StrongsBase` = `G\d+` (`G2424`) | identical; every Greek value is a single `G\d+[letter]?` |
| Hebrew | `split_part(RootDisambiguatedStrongsInstance, '_', 1)`: root with letter, instance suffix `_A`/`_B` dropped (`H0853_A` → `H0853`) | `StrongsFull` = first braced morpheme of `DisambiguatedStrongs` (`H9003/{H7225G}` → `H7225G`); `StrongsBase` = `H\d+` of it (`H7225`) | identical for 305,604 rows; differs for 34 compound words with two braced roots (`{H0310A}/{H3651C}`: page key is the unusable string `H0310A, H3651C`, DqWordFacts keeps `H0310A`); 14 rows have empty strings in all three columns |

So **no mapping table from braced number to page key is needed**: `StrongsFull` *is* the page key, and the page's
variant expansion (`StrongsMatchKeys`: `H7225` → all lexicon variants `H7225`, `H7225G`, …) sums `StatsStrongs` rows
over the keys exactly as it sums STEP rows today. The 34 compound words become a small improvement (counted under
their first root instead of nowhere) and are listed in the verification.

What `DqWordFacts` does **not** keep, and how to reach it:

- The raw `DisambiguatedStrongs` (with prefix/suffix morphemes such as `H9002` ו "and", `H9009` ה "the", `H9003` ב "in",
  punctuation `H9014`/`H9016`; 190,857 Hebrew words carry them), the instance suffix (`H0853_A`), the second root of
  compounds, `ExpandedStrongTags` and the Hebrew `Grammar` string. All of these are one primary-key join away:
  `DqWordFacts."StepWordId"` → `StepHebrewWords."Id"` (or `StepGreekWords."Id"`, by `Lang`). Detail views (hits lists,
  word popups) should do that join on the few rows they show, not the snapshot.
- Nothing is needed for parity with the Strong's page: today the page never counts affix morphemes either (its key is
  the root). Stats *per affix* (e.g. how often ו is rendered "und") or per second root would need the side
  view `DqSourceMorphemes`; see "Source morphemes (deferred design)".

### The column question from 0007 (one text column, one with frequencies, or json)

Answer: **a normalized pair view instead.** `StatsStrongsRenderings` has one row per (Strong's, lemma, PoS) with the
count as a real column, so both directions are a plain btree lookup (Strong's → renderings ordered by count; lemma →
originals ordered by count), filtering and paging need no json parsing, and concurrent refresh has a natural unique
key. Display-only shortcuts (`TopRenderings`, `BookCounts`) are jsonb on the per-Strong's / per-lemma rows, because the
page always reads them whole. No FTS (`tsvector`) column and no `pg_trgm`: search tokens are resolved through
`StatsGermanForms` (exact form → lemma) and `text_pattern_ops` prefix indexes on `PlainLower` / `GermanKey`; the verse
hits of the smart search keep using the existing `Elb1871Verses."SearchVector"`.

### Frequency deprioritisation

`StatsRules.HighFrequencyThreshold` (C# constant, 2,000, final) is compared at read
time with the lemma's `Occurrences` summed over its PoS rows in `StatsGermanLemma`. Read-time evaluation means a changed
value needs no migration or refresh. Smart search returns such tokens flagged and without originals unless
`includeFrequent=true`; the Strong's page shows everything, it just reads the precomputed rows, so `und` costs the same
as any other word.

### Refresh strategy

- **Manual only.** The existing "Neu berechnen" button on the data-quality page and the new one on the Strong's page
  (0008.6f, writers only) both call `POST api/dataQuality/refresh` (admin/maintainer, 202, 409 while running); that is
  the only trigger. Edits, bulk apply/revert, mapping saves and API start never refresh. Stats are as old as the last button press; the pages show the timestamp.
- **One pipeline, one coordinator.** `DqRefreshCoordinator` (process-wide flag, `Task.Run`) and
  `DqOverviewService.RefreshAsync` are extended: refresh `DqWordFacts`, then the four `Stats*` views in one connection,
  then store counts and timestamps. The stats views read the facts view, so refreshing them on their own would only ever
  re-read the old facts.
- **Concurrent after the first fill** (`REFRESH MATERIALIZED VIEW CONCURRENTLY` needs the unique indexes and a
  populated view), so readers keep the previous snapshot during a refresh; same logic as `DqFactsRepository.RefreshViewAsync`.
- **Bookkeeping:** `DqRefreshState` stays the single-row state; the new nullable `StatsRefreshedAt` records the last
  successful stats refresh, so a failed stats step is visible while the facts are fresh.
- **Fallback:** while `StatsRefreshedAt` is null (fresh install, never pressed), the Strong's page and `wordStats` use
  today's live SQL (kept until a later cleanup), so they never show empty stats.

### Who reads it

| Reader | Reads | Access |
|---|---|---|
| Strong's page (`strongsSummary`) | `StatsStrongs`, `StatsStrongsRenderings` | public, read-only |
| Word stats (`wordStats`) | `StatsGermanLemma`, `StatsGermanForms`, `StatsStrongsRenderings` | public, read-only |
| Smart search lookup (0008.5, consumed by 0007) | `StatsGermanForms`, `StatsGermanLemma`, `StatsStrongsRenderings` + `StepStrongsLexicon` for gloss | public, read-only |
| Data quality page (optional, later) | M1/M2 rankings could read `StatsStrongsRenderings` instead of grouping `DqWordFacts` | login, maintainer/reviewer |
| MCP server (later) | same lookups, read-only | policy undecided |

### Building on DqWordFacts (how it differs, how it builds on it)

| | `DqWordFacts` (0012) | `Stats*` (this roadmap) |
|---|---|---|
| Grain | one row per German word × aligned source word (+ unmapped rows on both sides), ~0.75–0.8 M rows | aggregates: per (Strong's, lemma, PoS), per Strong's, per (lemma, PoS), per form; estimated 0.1–0.2 M rows in total (to measure in 0008.1) |
| Purpose | find individual wrong facts, page through findings, jump to editors | answer "how often / which" in one indexed read |
| Audience | logged-in maintainers/reviewers | public readers and search |
| Refresh | manual, `CONCURRENTLY`, state in `DqRefreshState` | same button and pipeline, right after `DqWordFacts`, `StatsRefreshedAt` |

Building on it means the expensive part (joining 3–4 large tables, parsing the Hebrew braced morpheme and grammar
class, the `Elb1871Morphology` join) is done once; the stats views are cheap `GROUP BY`s over one relation. When
Mapping v2 (0005) replaces the legacy tables, only the `DqWordFacts` definition changes; the `Stats*` views stay as they
are. Cost: the stats are never fresher than `DqWordFacts`, and a definition change of `DqWordFacts` (drop + recreate)
must drop and recreate the dependent `Stats*` views in the same script.

## Background

- `DqWordFacts` (`scripts/0012_DataQuality.sql`, definition superseded by `0014_AddedWordLinks.sql`, which drops and recreates
  it together with the four `Stats*` views; 0012/0013 stay as applied and only differ in a header note): columns used here are `Lang`, `Kind`, `ElbWordId`, `HebRefId`,
  `BibleBookId`, `Plain`, `PlainLower`, `Lemma`, `LemmaLower`, `Pos`, `HasMorphology`, `StepWordId`, `StrongsBase`,
  `StrongsFull`, `GermanRowNo`, `IsAddedWord` (added by 0014). Unique index `("Lang", "ElbWordId", "StepWordId") NULLS NOT DISTINCT`.
  Created `WITH NO DATA`; filled by `POST api/dataQuality/refresh` (`DqRefreshCoordinator` → `DqOverviewService` →
  `DqFactsRepository.RefreshViewAsync`).
- Strong's page SQL today: `src/UnshackledWord.Infrastructure/Repositories/StrongsStatsRepository.cs`
  (`GetBookCountsAsync`, `GetKeyCountsAsync`, `GetRenderingsAsync`, `GetMappedCountAsync`, `GetWordBookCountsAsync`,
  `GetWordOriginalsAsync`), services `Application/Features/StrongsStats/StrongsSummaryService.cs` and
  `WordStatsService.cs`, key resolution `StrongsMatchKeys.Resolve`. German key there is
  `LOWER(COALESCE(NULLIF(ew."Lemma", ''), ew."PlainWord"))` on `Elb1871Words` (obsolete column, index
  `IX_Elb1871Words_GermanKey_Book` from 0007). Book counts map STEP `LxxRefId` to Elberfelder `HebRefId` with
  `DISTINCT ON (LxxRefId)` over `Elb1871Verses`; `DqWordFacts` uses the German-side `BibleBookId` for aligned rows and
  `min(HebRefId)` per `LxxRefId` for unmapped source rows.
- `Elb1871Morphology` has no `CREATE TABLE` in `scripts/` and no importer in this repo (only indexes in 0010 and
  readers); it exists in the maintainer's DB. Because `DqWordFacts` joins it, `0012_DataQuality.sql` cannot run on a
  fresh install today. That is why 0008.0 comes first.
- STEP-derived columns stay in the self-hoster's DB only (licence), as for `DqWordFacts`; no snapshot data is ever
  exported to the repo seed.
- Scale: 722k German words, 142k Greek + 306k Hebrew words, 726k mapping rows, ~31k verses; `DqWordFacts` refresh
  estimated 10–30 s (suggestions doc §5).
- Measured on the dev copy (0008.7): a full refresh through `POST api/dataQuality/refresh` (facts, then the four `Stats*`
  views, `CONCURRENTLY`) takes about 38-40 s end to end; the Strong's page showed "wird berechnet …" for that long and
  switched to the new timestamp on the next 5 s poll. Sizes after 0013 / after 0014 (rows, table + indexes):

  | Relation | After 0013 | After 0014 |
  |---|---|---|
  | `StatsStrongsRenderings` | 103,739 rows, 14 MB | 98,465 rows, 13 MB |
  | `StatsStrongs` | 17,003 rows, 5.5 MB | 17,003 rows, 5.5 MB |
  | `StatsGermanLemma` | 19,902 rows, 7.3 MB | 19,902 rows, 7.2 MB |
  | `StatsGermanForms` | 25,753 rows, 3.4 MB | 25,753 rows, 3.3 MB |
  | `DqWordFacts` | – | 752,616 rows, 202 MB |

## Verification (0008.7)

For a fixed list (G2424, G2316, G2532, G3588, H0430, H0853, H3068, H8628, H0310A and the lemmas `gott`, `gehen`, `und`):
snapshot vs live path for occurrences, mapped count, book counts and rendering counts. Expected differences only where
the live path groups by form and the snapshot by lemma + PoS (document per case), the 34 compound Hebrew words (now
counted under their first root), and book counts where versification assigns an aligned German word to a different
book/verse than its source verse (expected: none or a handful).

Result after 0014 (dev copy): mapped counts of H0853, G2424, H8628, H0430, H3068, G2532 equal the live path
(5,697 / 944 / 68 / 2,372 / 6,510 / 9,148); occurrences and verse counts differ by 0-2 (verses without Elberfelder verse,
compounds under the first root); rendering sums differ by 1-12 (lemma + PoS grain merges forms of one source word).

Browser check (0008.7, built-in browser against the rebuilt stack, plain user and a temporary `maintainer` role):
Strong's page G2424 and H8628 show lemma + PoS renderings (`Jesus, Eigenname`, `stoßen, Verb`), the footer
"Statistik: Stand …", and for maintainers "Neu berechnen"; the refresh ran to completion with polling ("wird berechnet …"
then the new timestamp, ~38 s). Word stats: `ging` resolves to "gehen, Verb", a PoS switch appears for `kommen` (Verb /
Nomen). Data-quality header shows both timestamps. Checked in light and dark at 1280 px and 375 px.
Known limits: the concordance filter is keyed by lemma only, so with two PoS rows of one lemma the active highlight
lands on the first row; lemma `Ging` (Verb, 1 occurrence) next to `gehen` is a tagging error visible in the candidates.

## Risks

| Risk | Impact | Mitigation |
|---|---|---|
| 0008.0 not done | `0012` and `0013` fail on fresh installs; stats only on the maintainer's DB | 0008.0 is a hard dependency of 0008.1; fresh-stack check in its Done when |
| Snapshot numbers differ from today's page (lemma + PoS vs form, German-side vs source-side verse) | Users see changed counts, tests built on old numbers break | Verification list in 0008.7; differences documented; live path kept as fallback and for comparison |
| Lemma + PoS grain splits one word into several rows (PoS tagging errors, e.g. `sein` AUX vs VERB) | Renderings lists look fragmented | Page groups visually by lemma with PoS as label; wrong PoS show up in the data-quality check M3 anyway |
| Stats stale because refresh is manual only | Search/Strong's page lag behind edits until someone presses the button | Timestamp on both pages, "Neu berechnen" on the Strong's page for writers (0008.6f); editors read live data |
| Refresh button on a public page | Anonymous users trigger expensive refreshes | Button rendered only for writers; the endpoint keeps `[RequiresLogin]` + admin/maintainer roles and 409 while running |
| Chained views: changing `DqWordFacts` requires `DROP … CASCADE` | A later migration silently drops the stats views | Any script that recreates `DqWordFacts` recreates `Stats*` too; note in both script headers. Done once already: `0014_AddedWordLinks.sql` (new numbered script, 0012/0013 untouched so fresh installs and migrated DBs end up identical; it resets `StatsRefreshedAt` so readers use the live SQL until the next refresh) |
| Added-word rows with a `StepWordId` (assumed absent in the first design) | Snapshot counted added-word links as mapped/renderings (H0853: 6,876 vs live 5,697) | Fixed in 0014 (see "Keys"); verification compares mapped counts of the fixed list. Side effects: U1 (German without original) grows by ~49,600 Hebrew added words, U2/U3 by ~260/~2,370 original words that only anchor added-word links, C1 1 → 8 chapters, M-checks shrink |
| Refresh duration grows (facts + four views) | Longer "wird berechnet" | Measure in 0008.1; concurrent refresh keeps reads working |
| Single-process coordinator | Two API instances could refresh twice; 409 is per instance | Scale stays small (one instance); `pg_try_advisory_lock` is the upgrade path |
| Affix morphemes and second roots not in the snapshot | No per-affix stats | Not needed for page parity; side view `DqSourceMorphemes` designed in "Deferred"; raw strings via `StepWordId` |
| Public endpoints on STEP-derived aggregates | Licence | Aggregates stay in the DB and are served like the existing Strong's page; never exported to the repo |
| jsonb display columns grow (`BookCounts` for 66 books) | Larger rows | Bounded (66 entries, top 10 renderings); measure in 0008.1 |

## Deferred

- **Source morphemes** (only if per-affix or per-second-root stats are wanted; see "Source morphemes (deferred design)").
- Aramaic share and other STEP grammar facets in the snapshot (needs the `Grammar` string in `DqWordFacts`).
- Removing the live fallback SQL from `StrongsStatsRepository` and the obsolete index `IX_Elb1871Words_GermanKey_Book`.
- Fuzzy token matching (`pg_trgm`).

### Source morphemes (deferred design)

Measured on the maintainer's DB copy, 2026-11-12.

- **Source:** `StepHebrewWords."DisambiguatedStrongs"` only (e.g. `H9002/H9009/{H0776G}\H9016`). Split on `/` and `\`
  with ordinality; a token in `{…}` is a root, the rest are prefix/suffix morphemes; the Strong's is
  `substring(tok FROM 'H\d+[A-Za-z]?')`; blank tokens (`/ /`, `//`) are dropped. Glosses per morpheme could come from
  `ExpandedStrongTags` (`H9002=ו=and/{…}`), which uses the same order. Greek needs nothing: every Greek word has exactly one
  Strong's (`StrongsFull`).
- **Size/time:** 540,467 morphemes over 305,652 Hebrew words: 305,675 roots (incl. the second roots of the 34
  compounds) and 234,792 affix tokens over only 49 distinct numbers (top: `H9014` maqaf 42.5k, `H9002` ו 29.7k,
  `H9009` ה 23.9k, `H9016` sof pasuq 23.2k, `H9001`, `H9005`). The split query runs in ~1.6 s; expected
  ~30–40 MB with indexes. Punctuation (`H9014`, `H9016` and similar) should be excluded or flagged.
- **Where:** a separate materialized view `DqSourceMorphemes` (`Lang`, `StepWordId`, `MorphemeNo`, `Strongs`,
  `IsRoot`, unique on the first three) in a new numbered script after `0013`, e.g. `0014_SourceMorphemes.sql`, built from
  the STEP table, not from `DqWordFacts`. Readers join it to `DqWordFacts` / the `Stats*` views on (`Lang`,
  `StepWordId`). This leaves `0012`'s view untouched. The alternative, a `SourceMorphemes text[]` column on
  `DqWordFacts`, would need a new script with `DROP MATERIALIZED VIEW "DqWordFacts" CASCADE` (which drops all `Stats*`
  views), then `CREATE` of `DqWordFacts` and all `Stats*` views with their indexes again, and a manual refresh before
  any page works again; it adds ~20 MB and ~2 s to every refresh. Rejected for that reason.
- **Refresh:** the side view is `WITH NO DATA` like the others and joins the same button pipeline (first non-concurrent,
  then `CONCURRENTLY`). Its content only changes when STEP is re-imported, so the extra ~2 s per refresh is the only cost.
- **Stats on top:** `StatsMorphemeRenderings` (`Strongs` × German lemma × PoS) = `DqSourceMorphemes` joined to aligned
  `DqWordFacts` rows. **Limitation:** the mapping is word-level (a German word is linked to the whole STEP word), so a
  German "und" linked to `וְהָאָרֶץ` counts for ו, ה and אֶרֶץ alike. Affix stats would be approximate; only
  `GermanWordPart` or Mapping v2 groups could attribute a German word to a single morpheme.
- **Verdict:** cheap to build (one script, ~1.6 s, no changes to existing views), but of limited value until mappings are
  morpheme-level, so it stays deferred.

## Open questions for the maintainer

None at the moment. All earlier questions were answered on 2026-11-12 (see "Decisions").
