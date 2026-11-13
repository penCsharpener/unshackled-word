# Strong's page – stats header (design draft)

Status: draft, nothing implemented. Mockup: `docs/drafts/strongs-stats.html` (one responsive file; toolbar switches theme, a 375 px frame and two sample scenarios: Strong G1577 / word search »Versammlung« AT+NT). Reference: csv-bible.de Strong and lemma pages.

## Page order (`/bible/strongs/:strongs`)

1. **Header** – `h1`: `Strong G1577 – ἐκκλησία – ekklēsia` (lemma in `font-original` + `text-accent`, transliteration italic). Sub line: `Griechisch – Versammlung (church, assembly)`: language, German gloss = most frequent Elberfelder rendering(s), English STEP gloss in brackets (`lang="en"`). Stat line: **114** Vorkommen · **111** Verse · **17** von 27 NT-Büchern. Replaces today's "Alle Stellen zu G1577" heading; that heading moves down to the concordance.
2. **Gebildet aus** – placeholder now (see below).
3. **Vorkommen im NT / AT / Bibeltext** – book bar chart.
4. **Übersetzung und Grundtext** – donuts.
5. Existing concordance (KWIC list + pagination), unchanged, under an `h2` "Alle Stellen".

## Layout per width (container queries on the page `main`, which is `max-w-page-wide` = 120rem)

| | Phone (~375 px, container < 40rem) | Tablet (40–72rem) | Desktop (72–150rem) | 4k (≥ 150rem*) |
|---|---|---|---|---|
| Header | `text-2xl`, wraps; stat line wraps | `text-3xl` | `text-3xl` | `text-4xl` |
| Gebildet aus | rows stack: badge + lemma, count on 2nd line | count right-aligned in the row | same | same |
| Book chart | **list**: one row per book *with* occurrences (abbr link · horizontal bar · count), then one muted line "Ohne Vorkommen (10): Mk, Lk, …" | **columns**: one bar per book, all books, zeros as flat muted bars, count above bar, abbreviations in two staggered rows (odd/even + tick) | columns, plot height 36 → 48 units from 100rem | OT and NT side by side (`39fr 27fr`), `text-sm` labels |
| Donuts | 1 column, ring 8rem + legend beside it | 2 columns | 3 columns | 3 columns, ring 11rem |

\* 120rem is the page max width, so the 4k row only applies if the page container is widened for stats pages or with browser zoom < 100 %; at 120rem the desktop column already holds 66 bars at ~22 px each. Open question 6.

**Why a list on phones instead of horizontal scroll or two label rows:** 27 NT bars on 343 px are 12 px wide and 39 OT bars 8 px, so even staggered labels collide and counts can't be read; horizontal scroll hides the overview and fights the page scroll. The list keeps every number readable, needs no tooltip (touch), and is the text alternative at the same time. A "Als Liste / Als Diagramm" toggle (ghost `Button`) lets desktop users switch too (screen-reader and copy friendly).

**One scale for OT and NT** in the word-search case so bar heights are comparable; the per-testament total sits in a small heading above each part.

## Components

Registered today (`Components.md`): `Button` (view toggle, later "Mehr anzeigen"), `AppShell`. No chart, select or empty-state component exists. Proposed (to confirm in chat before building):

