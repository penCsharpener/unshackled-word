# Data quality page – suggested filters, statistics and checks

Status: suggestion, nothing implemented. Logged-in page of `src/UnshackledWord.Web` (route proposal `edit/quality`, role
`maintainer`/`reviewer`). Goal: find wrong Elb1871 ↔ Greek/Hebrew mappings and wrong German lemma/morphology data, and
jump from every finding straight to the editor that fixes it.

Sources read: migrations `0001`–`0011`, `docs/drafts/mapping-v2-schema.sql`, `docs/roadmaps/0005-mapping-redesign.md`,
`docs/drafts/strongs-stats-*.md`, `docs/drafts/lemma-*.md` (numbers below come from those docs, measured on the dev DB
2026-11-10/12), `Endpoints/Lemmas`, `Endpoints/Elberfelder`, `ElbLemmaValueMaps.cs`, `router.tsx`, `ChapterPage.tsx`,
`Components.md`.

## 1. Facts that shape the design

| Fact | Consequence |
|---|---|
| Mapping today = legacy edge tables `Elb1871GreekMapping` (170k) / `Elb1871HebrewMapping` (556k), one row per (`ElbWordId`, `StepWordId`); `StepWordId NULL` + `IsAddedWord` = added German word. v2 (`MappingLinks` …) is a draft (0005). | Build every check on **one SQL fact source** (section 2.1) so the switch to v2 changes one definition, not 25 queries. |
| German lemma/PoS/features live in `Elb1871Morphology` (join on `HebRefId` + `PositionInVerse`). `Elb1871Words.Lemma` is obsolete (maintainer: lemma, PoS and morphology per word come only from `Elb1871Morphology`). | Never read `Elb1871Words.Lemma`. Every German-side query joins `Elb1871Morphology` on `HebRefId` + `PositionInVerse`. Existing `GetStrongsSummary`/`GetWordStats` and `IX_Elb1871Words_GermanKey_Book` still use the obsolete column and should be moved to the join later. |
| `Elb1871Morphology` is not created by any migration script in `scripts/`. | Check that a unique index on (`HebRefId`, `PositionInVerse`) exists; add it in a new numbered script if not (all checks join on it). |
| Greek Strong's: `StepGreekWords.DisambiguatedStrongs` (`G2424G`), class = `split_part("Morphology",'-',1)`. Hebrew: all morphemes in one string, main word in braces (`H9005/{H0935P}`), class = `Grammar` segment at the braced index. `Elb1871GreekMapping.StrongsNumber` is stale in 3,113 rows. | Group by **base number** (`G\d+`, braced `H\d+`); never read `StrongsNumber` from the mapping table. Hebrew prefixes/suffixes make German function words map to the host word → OT function words must be excluded from PoS/minority flags. |
| Legacy mapping has no author, AI flag, confidence or history; `CreatedAt` is the batch day (3 Greek / 5 Hebrew days), a verse save rewrites it with `now()`. | "AI-only / low confidence" is only possible after v2 (`MappingLinkTrust`). "Recently edited mapping" today = `CreatedAt` not on a seed day. |
| Known baselines: unmapped source words 13,579 Greek (8,920 articles; 2,549 not in NA, 1,698 not in TR) and 11,378 Hebrew (4,070 object markers `HTo`). NT German words added-only 19 %, OT 1.5 %; 336 OT German words have no mapping row. Content words ≈ 99 % aligned. 3,634 legacy groups ≥ 6 tokens (max 46). | Raw "unmapped" counts are dominated by legitimate cases → every unmapped chart needs a **class split** (content vs function, edition, article/object marker), otherwise the real errors drown. |
| Lemma edits are audited (`Elb1871MorphologyEdits` with `OriginalKey`, `Elb1871MorphologyChangeSets`). | "Recent edits" and "edits that disagree with the original" are cheap. |
| Deep links: `/bible/:book/:chapter#v{verse}` scrolls to the verse; the mapping editor opens only by click. `/edit/lemmas/:book/:chapter` has no word/verse selection param. Bulk replace is URL-driven (`lib/lemmas/bulk.ts`, `useBulkUrl`). `/bible/strongs/:strongs?book&rendering` exists. | Small frontend work needed for the jump actions (section 2.3). |

