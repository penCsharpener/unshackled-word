# Roadmap 0009: Smart search overlay in the reader (verse full text + originals per word)

Carved out of `0007-smart-search.md` (now an index). Builds on roadmap 0008 for the per-word lookup and on roadmap 0010
for `Elb1871Morphology` on fresh installs; the verse search itself (0009.1–0009.3f) needs neither and ships first.

## Goal

The Bible reading view gets a search overlay. `F` or a magnifier icon in the menu opens it; while the user types, the
overlay shows two things side by side:

- **Verses:** full text search over `Elb1871Verses`, ranked, with the hit words marked and a link to the verse in the
  reader (`/bible/{bookId}/{chapter}#v{verse}`), plus hits per book as the same `BookBarChart` the Strong's page uses.
- **Originals per word:** every query word is looked up on its own and lists the Greek/Hebrew words it is mapped to,
  by frequency, in the same look as `/bible/strongs/H8628` (`DonutChart` + legend, links to the Strong's page). The data
  comes from the 0008 snapshot through `GET api/elberfelder/search/originals` (0008.5) and `useOriginalsLookup`
  (0008.5f); this roadmap only renders it.

What is wrong with the search that exists today (measured on the maintainer's DB copy, 2026-11-12, read-only):

- `Elb1871Verses."SearchVector"` is `to_tsvector('german', "VerseText")` (0001) with a GIN index (2.5 MB, 31,169
  verses). The `german` configuration drops stop words, so `ich bin` becomes an empty query (0 hits, PostgreSQL
  notice), `daß` is a stop word as well, and "Ich bin der Weg" can only be found by `Weg`.
- Snowball stems do not join strong verb forms: `sprechen` hits 189 verses, `sprach` 3,967. Only the lemmas in
  `Elb1871Morphology` know that both are `sprechen`.
- The two existing endpoints `GET api/elberfelder/search/german` and `GET api/elberfelder/fts/single` are early
  experiments: inline SQL in the endpoint, Greek only, `ILIKE '%…%'` on `Elb1871Words`, no ranking, no frontend
  caller (only `api/unshackled-word/Elberfelder/Search/Keyword.bru`).

What is already good: the GIN plan is fast (`Gott Liebe`, ranked, with `ts_headline`, top 20: 2.4 ms), the verse text
has no markup (0 verses with `<>{}[]`), `HebRefId` is `BBCCCVVV` and equals book/chapter/verse for every word (0
mismatches), and the reader already scrolls to `#v{verse}`.

Out of scope: the snapshot, its refresh and the lookup endpoint (0008), the `Elb1871Morphology` table and seed (0010),
fuzzy matching (`pg_trgm`), search over the Greek/Hebrew text, a separate search page with shareable URL, MCP search
tools.

## Decisions

Only rules stated by the maintainer (in 0007, 0008 or CLAUDE.md). Everything else is under "Open questions" with a
recommendation, and the tasks follow the recommendation until it is answered.

| Topic | Decision | Source |
|---|---|---|
| Entry points | `F` key or a magnifier icon in the menu opens the search as an overlay over the default Bible reading view | 0007 |
| Search start | After a space or a typing pause | 0007 |
| Verse search | PostgreSQL full text search over `Elb1871Verses` | 0007 |
| Per-word lookup | Each query word is also looked up on its own: mapped Greek/Hebrew originals by frequency, same charts and look as `/bible/strongs/H8628` | 0007 |
| Lookup source | The 0008 snapshot via `GET api/elberfelder/search/originals` (0008.5) and `useOriginalsLookup` (0008.5f); not redesigned here | 0007, 0008 |
| Frequent words | Lemmas with `Occurrences >= 2000` come back flagged without originals unless `includeFrequent=true` (0008.5 contract) | 0008 decisions |
| Fuzzy search | No `pg_trgm` now | 0008 decisions, task brief |
| Access | Read-only study tools are public; the search endpoints are anonymous and read-only | CLAUDE.md |
| Magnifier and `F` | On every `/bible…` route (reader, book picker, Strong's page) | maintainer, 2026-11-13 |
| Result order | Relevance (literal hits first, then lemma hits; ties in Bible order) | maintainer, 2026-11-13 |
| Typing pause | Search after 300 ms without a keystroke (or at once on space/Enter), not below 2 characters | maintainer, 2026-11-13 |
| Legacy endpoints | `GET api/elberfelder/search/german` and `GET api/elberfelder/fts/single` are deleted | maintainer, 2026-11-13 |
| New components | `SearchOverlay`, `VerseHitList` and the `Dialog` / `TextField` prop additions stay **proposed** until explicitly approved | maintainer, 2026-11-13 |
| UI | German and English via i18n, light/dark, mobile to 4k, design tokens only, new components proposed in chat first and registered in `Components.md`, no new dependency without asking | CLAUDE.md |

## How to use this roadmap

Work in table order or as *Depends on* allows; one task = one commit. A slice is the backend row plus its `f` row of the
same number; both land before the slice counts as done, and every slice leaves `dotnet build UnshackledWord.slnx`,
`dotnet test` and `docker compose run --rm --no-deps unshackledword.web bun run check` green. Conventions of 0004
("Conventions for all steps") apply: Dapper only, `*Sql` constants in repo SQL style, FakeDbReader tests, design
tokens, i18n de/en, `Components.md`, Bruno files updated in the slice that changes a contract. DB experiments on a
scratch copy only (`postgres-test-db` or a throwaway container), never on the shared DB copy.

**Components:** rows that need a new component name it as **proposed**. Before such a row starts, the proposal (name,
props, why no existing component or prop fits) is put to the maintainer in chat; only after a yes is it built and added
to `Components.md`. If the answer is "extend X instead", the row is done with the extension.

## Tasks

| ✔ | # | Task | Depends on | Done when |
|---|---|---|---|---|
| [x] | 0009.1 | Migration `scripts/0015_VerseSearch.sql` (0013/0014 are taken by 0008): text search dictionary `"unshackled-word".uw_german_stem` (`TEMPLATE = snowball, Language = german`, **no** `StopWords`) and configuration `"unshackled-word".uw_german` (`COPY = german`, mappings `asciiword, word, hword, hword_part, hword_asciipart, asciihword` → `uw_german_stem`), both guarded with `IF NOT EXISTS` checks in a `DO` block; drop index `Elb1871Verses_Search_idx`, `ALTER TABLE "Elb1871Verses" DROP COLUMN "SearchVector"`, re-add it as `tsvector GENERATED ALWAYS AS (to_tsvector('"unshackled-word".uw_german', "VerseText")) STORED`, recreate the GIN index | – | Fresh scratch DB: script runs after all earlier scripts and twice in a row; on a scratch copy `websearch_to_tsquery('"unshackled-word".uw_german', 'ich bin')` hits > 0 verses, `dass` and `daß` hit the same verses, `Füße`/`fuesse` the same; the legacy endpoints `search/german` and `fts/single` (they query with `'german'`, same stems) still return rows for `Gott`; rebuild time and index size noted in "Background" |
| [x] | 0009.2 | Endpoint `GET api/elberfelder/search/verses?q=&page=&pageSize=&bookId=` (feature slice `Endpoints/Elberfelder/SearchVerses` + `Application/Features/VerseSearch`): validator (`q` 1–200 chars after trim, at most 8 tokens, `pageSize` 10–50, default 20, `bookId` 1–66 or empty); `VerseSearchQueryBuilder` turns the input into a `tsquery` text (tokens AND-ed, `"…"` phrase → `<->`, leading `-` → `!`, `oder`/`or` → `|`, lexemes from `ts_lexize('"unshackled-word".uw_german_stem', …)` so user text never reaches `to_tsquery` raw); SQL ranks with `ts_rank_cd("SearchVector", q)` descending, ties by `"HebRefId"`; response `{ query, total, page, pageSize, hits: [{ bibleBookId, chapter, verse, tokens: [{ text, matched }] }] }`, tokens from `Elb1871Words."WordInContext"` of the page's verses, `matched` = the word's stem (`to_tsvector(uw_german, "PlainWord")`) is one of the query lexemes; anonymous, `Cache-Control: public, max-age=300` | 0009.1 | Builder tests (plain words, phrase, exclusion, `oder`, quotes left open, only punctuation → validation error, SQL injection strings end up as lexemes); repository SQL test with FakeDbReader; scratch copy: `Gott Welt geliebt` returns Joh 3:16 on page 1 with exactly those three words `matched`, `ich bin` > 0 hits; Bruno `SearchVerses.bru`; p95 < 50 ms for `Gott`, `und`, `ich bin der Weg` |
| [x] | 0009.2f | Search overlay MVP. Typed client + hook `useVerseSearch(q, page, bookId)` (TanStack Query, request aborted when the query changes, previous result kept while the next loads). Magnifier icon button (`Button` ghost, `aria-label` "Suche (F)") in the `AppShell` header on all `/bible…` routes; `F` (no modifier, not in an input/textarea/contenteditable, not inside `[data-mapping-editor]`, not while the word popover or another dialog is open, same guard as the arrow keys in `ChapterPage`) opens it. Overlay = `Dialog` (full screen below 48rem) with **proposed** props `placement="top"` (palette position from md) and `titleHidden` (title stays as accessible name); input = `TextField` with **proposed** `type="search"` support and a leading icon; search runs on space, Enter or 300 ms after the last keystroke, not below 2 characters; result list = **proposed** component `VerseHitList` (reference link in the reader language via `bookName`, verse text with `matched` tokens in `<mark>` styled by tokens, "Mehr laden" paging, empty and error states with `EmptyState`); clicking a hit navigates to `/bible/{b}/{c}#v{v}` and closes the overlay; the last query is kept in memory while the tab lives | 0009.2 | Vitest: `F` opens, `F` inside an input does not, Esc closes and returns focus to the magnifier, space triggers a search without waiting, typing pause of 300 ms triggers one, a hit click navigates and closes; i18n de/en keys (`search.*`); `Components.md` updated after the proposals are accepted; manual check at 375 px and 1440 px in light and dark |
| [x] | 0009.3 | `search/verses` returns `bookCounts: [{ bookId, count }]` for the whole query (one `GROUP BY "HebRefId" / 1000000` over the matching rows, independent of `bookId` filter and paging) | 0009.2 | Service test: counts sum to `total` without filter; with `bookId` the counts stay the same and `total` equals that book's count; Bruno file updated |
| [x] | 0009.3f | Overlay shows hits per book with `BookBarChart` (same component and colours as the Strong's page); clicking a bar sets the book filter, clicking again clears it; filter shown as a removable chip above the list | 0009.3 | Vitest: bar click filters, second click clears, chip removes filter; the chart is the list view on narrow containers as on the Strong's page |
| [x] | 0009.4 | Lemma-aware matching. Migration `scripts/0016_VerseLemmaText.sql`: column `Elb1871Verses."LemmaText" text NULL` (lower-cased lemmas of the verse in word order, from `Elb1871Morphology`), function `"unshackled-word"."RefreshVerseLemmaText"(int[])` (all verses when `NULL`), and the generated `SearchVector` recreated as `setweight(to_tsvector(uw_german, "VerseText"), 'A') \|\| setweight(to_tsvector('simple', coalesce("LemmaText", '')), 'B')`. Callers: the morphology seed runner (0010.4) after import, `LemmaRepository` / `LemmaBulkRepository` (single save, chapter save, bulk apply and revert) for the touched `HebRefId`s in the same transaction. Query builder: each token becomes `(stem \| lemma1:B \| lemma2:B …)`, lemmas from the forms → lemma resolution of 0008.4 (`StatsGermanForms`); while the snapshot is unpopulated the token is stem-only. `matched` also marks words whose lemma is one of the token's lemmas | 0008.4, 0010.4 | Scratch copy: `sprechen` hits ≥ the 3,967 `sprach` verses plus the 189 `sprechen` verses (union), ranked with the literal-form verses first; a lemma edit of one word changes that verse's `LemmaText` in the same request (repository test + manual); a fresh compose stack has `LemmaText` filled after seeding; tests for the builder with lemmas and with an empty snapshot |
| [x] | 0009.4f | Above the hits, one line per token that was widened by a lemma ("ging → auch Formen von *gehen*"); lemma-matched words marked like stem matches | 0009.4 | Vitest with a fixture containing a lemma-widened token; i18n de/en |
| [x] | 0009.5f | Originals per word, from `useOriginalsLookup(q)` (0008.5f) with the same debounce as the verse search. One block per token: heading "Wort → Lemma (Wortart)", a `DonutChart` of the originals by count (slice label = original lemma in `OriginalText`-style font, `text` = Strong's + gloss, more than 8 fold into "Other" as on the Strong's page), legend rows link to `/bible/strongs/{strongs}` and close the overlay; tokens with `highFrequency: true` show "Sehr häufiges Wort – Grundtext ausgeblendet" with a `Button` secondary "Trotzdem anzeigen" that refetches that token with `includeFrequent=true`; unknown tokens show nothing; snapshot never refreshed → one `EmptyState` "Statistik noch nicht berechnet". Layout: two columns (verses left, originals right) from 64rem, tabs "Verse" / "Grundtext" below (tab buttons are `Button` `secondary` with `aria-pressed`) | 0009.2f, 0008.5f | Vitest against the 0008.5 contract fixture: donut per token, high-frequency token with the reveal button, unknown token, unpopulated snapshot, Strong's link navigates and closes; tabs below 64rem; i18n de/en; manual check that the donut looks like the one on `/bible/strongs/H8628` |
| [x] | 0009.6f | Keyboard and accessibility pass: `ArrowDown`/`ArrowUp` from the input move through the hits (`aria-activedescendant`, list is a `listbox` of links), `Enter` opens the active hit, `Tab` reaches the originals column; a polite live region announces "123 Verse gefunden" after each result; focus never leaves the overlay; on touch devices (`useCoarsePointer`) no autofocus jump after the first render, `enterkeyhint="search"` on the input | 0009.5f | Vitest for arrow navigation, Enter, live region text; axe-style checks in the existing test setup show no unlabeled control; manual check with the on-screen keyboard at 375 px |
| [x] | 0009.7 | Clean-up and docs: delete the two legacy endpoints (`Endpoints/Elberfelder/Search/German`, `Endpoints/Elberfelder/Fts/GetElbWordOccurrence`) and `api/unshackled-word/Elberfelder/Search/Keyword.bru`; drop them from 0005.5's reader list when 0005 is next edited; README section "Search" (FTS config, what is ranked how, that lemma widening needs the 0008 snapshot) | 0009.6f | `rg -n "search/german\|fts/single" src api` returns nothing; `dotnet test` and `bun run check` green; README section present |

Ordering: 0009.1 → 0009.2 → 0009.2f is the hard start and needs neither 0008 nor 0010. 0009.3 is independent of
0009.4/0009.5f. 0009.4 waits for 0008.4 and 0010.4; 0009.5f waits for 0008.5f.

## Design

### Full text search configuration

- **`uw_german` = `german` without stop words.** A Bible search must find "Ich bin", "der Herr" and "daß"; the 2.5 MB
  GIN index on 31k verses does not care about the extra lexemes. The snowball dictionary is created in SQL (no files in
  the Postgres share directory, so the stock `postgres:18` image keeps working). Tested in a rolled-back transaction:
  `ich bin der Weg` → `'ich' & 'bin' & 'der' & 'weg'`; `daß` and `dass` both → `dass`; `Füße` → `fuss`, `Häuser` →
  `haus`.
- **Generated stored column + GIN** stays the model (as today and as `StepBibleVerses` in 0005): no trigger, no refresh,
  rebuilt by the migration. Alternatives considered: an expression index on `to_tsvector(...)` (every query must repeat
  the expression exactly, no weights from another column), a materialized view (needs a refresh trigger nobody presses),
  `pg_trgm` (not wanted now).
- **Weights:** `A` = stems of the surface text, `B` = lemmas (0009.4). `ts_rank_cd` with default weights ranks literal
  matches above lemma-only matches; ties go to Bible order (`HebRefId`).
- **Query building in C#** (`VerseSearchQueryBuilder`) instead of `websearch_to_tsquery`, because each token needs its
  lemma alternatives and the result must say which lexemes were used for marking. User text is never concatenated into
  SQL or `to_tsquery`; tokens go through `ts_lexize` and are quoted as lexemes.
- **Highlighting via tokens, not `ts_headline`.** `ts_headline` would return HTML-ish text and cannot mark lemma
  matches (`ging` for lemma `gehen`). The hit list returns the verse as word tokens with `matched`, the same shape as
  `StrongsOccurrenceToken`, so the frontend renders `<mark>` without `dangerouslySetInnerHTML`.
- **Archaic spelling** (`daß`, `Thür`): the export text already uses `Tür`/`tun` in most places (`thun` 0 hits, `tun`
  732); `ß` is folded by the stemmer. A spelling map for further variants is open question 1.

### Endpoint shapes

```
GET api/elberfelder/search/verses?q=Gott%20Welt%20geliebt&page=1&pageSize=20&bookId=     (values illustrative)
→ { "query": "Gott Welt geliebt", "total": 9, "page": 1, "pageSize": 20,
    "tokens": [{ "text": "Gott", "lexemes": ["gott"] }, { "text": "Welt", "lexemes": ["welt"] },
               { "text": "geliebt", "lexemes": ["geliebt"], "lemmas": ["lieben"] }],
    "bookCounts": [{ "bookId": 43, "count": 17 }, …],
    "hits": [{ "bibleBookId": 43, "chapter": 3, "verse": 16,
               "tokens": [{ "text": "Denn", "matched": false }, …, { "text": "Gott", "matched": true }, …] }] }

GET api/elberfelder/search/originals?q=…            (0008.5, unchanged; consumed by 0009.5f)
```

Two calls instead of one combined endpoint: they have different caches (verse text never changes, the snapshot
changes on "Neu berechnen"), different latency, and 0008.5 already exists as its own slice.

### Overlay UX

- **Open:** `F` or the magnifier; the magnifier sits in the header next to the language switch on every `/bible…`
  route (the reader, the book picker, the Strong's page). The hint "F" is in its tooltip and `aria-keyshortcuts="f"`.
  `Ctrl+F` stays the browser's.
- **Typing:** search on space or Enter immediately, otherwise after 300 ms without a keystroke; requests in flight are
  aborted; the old result stays visible (dimmed) until the new one arrives.
- **Desktop (≥ 64rem):** palette at the top of the screen (`Dialog` `placement="top"`, size `lg`), input on top, verses
  left (with the book chart folded above the list), originals right.
- **Tablet/phone:** `Dialog` full screen (existing behaviour below 48rem), input sticky at the top, tabs "Verse" /
  "Grundtext", book chart in list view.
- **Leaving:** a hit or a Strong's link navigates and closes; Esc / backdrop / close button close and return focus.
- **i18n:** all UI text from `search.*` keys in `de.ts` / `en.ts`; book names through `bookName(book, language)`;
  glosses are the lexicon's English glosses (as on the Strong's page).

### Components

| Need | Plan | Status |
|---|---|---|
| Overlay frame | `Dialog` + props `placement` (`center` default / `top`) and `titleHidden` | proposed prop |
| Search input | `TextField` + `type="search"` and optional leading icon | proposed prop |
| Magnifier button | `Button` ghost with inline SVG (like `ThemeToggle`) inside `AppShell` | no new component |
| Verse hit list | `VerseHitList`: props `hits`, `books`, `language`, `activeIndex`, `onSelect(hit)`, `onLoadMore`, `hasMore`, `loading` | proposed component |
| Search panel | `SearchOverlay`: owns query state, debounce, hotkey-independent layout, renders `Dialog`, `TextField`, `VerseHitList`, `BookBarChart`, `DonutChart`; opened from `AppShell` | proposed component |
| Hotkey | `lib/search/useSearchHotkey.ts` (hook, not a component) | – |
| Charts | `BookBarChart`, `DonutChart`, `EmptyState` unchanged | existing |

## Background

- Schema: `Elb1871Verses` (`0001_InitialMigration.sql`): `Id`, `HebRefId` (unique), `LxxRefId`, `VerseText`,
  `SearchVector` generated with `'german'`, GIN `Elb1871Verses_Search_idx`. 31,169 verses, table 16 MB with indexes.
- Words: `Elb1871Words` (`HebRefId`, `PositionInVerse`, `WordInContext`, `PlainWord`), 722,753 rows in the maintainer's
  DB; lemmas only in `Elb1871Morphology` (`Elb1871Words.Lemma` is obsolete, see 0008).
- Hit counts on the copy: `Gott` 3,711 verses; `sprach` 3,967; `sprechen` 189; `Jahwe` 5,799; `ich bin` 0 (stop words).
- Existing code to reuse: `BibleReferenceParser` (Domain) for a possible reference jump; `StrongsOccurrenceToken`
  shape and `KwicRow` styling on `StrongsPage`; `ChapterPage` arrow-key guard (`isTypingTarget`, mapping editor,
  open popover); `useCoarsePointer`.
- Auth: every endpoint is anonymous unless it has `[RequiresLogin]` (`AuthSupport.cs`), so the new endpoints need no
  attribute; they must not get one.

### Implementation status and measurements (backend, 2026-11-13)

Backend of 0009.1, 0009.2, 0009.3 and 0009.4 is implemented (migrations 0015 and 0016 applied to the dev DB, endpoint live,
legacy endpoints untouched until 0009.7). Measured on the maintainer's dev DB (31,169 verses, Postgres 18.6), HTTP round trip
on localhost, 40 requests per query, after warm-up:

| What | Result |
|---|---|
| 0015 on the dev DB (rebuild of `SearchVector`, GIN) | about 1.0 s; both scripts run twice in a row without error (a rolled-back trial of 0015+0016 twice took 2.9 s) |
| 0016 on the dev DB (LemmaText for 31,166 verses, vector rebuild, GIN) | about 2.8 s |
| GIN index `Elb1871Verses_Search_idx` | 2.5 MB before, 2.7 MB with `uw_german` stems only (no stop words), 4.6 MB with the lemma part (weight B) |
| `Elb1871Verses` table | 16 MB total before, 30 MB total after 0016 (`LemmaText` about 4 MB, the vector doubles) |
| Hit counts | `ich bin` 820 verses (was 0), `ich bin der Weg` 125 with John 14:6 first, `dass` = `daß` = 3,771, `Füße` = `fuesse` = 361, `Gott` 3,711 (same as `german`) |
| Lemma widening | `sprechen` 5,201 verses (189 literal `sprech` stems first, positions 1-189, then 5,012 lemma-only), `sprach` + lemma 5,280, `ging` 1,384, `Liebe` 304 |
| Latency, median / p95 (ms) | `Gott` 15 / 23, `ich bin der Weg` 12 / 14, `ging` 12 / 13, `gehen` 10 / 12, `"und es geschah"` 8 / 10, `Herr oder Jahwe` 33 / 35, `sein` 31 / 35, `der` 43 / 47, `und` 46 / 51 |
| `LemmaText` refresh | `RefreshVerseLemmaText(ARRAY[43003016])` after a lemma change makes `'newlemma':B` match that verse in the same transaction; a second call changes 0 rows; 0 mismatches between stored `LemmaText` and a fresh aggregate |

`und` and `der` match about 24,000 of 31,000 verses; the planner does a sequential scan and `ts_rank_cd` over all matches, so these
single function-word queries sit at the 50 ms target (p95 51 ms). The two heavy statements (per-book counts, ranked page) run
concurrently; without that `und` was 78 ms p95.

Deviations from the plan above: the response also carries `tokens` (query words with `lexemes` and `lemmas`, for 0009.4f);
lemma widening is limited to the 3 most frequent lemmas per single word and never applies to phrases or excluded words;
an empty `?bookId=` arrives as 0 and means "no filter". `RefreshVerseLemmaText` is plpgsql so 0016 also runs on a fresh install
without `Elb1871Morphology`; it fails (table missing) only if called before the table exists. The morphology seed (0010.4) still has to call
`RefreshVerseLemmaText(NULL)` once after its import; the lemma editor's single save, chapter save, bulk apply and bulk revert call it already.

## Risks

| Risk | Impact | Mitigation |
|---|---|---|
| Dropping and re-adding `SearchVector` in 0009.1 and 0009.4 | Short lock on `Elb1871Verses` during the migration; queries that name the column fail if the script dies halfway | One transaction per script; 31k rows rebuild in seconds (measure in 0009.1); legacy endpoints keep working because `german` and `uw_german` share the stemmer |
| No stop words | `und` matches almost every verse; one-word queries on function words give huge result sets | Ranking + paging, count is cheap on GIN; the originals side already hides frequent words (0008 threshold) |
| `LemmaText` is a copy of `Elb1871Morphology` | Search drifts from the lemma editor if a write path forgets the refresh call | One SQL function called from every write path and the seed; repository tests per path; a data-quality check comparing `LemmaText` with a fresh aggregate is **not done** (a new check code needs list rows, facets, labels de/en and tests across `DqChecks`, `DqCheckRepository` and the check page; not small). Manual probe: `SELECT count(*) FROM "unshackled-word"."Elb1871Verses" v WHERE v."LemmaText" IS DISTINCT FROM (SELECT string_agg(lower(m."Lemma"), ' ' ORDER BY m."PositionInVerse") FROM "unshackled-word"."Elb1871Morphology" m WHERE m."HebRefId" = v."HebRefId")`; fix with `SELECT "unshackled-word"."RefreshVerseLemmaText"(NULL)` |
| Lemma widening depends on the 0008 snapshot | Before the first "Neu berechnen" the search is stem-only; after lemma edits it lags until the next refresh | Stated in the README and the overlay line "auch Formen von …" only shows when widening happened; editors see live data |
| Lemma widening teaches false equivalences when lemmas are wrong (e.g. `zu` tagged as verb particle 10,922 times, see 0010) | A reader sees verses for the wrong word marked as hits | Literal matches rank first (weight A); the widening line names the lemma so it is visible; lemma fixes flow through the editor |
| Global `F` hotkey | Collides with typing in other inputs or future shortcuts | Same guard as the reader's arrow keys, tests for inputs/contenteditable/dialogs |
| Two independent requests per keystroke pause | Load on a small host | Debounce, abort, `Cache-Control` on both, server-side limits (8 tokens, 200 chars) |
| New components before agreement | Rework if the maintainer prefers another shape | Proposed rows wait for the chat answer (see "How to use") |

## Deferred

- Search page with shareable URL (`/bible/search?q=`) and "search in this chapter/book" from the reader.
- Filtering verse hits by a chosen original (click a donut slice → only verses where the token is mapped to that
  Strong's); needs a join to the mapping facts per hit page.
- Fuzzy and prefix matching (`pg_trgm`, `:*` prefix queries), spelling suggestions.
- Search over the Greek/Hebrew text (`StepBibleVerses."SearchVector"` exists) and English glosses.
- MCP search tool (policy undecided).

## Open questions for the maintainer

Answered on 2026-11-13 (see "Decisions"): magnifier on all `/bible…` routes, relevance order, 300 ms / 2 characters,
legacy endpoints deleted. Still open:

1. **Archaic spelling:** should a small variant map (`Thür` ↔ `Tür`, `thun` ↔ `tun`, `Hülfe` ↔ `Hilfe`) widen queries?
   Recommendation: not now (the export already uses modern forms in most places); revisit with the modernized revision.
2. **Bible references in the search box** (`Joh 3,16`): jump straight to the verse? Recommendation: yes, as a small
   later row using `BibleReferenceParser` with German book names; not in this roadmap's table.
3. **Lemma widening on by default** (recommended) or behind a switch "auch andere Formen"?
4. **Component approval:** `SearchOverlay`, `VerseHitList`, `Dialog` `placement`/`titleHidden`, `TextField`
   `type="search"` + icon (see "Components"); 0009.2f cannot start before this.