| Component | Props | Notes |
|---|---|---|
| `BookBarChart` | `books: BibleStructureBook[]` (from `useBibleStructure`), `counts: BookCount[]` (`{ bibleBookId, count }`, zeros may be omitted), `testaments: Testament[]` (`["nt"]`, `["ot"]`, `["ot","nt"]`), `title`, `summary?`, `bookHref?: (bookId) => string`, `view?: "auto" \| "list"` | Generic: works for Strong's and later free word search. Section colour per book from a static table in `lib/bible/books.ts` (`bookSection(id)`). Columns layout = `<ol>` of `<li>` with visible count, `aria-hidden` bar and `sr-only` "Apostelgeschichte: 23"; hover/focus tooltip "Apostelgeschichte: 23 (20,2 %)". List layout = the same data as `<ol>`. Legend lists only sections that occur. |
| `DonutChart` | `title: ReactNode`, `subtitle?` ("114 Vorkommen in 111 Versen"), `slices: DonutSlice[]` (`{ key, label: ReactNode, count, href? }`), `centerValue`, `centerLabel`, `highlightKey?`, `filter?: { label, value, onChange }` (options are generated from `slices`), `note?` | Generic, not Strong's-specific: the word search will reuse it for whole Bible / AT / NT. SVG `<circle pathLength="100">` segments via `stroke-dasharray`/`stroke-dashoffset` attributes (no style), 2 px gap between segments, centre number in HTML. Legend is an HTML `<ul>` with swatch, label, count and percent: identity is never colour only. Selecting a filter dims the other segments and shows that slice's count in the centre; it is also the hook for filtering the concordance later. Colours by slice order (stable, sorted by count from the API), max 8 slots; a 9th+ slice folds into "Sonstige". |
| `Select` | `label`, `value`, `onChange`, `options`, `disabled?`, `size?: "sm" \| "md"` | Native `<select>` styled with tokens; needed by `DonutChart` and later by search filters. |
| `EmptyState` | `title?`, `children`, `variant?: "empty" \| "comingSoon"` | Dashed box with muted text; `comingSoon` adds a skeleton slot. Also usable for "Keine Stellen gefunden". |
| `LemmaRow` (later, with the data) | `strongs`, `lemma`, `lang`, `transliteration`, `gloss`, `meta` ("923 Mal in 756 Versen"), `href` | For "Gebildet aus" and later the lemma page lists ("Wörter im AT, die so übersetzt werden"). Not built now; the placeholder renders its skeleton. A `StrongsBadge` inside it could become a variant of the existing mark colours, no own component. |

Header, section headings and stat line stay plain markup in `StrongsPage`; no component.

## Data: available vs. needed

**Available today**

- `GET api/strongsOccurrences/strongs/{strongs}?page&pageSize` → `strongs`, `language` (`greek`/`hebrew`), `totalItems` (= occurrence count), paged KWIC items. Not enough for the charts (only one page of books).
- `GET api/wordDetail/wordId/{id}` → per reading `lexicon { lemma, transliteration, gloss (en), definition }`, `occurrenceCount` (testament), `renderings` top 5. Keyed by Elberfelder word id, not by Strong's, so the Strong's page can't use it.
- `GET api/bibleStructure` → books with `testament`, `abbreviationDe/En`, names: enough for axis labels and zero books.
- DB: `StepStrongsLexicon` (`OriginalWord`, `Transliteration`, `Gloss` en), `WordDetailRepository.GetOccurrenceCountAsync` and `GetRenderingsAsync` (Strong's → German words, reusable with a larger limit).
- Bible sections (Gesetz, Geschichte, …): no data needed, static table by book id in `lib/bible/books.ts`.

**New endpoint 1 – Strong's summary** (one call, cacheable like the occurrences: `public, max-age=3600`)

```http
GET api/strongsSummary/strongs/{strongs}
```

```ts
interface GetStrongsSummaryResponse {
  strongs: string;                 // "G1577"
  language: "greek" | "hebrew" | "aramaic";
  lemma: string;                   // "ἐκκλησία" (StepStrongsLexicon.OriginalWord)
  transliteration?: string;        // "ekklēsia"
  gloss?: string;                  // English STEP gloss
  occurrenceCount: number;         // 114
  verseCount: number;              // 111 (COUNT DISTINCT LxxRefId / RefId)
  byBook: { bibleBookId: number; count: number }[];   // only books > 0, Elberfelder numbering
  renderings: { word: string; count: number }[];      // all Elberfelder renderings, most used first
  composedOf?: RelatedStrongs[];   // later; omitted until data exists
}
interface RelatedStrongs {
  strongs: string; lemma: string; transliteration?: string; gloss?: string;
  occurrenceCount: number; verseCount: number;
}
```

**New endpoint 2 – German word statistics** (drives the "»Versammlung« im AT / NT" donuts now and the free word search later)

```http
GET api/wordStats?word=Versammlung&testament=all|ot|nt&match=lemma|form
```

```ts
interface GetWordStatsResponse {
  word: string;
  testament: "all" | "ot" | "nt";
  occurrenceCount: number; verseCount: number;
  byBook: { bibleBookId: number; count: number }[];
  originals: {                     // Strong's numbers behind the German word, most used first
    strongs: string; lemma: string; transliteration?: string; gloss?: string;
    count: number;                 // how often this Strong's is rendered with the word
    totalCount: number;            // all occurrences of the Strong's ("105 von 123 Mal")
  }[];
}
```

The Strong's page calls it once per testament for the top rendering (or the rendering picked in donut 1's select). Matching on `Elb1871Words.Lemma` (German lemma) rather than `PlainWord` would group "Versammlung/Versammlungen"; open question 3.