## 2. Shared building blocks

### 2.1 One fact source: materialized view `DqWordFacts`

One row per (German word × aligned source word), plus one row per German word without a source word, plus one row per
source word without a German word. Built from the legacy tables now, from `MappingLinks` + compat logic after 0005.

| Column group | Columns |
|---|---|
| German | `ElbWordId`, `HebRefId`, `BibleBookId`, `Chapter`, `Verse`, `PositionInVerse`, `PlainLower` (`lower(PlainWord)`), `Lemma`, `LemmaLower`, `Pos`, `VerbForm`, `Number`, `Case`, `Gender`, `HasMorphology` |
| Link | `Lang` (`G`/`H`), `Kind` (`aligned` / `added` / `none` / `unmappedSource`), `GermanPerSource` and `SourcePerGerman` (window counts, for n:m checks), `ParentGermanWordId`, `GermanWordPart` |
| Source | `StepWordId`, `SourceRefId` (`LxxRefId`), `SourcePos`, `SourceWord` (Greek / Hebrew), `StrongsBase`, `StrongsFull`, `OrigClass` (N V A T P R C D …), `ProperNoun` (Greek `-P`/`-L`/`-T` suffix, Hebrew `Np`), `InNa`, `InTr` (Greek), `HebType` (Ketiv/Qere) |

- Size ≈ 0.75–0.8 M rows, ~100–150 MB with indexes. Build ≈ 10–30 s (the 0005 full-copy migration of the same joins
  took 3.5 min with much more work).
- Indexes: `(LemmaLower)`, `(PlainLower)`, `(Lang, StrongsBase)`, `(BibleBookId, Chapter)`, partial `WHERE "Kind" <> 'aligned'`,
  unique `(ElbWordId, StepWordId) NULLS NOT DISTINCT` (needed for `REFRESH … CONCURRENTLY`).
- Refresh: button "Daten neu berechnen" + timestamp on the page, and automatically after a bulk lemma apply/revert
  (async, fire-and-forget). Findings can be minutes stale; the editors you jump to always read live data. A finding that
  no longer reproduces is shown as "bereits behoben?" after refresh.
- The STEP-derived columns stay in the self-hoster's DB only (licence, same as `SourceWords.MatchText`).
- Alternative without a MV: run each check live (single aggregate over 722k words: ~0.3–2 s each) behind
  `Cache-Control`/in-process cache. Works for the overview at this scale, but the MV makes ad-hoc filters ~10× faster and
  isolates the v2 switch. Recommended: MV.

### 2.2 One finding shape and one list component

Every check endpoint returns the same row shape, so the page needs one table, one pager and one jump menu:

```ts
interface Finding {
  check: string;                 // "M3", "L2", …
  severity: "error" | "warning" | "info";
  bibleBookId: number; chapter: number; verse: number;
  elbWordId?: number; stepWordId?: number; lang?: "G" | "H";
  german?: string; lemma?: string; pos?: string;
  original?: string; strongs?: string; gloss?: string;
  detail: string;                // short reason, e.g. "Wortart VERB ↔ P (μου)"
  groupKey?: string;             // for aggregated checks: the lemma/strongs the row belongs to
}
```