**Gaps (no data yet):** "Gebildet aus" (etymology / composition), "related Strong's numbers" beyond "same German rendering", verse counts, German gloss (derived from renderings for now), AT data for Greek words (LXX is not mapped to the Elberfelder text).

## Donut semantics (proposal)

The request names (a) this Strong's vs. related Strong's and (b) German renderings and the original words behind them. The templates solves this with three donuts, and the mockup follows it:

1. **G1577 – ἐκκλησία**: its Elberfelder renderings (select: "Übersetzung"). = (b), first half.
2. **»Versammlung« im AT**: Strong's numbers rendered with the top rendering in the OT (select: "Strong"). = (b), second half.
3. **»Versammlung« im NT**: same for the NT, with G1577 highlighted against the other Strong's. = (a), "related" defined as "shares a German rendering".

For Hebrew numbers the order flips (AT donut highlights the current number). If a Greek word has no OT data the AT donut still makes sense, since it shows Hebrew words translated the same way.

## Accessibility

- Every chart is a `<figure>` with a heading; the data is always also present as text: bar chart columns are an `<ol>` with `sr-only` "Buch: Anzahl" per item (bars `aria-hidden`), the list view is the plain table; donut legends list every slice with count and percent, the SVG ring is `aria-hidden`.
- Counts are visible on every non-zero bar and in every legend row. This is required: chart slots 3, 4 and 5 (aqua, yellow, pink) are below 3:1 against the light surface (palette validator WARN), so colour never carries meaning alone and sections are also named in the legend.
- Zero books: muted flat bar plus muted label, link disabled (`aria-disabled`, not focusable); in the list view they are a single text line.
- Bar links (`bookHref`) are focusable and show the same tooltip on `:focus-visible` as on hover; the tooltip text is a duplicate of the `sr-only` text, so it is `aria-hidden`.
- Selects have visible labels; a disabled select when there is only one slice.
- Hebrew text in legends and header gets `lang="he" dir="rtl"` per span.

## i18n keys (de / en)

```text
bible.strongs.heading            "Strong {{strongs}}"                         / "Strong's {{strongs}}"
bible.strongs.statOccurrences    "{{count}} Vorkommen"                         / "{{count}} occurrences"
bible.strongs.statVerses_one/_other "{{count}} Vers" / "{{count}} Verse"        / "{{count}} verse(s)"
bible.strongs.statBooks          "{{count}} von {{total}} Büchern"             / "{{count}} of {{total}} books"
bible.strongs.glossEn            "Englische Lexikon-Glosse (STEP)"             / "English lexicon gloss (STEP)"
bible.strongs.composedOf.title   "Gebildet aus"                                / "Formed from"
bible.strongs.composedOf.soon    "Die Wortbildung wird noch aufbereitet …"     / "Word formation data is coming …"
bible.strongs.composedOf.count   "{{count}} Mal in {{verses}} Versen"          / "{{count}} times in {{verses}} verses"
bible.strongs.allOccurrences     "Alle Stellen"                                / "All occurrences"
bible.stats.books.title.nt|ot|all "Vorkommen im Neuen Testament" | "… im Alten Testament" | "Vorkommen im Bibeltext"
bible.stats.books.summary        "{{count}} Vorkommen in {{books}} von {{total}} Büchern"
bible.stats.books.bookValue      "{{book}}: {{count}}"                         (sr-only + tooltip, tooltip adds "({{percent}})")
bible.stats.books.zeroBooks      "Ohne Vorkommen ({{count}}): {{books}}"       / "No occurrences ({{count}}): {{books}}"
bible.stats.books.showList / showChart  "Als Liste" / "Als Diagramm"           / "Show as list" / "Show as chart"
bible.stats.books.legend         "Farben: Bibelteile"                          / "Colours: parts of the Bible"
bible.stats.section.law|history|wisdom|prophets|gospels|paul|general|revelation
                                 "Gesetz", "Geschichte", "Poesie & Weisheit", "Propheten", "Evangelien & Apg",
                                 "Paulusbriefe", "Hebr. & allg. Briefe", "Offenbarung"
bible.stats.donuts.title         "Übersetzung und Grundtext"                   / "Translation and source text"
bible.stats.donut.inTestament    "»{{word}}« im {{testament}}"                 / "“{{word}}” in the {{testament}}"
bible.stats.donut.total          "{{count}} Vorkommen in {{verses}} Versen"
bible.stats.donut.filterTranslation / filterStrongs / showAll   "Übersetzung" / "Strong" / "Alle anzeigen"
bible.stats.donut.centerTranslations_one/_other "Übersetzung in {{count}} Vorkommen" / "Übersetzungen …"
bible.stats.donut.centerOriginals.greek|hebrew  "griech. Wort(e) in {{count}} Vorkommen" / "hebr. Wort(e) …"
bible.stats.donut.ofTotal        "von {{total}} Vorkommen"
bible.stats.donut.other          "Sonstige"                                    / "Other"
bible.stats.donut.slice          "{{count}}× · {{percent}}"
```

Numbers and percents via `Intl.NumberFormat(i18n.language)`.

## Tokens

New in `styles/index.css` (light + dark, exposed through `@theme inline` as `--color-chart-1…8`, `--color-chart-empty`):

- `--uw-chart-1 … --uw-chart-8`: categorical slots, light `#2a78d6 #eb6834 #1baf7a #eda100 #e87ba4 #008300 #6250d6 #e34948`, dark `#3987e5 #d95926 #199e70 #c98500 #d55181 #008300 #9085e9 #e66767`. Ran the dataviz palette validator: CVD and normal-vision separation pass in both modes; dark passes contrast; light slots 3/4/5 WARN (< 3:1), handled with visible numbers and lists. Hex is fine here or converted to oklch to match the file.
- `--uw-chart-empty`: zero bars and the donut track (light ~`surface-muted`/`border` mix, dark `oklch(0.3 0.01 60)`).
- Section → slot: Gesetz 1, Geschichte 2, Poesie/Weisheit 3, Propheten 4, Evangelien & Apg 5, Paulusbriefe 6, Hebr. & allg. Briefe 7, Offenbarung 8 (8 sections = 8 slots; csv-bible's 10 groups would need more hues than are distinguishable).
- Existing: `surface`, `surface-muted`, `border`, `fg`, `fg-muted`, `accent` (lemma, links, highlighted slice text), `mark-same` (Strong's badge), `focus`.

## Lint / convention points

- Bar height/width is the one dynamic value. Options: (a) a CSS custom property via `style={{ "--v": ratio }}` (mockup does this) with a Biome exception limited to `*Chart.tsx`; (b) SVG `<rect height=…>` attributes, which need no style but make the count label hug the bar only if it is also SVG text (scales with `preserveAspectRatio="none"`, so labels would move to a fixed row above the plot). Recommendation: (a).
- Raw `<select>` lives only inside `Select`; raw `<button>` only via `Button`.
- No new dependency: charts are ~150 lines of SVG/CSS each.

## Open questions

1. Donut (a) "related Strong's numbers": is "shares a German rendering" (csv-bible) what you mean, or derived/compound words (e.g. ἐκκαλέω), or STEP's unified/alternative numbers?
2. Source for "Gebildet aus": Strong's 1890 dictionary derivations (public domain, "from G1537 and G2564") parsed in SeedDb, or something from STEP? Then `composedOf` lands in the summary endpoint.
3. Word stats match on German lemma (Versammlung + Versammlungen) or exact form? csv-bible uses the lemma.
4. Should clicking a bar / donut slice filter the concordance below (`?book=44`, `?rendering=…`)? Needs a filter param on `strongsOccurrences`; the design leaves `bookHref` / `filter.onChange` ready for it.
5. OK to allow `style={{ "--v": … }}` in chart components (Biome exception), or should bars be pure SVG?
6. 4k: keep the 120rem page width (OT and NT stacked, still readable) or let the stats section go wider so OT and NT sit side by side?
7. Header German gloss: use the top Elberfelder rendering(s) (as csv-bible), or wait for a German lexicon gloss?
8. Sections: 8 groups OK, or do you want csv-bible's 10 (major/minor prophets, Acts separate)? More than 8 colours are not reliably distinguishable.