Aggregated checks (lemma → many Strong's, coverage …) return groups first (`{ key, count, share, flags }`), and a group
expands into `Finding` rows. Paging server-side (`page`, `pageSize`), filters shared by all checks: testament, book range,
German PoS, `Lang`, minimum count.

Dismissals ("ist korrekt, nicht mehr melden"), tier *should*: table `DataQualityDismissals` (`Check`, `ItemKey` text
e.g. `M1:herr:H0136`, `UserId`, `Note`, `CreatedAt`); checks `LEFT JOIN … WHERE d IS NULL`. Without it the synonym-heavy
checks (M1, M2) keep re-flagging the same correct cases.

### 2.3 Jump actions

| Code | Target | Work needed |
|---|---|---|
| **J-M** mapping editor | `/bible/{book}/{chapter}?map={verse}#v{verse}`: scroll to verse, open the align view, highlight `elbWordId`/`stepWordId` (`&w=`, `&s=`) | `ChapterPage` reads `map`/`w`/`s` (small) |
| **J-L** lemma editor | `/edit/lemmas/{book}/{chapter}?word={elbWordId}`: select the cell | `LemmaEditorPage` reads `word` (small) |
| **J-B** bulk replace | `/edit/lemmas?…` bulk URL preset with `from` = lemma (+ PoS / original group) | existing URL state, just build the link |
| **J-S** Strong's page | `/bible/strongs/{strongs}?rendering={key}&book=` | exists |
| **J-F** re-filter | same page, new Strong's / lemma filter | page-internal |

Open in a new tab by default (keeps the finding list and its scroll/paging state).

### 2.4 Original-word filter with transliteration (required)

- Load `StepStrongsLexicon` (~15k rows) once into an in-process index in the API (no DB index, no new dependency):
  per entry `Number`, `Extra`, `OriginalWordNoDiacritics`, `Transliteration` folded, consonant skeleton, gloss words.
- Fold = lower case → `Normalize(FormD)` → drop `NonSpacingMark` → drop `.`, `-`, `ʼ`, `ʽ`, `'` → `ς→σ`.
  Equivalences on Latin input: `ph=f`, `kh=ch=x`, `th`, `sh=š`, `ts=tz=ṣ`, `w=v`.
- Matching order (ranked, top 20, with occurrence count from `DqWordFacts`): exact Strong's (`G1577`, `1577`, `H430`
  → `H0430`, all `Extra` variants) → Greek/Hebrew script without diacritics → folded transliteration prefix → **Hebrew
  consonant skeleton** (strip vowels from input and transliteration: `dbr` and `dabar` both find דָּבָר) → English gloss word.
- Result per Strong's: lemma, transliteration, gloss, occurrences, mapped / added / unmapped counts, and the German
  renderings **by `Elb1871Morphology.Lemma`** (+ PoS), ordered by frequency, each with share, flag chips (section 3) and
  a click = J-F into the German filter. Each rendering expands to its verses (J-M / J-L).

### 2.5 German filter (required)

Input: lemma (prefix, case-insensitive, uses `Elb1871Morphology_LemmaLower_idx` / MV index), or surface form
(`PlainLower`), plus PoS select. Result: per (lemma, PoS) the Strong's numbers behind it, split by `Lang`, ordered by
frequency, with share, original PoS class, gloss, minority / PoS-conflict flags; `added` and `none` as own rows. Click
on a Strong's = J-F into the original filter. Side panel: the surface forms of the lemma (with counts) and the other
lemmas that the same surface forms carry (feeds L3).

## 3. Checks

Columns: anomaly revealed; SQL idea (on `DqWordFacts` = `f` unless noted); chart; jump. "Content PoS" = NOUN, PROPN,
VERB, ADJ, ADV(-ish). "OT-FW exclusion" = skip when `Lang = 'H'` and German PoS is DET/ADP/CCONJ/SCONJ/PRON/AUX/PART.

### Tier 1 – must (required by the brief, or cheap with high yield)

| # | Check | Reveals | SQL idea | Chart | Jump |
|---|---|---|---|---|---|
| U1 | **Unmapped German words** (required) | German words without source word: AI gaps (content words) vs legitimate additions (articles, auxiliaries) | `Kind IN ('added','none')` grouped by book, PoS, `LemmaLower`; severity `error` for content PoS, `info` otherwise; `none` (no row at all, 336 OT words) always `error` | `BookBarChart` (stacked content/function), `DonutChart` by PoS, ranked list of top lemmas | J-M |
| U2 | **Unmapped Greek words** (required) | Source words without German: articles/particles (often fine), textual variants (fine), content words (error) | `Kind = 'unmappedSource' AND Lang='G'`; split by `OrigClass` and `InNa`/`InTr` ("not in NA and not in TR" = info) | `BookBarChart`; `DonutChart` by class (T, CONJ, PRT, N, V …); top Strong's list | J-M |
| U3 | **Unmapped Hebrew words** (required) | same for OT: object marker `HTo`, Ketiv/Qere duplicates, real gaps | `Lang='H'`, split by main-morpheme class, `HebType`, `StrongsBase = 'H0853'` separately | as U2 | J-M |
| C1 | **Coverage heatmap** book × chapter | Chapters the AI skipped or botched (sudden drop), versification gaps | per `(BibleBookId, Chapter)`: share of aligned content-PoS German words and share of mapped source words (articles/`HTo` excluded) | heatmap (new component), click cell = chapter list | cell → J-M for the chapter |
| M1 | **German lemma → many Strong's** | Homographs (`Tor` gate/fool, `Weise`), lemma errors (`Götter`), mapping noise (`Gott` → ὁ) | group `(LemmaLower, Pos, Lang, StrongsBase)` over aligned rows; rank `total × (1 − topShare)`, ≥ 50 occ., ≥ 2 keys with ≥ 5 % (query in `lemma-bulk-edit-original-language.md`); flag minority < 5 % with different gloss; OT-FW exclusion | ranked bar list of lemmas; per lemma a `DonutChart` of Strong's | group → J-B (split homograph), row → J-M |
| M2 | **Strong's → many German lemmas (outliers)** | Single wrong alignments: G2424 Ἰησοῦς → `der`; dominant rendering vs tail | group `(Lang, StrongsBase, LemmaLower)`; outlier = share < 5 % (or < 3 occurrences) and German PoS class not expected for `OrigClass` | `DonutChart` per Strong's (exists on Strong's page), ranked list "most outliers" | J-M |
| M3 | **PoS disagreement German ↔ original** | German tagging errors (`sein` AUX → αὐτοῦ 209×, `meinen` VERB → μου 68×) and misalignments | static table German UPOS → expected original classes (NOUN/PROPN→N(A); VERB/AUX→V; DET→T,D,P,R; PRON→P,R,D,F; ADJ→A,N; ADP→PREP/R …), flag rows outside; OT-FW exclusion; known translation technique (`siehe`→ἰδού INJ) via dismissals | matrix German PoS × original class (counts, cells clickable) | J-L (German PoS) and J-M |
| M4 | **Content word ↔ function word** | Misalignment proxy: German NOUN/PROPN/VERB/ADJ mapped to article/conj/prep/particle (`Gott` → ὁ 13×) and, NT only, German DET/ADP mapped to a Greek N/V | subset of M3 with severity `error`; measured 0.2–2.3 % of content words | count tiles per testament + list | J-M |
| L1 | **Missing lemma / morphology** | Words the lemmatizer skipped | `NOT HasMorphology OR Lemma IS NULL OR Lemma = ''`; also `Pos IS NULL` | count tile + `BookBarChart` | J-L |
| L2 | **Lemma = surface form for inflected words** | Lemmatizer echoed the form | `LemmaLower = PlainLower` AND (NOUN with `Number='Plur'` or `Case<>'Nom'` and the lemma's other forms differ; VERB `VerbForm='Fin'` and lemma not ending in `n`; ADJ with `Case` set and lemma ending `-e/-en/-em/-er/-es` while a shorter lemma exists) | ranked list by lemma | J-L, J-B |
| L3 | **Same surface form, several lemmas** | Inconsistent lemmatization (`Jesu`/`Jesum`), homographs (`meinen`) | group `PlainLower` → distinct `(LemmaLower, Pos)`; flag minority < 10 %; show the original key per group to tell homograph from error | ranked list, expand = mini donut | J-B (minority group), J-L |
| L4 | **Same lemma, several PoS** | `Jahwe` NOUN 4,858 / PROPN 1,642; `der` DET/PRON (legit) | group `LemmaLower` → distinct `Pos`; minority share; allow-list for legit pairs (DET/PRON, ADJ/ADV, VERB/AUX) | ranked list | J-B with PoS preset |
| L5 | **Morphology fields that don't apply / are missing** | Tense on a noun, case on a finite verb, missing gender on NOUN | evaluate `ElbLemmaValueMaps.Rules` (+ `ByVerbForm`) in C# over the rows, or one SQL `CASE` generated from the rules; also codes not in the value maps | count per (PoS, field) table | J-L |
| E1 | **Recently edited** | Review what changed (and by whom) before trusting it | `Elb1871MorphologyEdits` / `ChangeSets` by `EditedAt` (+ `OriginalKey`); mapping: verses whose `CreatedAt::date` is not a seed day | timeline (counts per day, `BookBarChart`-like) + list | J-L / J-M |

### Tier 2 – should

| # | Check | Reveals | SQL idea | Chart | Jump |
|---|---|---|---|---|---|
| M5 | **Proper-noun mismatch** | German PROPN ↔ Greek non-proper / Hebrew non-`Np`, and the reverse (name mapped to `der`) | `ProperNoun <> (Pos = 'PROPN')`, content PoS only | count + list grouped by lemma | J-L / J-M |
| M6 | **n:m size outliers** | Over-assigned AI rows (`Der` → Ὁ, τὸν, τῶν, τῆς in Joh 18:19), groups ≥ 6 tokens | `SourcePerGerman ≥ 3` (Greek) / `≥ 4` (Hebrew), `GermanPerSource ≥ 4`; or connected-component size as in 0005 | histogram of group sizes (bar chart, log y) | J-M |
| M7 | **Order crossing / swapped duplicates** | Two occurrences of the same Strong's in a verse mapped crosswise (2nd `Gott` → 1st θεός), links to a far-away word | per verse: for German words with the same `StrongsBase` occurring ≥ 2× in the verse, check that German order = source order; plus `abs(PositionInVerse/len_de − SourcePos/len_src) > 0.5` | list sorted by distance | J-M |
| M8 | **Cross-verse links** | Mapping into a neighbouring verse (versification, AI window) | `SourceRefId <> verse's LxxRefId` and the pair not in `BibleVerseCountingMapping`; legacy `ParentGermanWordId` in another verse (53/179) or = self (16/53) | count + list | J-M |
| M9 | **Duplicate / redundant links** | Same German word linked to a source word twice via parts; added row with an edge (157/613); `GermanWordPart` = whole word (43k Hebrew), `*`, or not a substring of the word | `GermanWordPart` checks on legacy tables; v2 makes most impossible (unique indexes) → keep as legacy-hygiene list until cut-over | count tiles | J-M |
| C2 | **Verse length ratio** | Verse segmentation errors, missing/duplicated verses, half-imported verses | per verse `n_de / n_src` (articles/`HTo` excluded), z-score vs the book median; also verses with 0 German or 0 source words | scatter (`n_src` x, `n_de` y, outliers marked) or ranked list; heatmap colour variant of C1 | J-M |
| C3 | **Verses without any mapping** | Verses the AI run never reached | verse with source words but no `aligned` row | list, count per book | J-M |
| L6 | **Noun gender inconsistency** | Wrong gender (or a homograph: `Tor`, `Erbe`, `Band`) | `Pos='NOUN'` group `LemmaLower` → distinct `Gender`; minority share; show original key per gender | ranked list | J-B / J-L |
| L7 | **Capitalisation of lemma vs PoS** | NOUN/PROPN lemma lower-case, other PoS capitalised (sentence-start copies) | `Pos IN ('NOUN','PROPN') AND Lemma ~ '^[[:lower:]]'`, or other PoS with `Lemma ~ '^[[:upper:]]'` and `PositionInVerse = 1` | count per PoS + list | J-B |
| L8 | **Rare-lemma typos** | Lemmas that occur 1–2× and are 1 edit away from a frequent lemma of the same PoS (`Theil`/`Teil`, `Göttin`/`Götter` false positives possible) | distinct lemmas (~20–30k) loaded in C#, Levenshtein ≤ 1 against lemmas with ≥ 20 occ. (no `pg_trgm` needed) | list | J-B |
| L9 | **Lemma edits against the original** | Bulk edits that changed a word whose original differs from the group the user meant | `Elb1871MorphologyEdits.OriginalKey` vs current key of the word in `DqWordFacts` | list per change set | J-L, revert link |
| F1 | **Facets on every list** | – | testament, book range, German PoS, `Lang`, original class, `Kind`, edition (`InNa`/`InTr`), `HebType`, min count, "only content words", "edited since" | – | – |

### Tier 3 – later (needs Mapping v2 or more data)

| # | Check | Reveals | SQL idea | Chart | Jump |
|---|---|---|---|---|---|
| A1 | **AI-only / unconfirmed links** | Unreviewed machine output, by book | `MappingLinkTrust.IsUnconfirmedMachine` | coverage heatmap variant "confirmed share" | J-M |
| A2 | **Low AI confidence** | Links the model itself doubted | `MappingLinks.Confidence < 0.6` (only for runs that report it; legacy NULL) | histogram of confidence | J-M |
| A3 | **Disputed links / open changesets** | Reviewer disagreement, stale proposals | `MappingConfirmations.Verdict = 'disputed'`; `Changesets` `submitted` by age | list | J-M / proposals page (0005.9f) |
| A4 | **Shape errors** | Invariant violations in v2 | `MappingLinkShapeErrors` view (must be empty) | count tile (red if > 0) | J-M |
| A5 | **Covered-reason sanity** | `covered/article` on a non-article, `object_marker` on a non-`H0853` | `Kind='covered'` × `Reason` × `OrigClass` | matrix | J-M |
| A6 | **AI run regressions** | A redo run that lowered coverage or flipped many confirmed-like patterns | compare per-range metrics before/after an `AiRuns` row | small multiples per run | J-M |
| X1 | **Number agreement** | German plural ↔ Greek/Hebrew singular nouns (and reverse) | NOUN `Number` vs `StepGreekMorphology.Number` / Hebrew `Grammar`; low precision (collectives, plurale tantum) → only as a filter, not a default list | – | J-M |
| X2 | **Lemma vs STEP German gloss** | Rendering far from STEP's German gloss (`StepGreekWords.German`) | similarity of `LemmaLower` and gloss words; coverage of the column unknown, check first | – | J-M |

### Pruned (not worth a check now)

- Stale `Elb1871GreekMapping.StrongsNumber` (3,113 rows): a one-line `UPDATE` or ignore (readers must use the STEP word); not a review item.
- Morpheme-level Hebrew checks (prefix `ו`/`ה` vs German `und`/`der`): impossible until links address morphemes.
- Overlapping mappings across levels / slider versions: no `GermanEdits` data yet.
- Comparing STEP alignment with SR GNT words (`SrGntWords`): a second alignment source would be valuable, but there is no Elb ↔ SR mapping table to compare against.

## 4. Page layout (suggestion)

1. Header: data timestamp + refresh, testament/book facets.
2. Overview tiles: one per Tier-1 check (count, delta since last refresh), click = open the list.
3. Required charts: three "unmapped" panels (U1–U3) side by side on wide screens, stacked on mobile; C1 heatmap below.
4. Explorer: the two filters (2.4 original, 2.5 German) as tabs that hand off to each other (J-F).
5. Findings list (2.2) for the selected check, with jump menu per row and "dismiss" (tier 2).

Components (propose in chat before building, per `CLAUDE.md`): reuse `BookBarChart`, `DonutChart`, `Select`,
`TextField`, `Button`, `EmptyState`. New candidates: `CoverageHeatmap` (C1/C2/A1 via a `metric` prop), `FindingsTable`
(rows + jump menu + pager), `RankedBarList` (horizontal bars with share; M1, M2, L3, L4; could also be a `variant` of
`BookBarChart`), `PosMatrix` (M3/A5; could be a `CoverageHeatmap` variant with categorical axes → prefer one component).

## 5. Backend work, indexes, performance

| Item | Kind | Notes |
|---|---|---|
| New endpoint group `Endpoints/DataQuality` (`RequiresLogin`, roles maintainer/reviewer) | backend | `overview`, `unmapped?lang&groupBy`, `coverage`, `checks/{code}` (paged `Finding`s), `original?q` (2.4), `german?lemma&pos&form` (2.5), `refresh` (POST) |
| Migration `00NN_DataQuality.sql` | SQL | `CREATE MATERIALIZED VIEW "DqWordFacts" … WITH NO DATA` + indexes (2.1); filled by the refresh endpoint / after seed. Later `DataQualityDismissals` |
| Unique index `Elb1871Morphology (HebRefId, PositionInVerse)` | SQL | verify first; table is not created in `scripts/` |
| Lexicon search index (2.4) | backend, in-memory | ~15k entries, built at startup, < 5 ms per query |
| Rules for M3 (PoS table) and L5 (applicability) | backend, C# | one static class next to `ElbLemmaValueMaps`, unit-tested |
| Deep-link params `map`/`w`/`s` on `ChapterPage`, `word` on `LemmaEditorPage` | frontend | J-M, J-L |
| Strong's page/word stats keyed on obsolete `Elb1871Words.Lemma` | follow-up fix | switch to `Elb1871Morphology.Lemma`, else M1/M2 numbers and the Strong's page disagree |

Performance at this size (722k German words, 142k Greek + 306k Hebrew words, 726k mapping rows, ~31k verses):

- MV refresh 10–30 s, concurrent refresh keeps the page usable. Every Tier-1/2 aggregate on the MV with its indexes:
  10–300 ms; filtered lists (one lemma / Strong's): < 20 ms. The overview (all tile counts) can be one query with
  `COUNT(*) FILTER (…)` per check, ~0.5–1 s, cached until the next refresh.
- Without the MV, the same aggregates join 3–4 large tables per request (0.3–2 s, Hebrew `DisambiguatedStrongs`
  regex parsing on 556k rows is the expensive part) → do the brace/class parsing once in the MV.
- M7 (order crossing) and C2 (z-scores) use window functions per verse over the MV: ~1–3 s full Bible → compute on
  refresh into a small result table, or page by book.
- L8 (typo distance) in C# over ~25k distinct lemmas × ~3k frequent ones: < 1 s, cache with the MV timestamp.
- v2 switch: only the MV definition changes (from `MappingLinks`/`MappingLinkGermanWords`/`MappingLinkSourceWords`/
  `SourceWords`), plus the Tier-3 checks that read `MappingLinkTrust`.

## 6. Open questions

1. Who may see the page: maintainer only, or reviewers too?
2. Freshness: is a manual refresh + auto refresh after bulk edits enough, or must findings be live (then no MV; live
   queries per check with ~1–2 s latency)?
3. Build on the legacy tables now (recommended: one MV definition to swap) or wait for Mapping v2 (0005)?
4. Thresholds (minority < 5 %, M1 ≥ 50 occurrences, M6 group sizes): fixed, or adjustable in the UI?
5. Should dismissals be per finding (word) or per pattern (lemma × Strong's), or both?
