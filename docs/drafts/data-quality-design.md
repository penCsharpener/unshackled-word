# Data quality page – "Datenqualität" (design draft)

Status: draft, nothing implemented. Mockup: `docs/drafts/data-quality.html` (one responsive file; toolbar switches theme,
a 375 px frame and the states loading / empty / error). Input: `docs/drafts/data-quality-suggestions.md` (check codes
U1…L9, `DqWordFacts`, `Finding`, jump codes J-M/J-L/J-B/J-S/J-F are defined there and reused here unchanged).

## Scope

A logged-in tool to find wrong Elb1871 ↔ Greek/Hebrew mappings and wrong German lemma/morphology, and to jump from every
finding to the place that fixes it. Charts first, lists second. In scope: Tier 1 checks (U1–U3, C1, M1–M4, L1–L5, E1)
and the two explorer filters. Tier 2 checks appear as tiles only once their endpoint exists (same `Finding` shape, no
UI change). Out of scope: dismissals UI (Tier 2), AI-trust checks (Tier 3, Mapping v2).

## Entry and route

- Route `/edit/quality`, `handle` without `fill` (the page scrolls). Menu entry "Datenqualität" in `UserMenu` below
  "Lemmata bearbeiten", for roles `maintainer` / `reviewer` (open question 1). Logged out or missing role:
  `EmptyState` "Anmeldung erforderlich" / "Keine Berechtigung" with sign-in button (as the lemma editor).
- All state is in the URL search params (shareable, back button works):

| Param | Meaning |
|---|---|
| `t` | testament facet `ot` / `nt` / empty = both |
| `book` | book facet (`bibleBookId`), set by a `BookBarChart` click |
| `check` | selected check in the findings list (`U1`, `M3` …) |
| `f` | findings facets of the selected check, e.g. `class=T`, `pos=NOUN`, `content=1` |
| `x` | explorer side `o` (Grundtext) / `d` (Deutsch) |
| `q` | explorer filter: `G2316` / `H0430` (original) or `gott~NOUN` (German lemma ~ PoS; PoS optional) |
| `trail` | filter history, comma list of up to 8 `q` values (`o:G2962,d:herr~NOUN,o:H3068`) |
| `page`, `size` | findings paging (50/100/250) |

## Page layout

Container queries on `main` (`max-w-page-wide`, as the Strong's page).

1. **Header**: `h1` "Datenqualität"; stat line "Stand 12.11.2026 14:05 · 722.418 deutsche Wörter · 448.312
   Grundtextwörter" + `Button` ghost "Neu berechnen" (POST refresh, shows "wird berechnet …" while `refreshing`).
   Facet row: testament toggle (`Button` secondary + `aria-pressed`: Alle / AT / NT), `Select` book, reset link.
   Facets apply to every chart, tile and list on the page.
2. **Overview tiles** (`StatTile`): one per Tier 1 check, grouped under small headings *Zuordnung* (U1–U3, C1, M1–M4) and
   *Lemma* (L1–L5, E1). Tile = code, title, big count, muted sub line ("davon 1.204 Inhaltswörter"), delta since the
   previous refresh, severity icon + label. Click = select check (`check=`) and scroll to the findings list.
3. **Unmapped panels** (required): three cards *Deutsch ohne Grundtext* (U1), *Griechisch ohne Deutsch* (U2),
   *Hebräisch ohne Deutsch* (U3). Each card: `DonutChart` by class (German PoS / original class, legit classes like
   article, object marker `HTo`, not-in-NA/TR, Ketiv shown but muted and labelled "meist korrekt"), below it a
   `RankedBarList` "Häufigste" (top 10 lemmas / Strong's, content words only by default, switch "auch Funktionswörter").
   A `Button` ghost "Nach Büchern" swaps the ranked list for a `BookBarChart`. Clicks: slice = findings of that check
   filtered to the class; ranked row = explorer filter (J-F); book bar = `book` facet.
4. **Coverage heatmap** (C1, `CoverageHeatmap`): books (rows) × chapters (cells), colour = share of *unaligned content
   words* (dark = bad). Metric switch "Deutsch" / "Grundtext". Cell click = findings U1/U2/U3 for that chapter; cell
   context action "Kapitel öffnen" = J-M chapter.
5. **Explorer** (required): side switch *Grundtext* / *Deutsch*, one `TextField` (search) + `Select` PoS (German side),
   filter trail (breadcrumbs) and the result (see flows).
6. **Findings list** (`FindingsTable`) of the selected check, with facet chips, paging and a jump menu per row.

| | Phone (< 40rem) | Tablet (40–72rem) | Desktop (72–120rem) | 4k (page max 120rem) |
|---|---|---|---|---|
| Tiles | 2 columns, sub line hidden | 3 columns | 6 columns | 6 columns, `text-3xl` count |
| Unmapped | stacked, donut ring 8rem + legend below | stacked, ring + legend side by side | 3 columns | 3 columns, ring 11rem |
| Heatmap | book label above its row, cells wrap (no horizontal scroll) | grid, 1 cell = 3 units | grid | grid, 4 units |
| Explorer | search, trail wraps, ranked list full width; verse hits below | same | result 2 columns: renderings (left) · forms / verses (right) | same |
| Findings | one card per finding (ref, words, reason, jump buttons) | table, gloss hidden | table | table |

Light/dark: tokens only; the mockup toolbar shows both. Heat and severity colours are new tokens with their own dark
steps (below), not an automatic flip.

## Interaction flows

**Explorer, original side** (`x=o`)
1. Type `Jesus`, `iesous`, `ιησους`, `G2424`, `2424`, `dbr`, `dabar`, `word` → debounced (250 ms) `original/search`.
   One exact Strong's hit selects it directly; otherwise a candidate list (lemma, translit, gloss, `matchedBy` chip
   "Strong's / Grundtext / Umschrift / Konsonanten / Englisch", occurrences). Enter takes the first candidate.
2. Selected: header line (Strong's, lemma `font-original`, translit, gloss, class) + stat line "917 Vorkommen · 902
   zugeordnet · 15 ohne Deutsch". Then `RankedBarList` of German renderings by `lemma (PoS)`, ordered by count, share in
   %, flag chips (`Minderheit`, `Wortart passt nicht`, `Inhalt ↔ Funktion`, `Eigenname`).
3. Click a rendering label = **new filter** on the German side (`x=d`, `q=lemma~PoS`), pushed onto `trail` and the
   browser history. The chevron/"Stellen" button on the row expands its verse hits (paged, 20 per page) with jumps.
4. German side mirrors it: rows are the Strong's numbers behind the lemma (split by `Lang`), plus `hinzugefügt` and
   `ohne Zuordnung` rows; click = new original filter. Side panel "Formen": surface forms with counts and other lemmas
   carrying the same form (click = new German filter on that lemma).

**Trail**: "Filterverlauf: G2962 κύριος › herr (NOUN) › H3068 יהוה". Each crumb is a link (jumps back, truncates the
trail after it); the last is `aria-current`. More than 8 entries drop the oldest. "Verlauf leeren" ghost button.
Phones show the last 3 crumbs plus "… (n)" which expands.

**Findings → fix**: every row has a jump menu (visible buttons on desktop, one "Öffnen ▾" `ToggleMenu`-like popover on
phones). Links open in a new tab (keeps list scroll and paging; open question 3):

| Button | Code | Target |
|---|---|---|
| Zuordnung | J-M | `/bible/{book}/{chapter}?map={verse}&w={elbWordId}&s={stepWordId}#v{verse}` |
| Lemma | J-L | `/edit/lemmas/{book}/{chapter}?word={elbWordId}` |
| Ersetzen | J-B | `/edit/lemmas/{book}/{chapter}?dlg=bulk&from={lemma}&orig={strongs}` |
| Strong's | J-S | `/bible/strongs/{strongs}?rendering={lemma}` |
| Filtern | J-F | same page, `x`/`q` set, scroll to explorer |

After returning to the tab, a finding the user just fixed still shows until refresh; rows that no longer reproduce after
refresh get a muted "bereits behoben?" badge.

## Chart choice per check

| Check | Chart | Why |
|---|---|---|
| U1 | `DonutChart` by German PoS (function words muted), `RankedBarList` top lemmas, `BookBarChart` toggle | part-to-whole of a few classes; the long tail of lemmas needs a ranked list |
| U2 / U3 | `DonutChart` by original class (+ "nicht in NA/TR", `HTo`, Ketiv as own slices), `RankedBarList` top Strong's, `BookBarChart` | same; legit classes must be visible to be ignored |
| C1 | `CoverageHeatmap` share | 1,189 chapters: only a grid shows dips at a glance |
| M1 | `RankedBarList` of lemmas by score; expanded row = `DonutChart` of its Strong's | ranking + composition |
| M2 | `RankedBarList` "Strong's mit den meisten Ausreißern"; row = explorer | ranking |
| M3 | `CoverageHeatmap` `mode="count"`: German PoS × original class, off-table cells outlined | a matrix; cells clickable |
| M4 | two `StatTile`s (AT / NT) + list | single numbers |
| L1 | `StatTile` + `BookBarChart` | count by book |
| L2 / L3 / L4 | `RankedBarList` (L3 expand = mini `DonutChart` of lemmas per form) | ranking |
| L5 | `CoverageHeatmap` `mode="count"`: PoS × field | matrix |
| E1 | `RankedBarList` with one row per day (label = date), newest first | avoids a new time chart; open question 5 |

## Components

Reuse: `Button`, `Select`, `TextField`, `EmptyState`, `FormError`, `BookBarChart`, `DonutChart`, `ToggleMenu` (facet
"Spalten"/"auch Funktionswörter" toggles), `Dialog` (none needed so far). Proposed new components (confirm in chat
before building, then register in `Components.md`):

| Component | Props | Notes |
|---|---|---|
| `StatTile` | `code?`, `title`, `value`, `sub?`, `delta?` (`number`, sign shown, no colour judgement), `severity?` (`error`/`warning`/`info`/`ok`), `selected?`, `href?` or `onSelect?`, `loading?` | Link-or-button card. Severity = icon + label + left border token, never colour alone. Reusable for the Strong's stat line later. |
| `RankedBarList` | `title`, `items` (`{ key, label: ReactNode, sub?: ReactNode, value, share?, flags?: { key, label, severity }[], muted? }`), `max?` (default 10, "Mehr anzeigen"), `onSelect?(key)`, `selectedKey?`, `renderExpanded?(item)`, `expandLabel?`, `valueFormat?` | Horizontal bars as an `<ol>`; label is the click target (J-F), the bar is `aria-hidden`, value + share as text. Could be a `variant="ranked"` of `BookBarChart`, but its items are not books → own component. |
| `CoverageHeatmap` | `rows` (`{ key, label }`), `columns?` (fixed axis for matrices) or per-row `cells` (`{ key, label, value, total, title }`), `mode` (`"share"` 0–1 / `"count"`), `scale` (5 thresholds), `legend`, `onSelectCell(rowKey, cellKey)`, `selected?`, `outlined?(row, cell)` | One component for C1 (books × chapters, ragged rows), M3 and L5 (categorical matrix, `mode="count"`). Grid of `Button`-based cells with roving tabindex (arrows), `title` tooltip, `sr-only` table fallback; list fallback below 40rem. |
| `FindingsTable` | `findings: Finding[]`, `total`, `page`, `pageSize`, `onPage`, `onPageSize`, `columns` (subset of `ref`/`german`/`original`/`detail`/`severity`), `books`, `jumps(finding) => { key, label, href?, onClick? }[]`, `loading?`, `error?`, `onRetry?`, `emptyText?` | Table ≥ 40rem, cards below; jump menu per row; `aria-busy` while loading. Dismiss column later via a `rowActions` prop. |

Page-local markup (no component): header, facet row, filter trail (`<nav aria-label>` + `<ol>`), explorer layout,
candidate list (a `RankedBarList` without bars would be odd → plain `<ul>` of `Button variant="ghost"`).

## Design tokens

Existing: `uw-bg`, `uw-surface`, `uw-surface-muted`, `uw-fg`, `uw-fg-muted`, `uw-border`, `uw-accent`, `uw-focus`,
`uw-mark-*`, `uw-chart-1…8`, `uw-chart-empty`, `font-original`, `font-scripture`, spacing/text/radius scales.
**New** (to add to `src/styles/index.css`, light + dark):

| Token | Light | Dark | Use |
|---|---|---|---|
| `--uw-heat-0…5` | `#f4f2ee`, `#cde2fb`, `#86b6ef`, `#3987e5`, `#1c5cab`, `#0d366b` | `#2b2a27`, `#184f95`, `#256abf`, `#3987e5`, `#6da7ec`, `#b7d3f6` | sequential (one hue, blue ramp of the chart palette); 0 = no gap, 5 = ≥ 20 % gap; dark mode steps run toward light |
| `--uw-sev-error` / `-warning` / `-info` / `-ok` | `#d03b3b`, `#fab219`, `#2a78d6`, `#0ca30c` | same | status, always with icon + label |

## i18n keys (namespace `quality`, de / en)

```ts
quality: {
  nav: "Datenqualität" / "Data quality",
  title: "Datenqualität" / "Data quality",
  stand: "Stand {{date}}" / "As of {{date}}",
  refresh: "Neu berechnen" / "Recompute", refreshing: "Wird berechnet …" / "Recomputing …",
  facets: { all: "Alle" / "All", ot: "AT" / "OT", nt: "NT" / "NT", book: "Buch" / "Book", reset: "Filter zurücksetzen" / "Reset filters" },
  groups: { mapping: "Zuordnung" / "Mapping", lemma: "Lemma" / "Lemma" },
  severity: { error: "Fehler" / "Error", warning: "Prüfen" / "Check", info: "Hinweis" / "Note", ok: "Unauffällig" / "Clean" },
  delta: "{{sign}}{{count}} seit letzter Berechnung" / "{{sign}}{{count}} since last run",
  checks: { U1: { title: "Deutsch ohne Grundtext" / "German without original", sub: "davon {{count}} Inhaltswörter" / "{{count}} content words" },
            U2: "Griechisch ohne Deutsch" / "Greek without German", U3: "Hebräisch ohne Deutsch" / "Hebrew without German",
            C1: "Abdeckung je Kapitel" / "Coverage per chapter", M1: "Lemma → viele Strong's" / "Lemma → many Strong's",
            M2: "Strong's-Ausreißer" / "Strong's outliers", M3: "Wortart passt nicht" / "Part of speech mismatch",
            M4: "Inhalt ↔ Funktionswort" / "Content ↔ function word", L1: "Ohne Lemma" / "Missing lemma",
            L2: "Lemma = Form" / "Lemma = surface form", L3: "Form mit mehreren Lemmata" / "Form with several lemmas",
            L4: "Lemma mit mehreren Wortarten" / "Lemma with several parts of speech",
            L5: "Merkmale passen nicht" / "Features don't apply", E1: "Zuletzt bearbeitet" / "Recently edited" },
  unmapped: { top: "Häufigste" / "Most frequent", byBook: "Nach Büchern" / "By book", withFunction: "auch Funktionswörter" / "include function words", legit: "meist korrekt" / "usually fine" },
  heatmap: { title: "Abdeckung" / "Coverage", german: "Deutsch" / "German", source: "Grundtext" / "Original", legend: "Anteil nicht zugeordneter Inhaltswörter" / "Share of unaligned content words", cell: "{{book}} {{chapter}}: {{percent}} ({{count}} von {{total}})" / "{{book}} {{chapter}}: {{percent}} ({{count}} of {{total}})", open: "Kapitel öffnen" / "Open chapter" },
  explorer: { title: "Erkunden" / "Explore", original: "Grundtext" / "Original", german: "Deutsch" / "German",
    placeholderOriginal: "Strong's, Wort, Umschrift oder Englisch" / "Strong's, word, transliteration or English",
    placeholderGerman: "Lemma oder Wortform" / "Lemma or word form", pos: "Wortart" / "Part of speech", anyPos: "Alle Wortarten" / "Any part of speech",
    matchedBy: { strongs: "Strong's", script: "Grundtext" / "Script", translit: "Umschrift" / "Transliteration", skeleton: "Konsonanten" / "Consonants", gloss: "Englisch" / "English", lemma: "Lemma", form: "Form" / "Form" },
    stats: "{{total}} Vorkommen · {{aligned}} zugeordnet · {{unmapped}} ohne Deutsch" / "{{total}} occurrences · {{aligned}} aligned · {{unmapped}} without German",
    renderings: "Deutsche Wiedergaben" / "German renderings", sources: "Grundtext dahinter" / "Original behind it", forms: "Formen" / "Forms",
    added: "hinzugefügt" / "added", none: "ohne Zuordnung" / "unmapped", verses: "Stellen" / "Verses", trail: "Filterverlauf" / "Filter history", clearTrail: "Verlauf leeren" / "Clear history" },
  flags: { minority: "Minderheit" / "Minority", posConflict: "Wortart passt nicht" / "PoS mismatch", contentFunction: "Inhalt ↔ Funktion" / "Content ↔ function", properNoun: "Eigenname" / "Proper noun" },
  findings: { title: "Befunde" / "Findings", ref: "Stelle" / "Reference", german: "Deutsch" / "German", original: "Grundtext" / "Original", detail: "Grund" / "Reason", open: "Öffnen" / "Open", fixed: "bereits behoben?" / "already fixed?", showing: "{{from}}–{{to}} von {{total}}" / "{{from}}–{{to}} of {{total}}" },
  jump: { mapping: "Zuordnung" / "Mapping", lemma: "Lemma", bulk: "Ersetzen" / "Replace", strongs: "Strong's", filter: "Filtern" / "Filter" },
  states: { loading: "Lädt …" / "Loading …", error: "Konnte nicht geladen werden." / "Could not be loaded.", retry: "Erneut versuchen" / "Retry",
    emptyCheck: "Keine Befunde – gut so." / "No findings – well done.", emptySearch: "Nichts gefunden für »{{q}}«." / "Nothing found for “{{q}}”.",
    notComputed: "Noch nicht berechnet." / "Not computed yet.", stale: "Daten sind älter als die letzte Änderung." / "Data is older than the last edit." },
}
```

## States

| State | Behaviour |
|---|---|
| Never computed (`refreshedAt` null) | Whole page: `EmptyState` "Noch nicht berechnet" + primary "Jetzt berechnen". |
| Refreshing | Header shows "Wird berechnet …"; old data stays visible (concurrent refresh); poll `overview` every 5 s. |
| Loading (per section) | Each card loads independently: tile skeleton (`StatTile loading`), `EmptyState variant="comingSoon"` skeleton rows in charts/tables, `aria-busy`. |
| Error (per section) | `FormError` inside the card + `Button` "Erneut versuchen"; other sections stay usable. |
| Empty check / chapter | "Keine Befunde – gut so." with severity `ok` on the tile. |
| Empty search | "Nichts gefunden für »xyz«" + hint "Vokale/Akzente werden ignoriert; Englisch findet Glossen". |
| Stale | `lastEditAt > refreshedAt` → muted banner "Daten sind älter als die letzte Änderung" + refresh button. |

## Accessibility

- Landmarks: `h1`, one `h2` per section (Übersicht, Ohne Zuordnung, Abdeckung, Erkunden, Befunde); tiles are links with
  full names ("U1 Deutsch ohne Grundtext: 1.204 Fehler").
- Identity never colour only: severity = icon + text; donut legend with counts; heat cells carry `title` + `sr-only` table
  ("Kapitel 7: 12 %"); ranked bars show value text.
- Heatmap: one tab stop, arrows move between cells, Enter selects; announces cell text. Explorer results update an
  `aria-live="polite"` count line ("12 Wiedergaben für G2424").
- Trail is `<nav aria-label="Filterverlauf">` with `aria-current="page"` on the last crumb.
- Jump links that open a new tab say so (`sr-only` "(neuer Tab)").
- Focus: after a J-F click focus moves to the explorer result heading; `prefers-reduced-motion` disables the scroll animation.
- Original text with `lang="grc"` / `lang="he" dir="rtl"`; English glosses `lang="en"`.

## API contract (backend can start now)

Group `Endpoints/DataQuality`, prefix `api/dataQuality`, login + role `maintainer`/`reviewer`, camelCase, nulls omitted.
Common query facets on every GET: `testament` (`ot`/`nt`), `bookId`. All reads come from `DqWordFacts`; responses carry
`refreshedAt` so the UI can detect a refresh in between. `Cache-Control: private, max-age=60`.

```ts
// GET api/dataQuality/overview
interface OverviewResponse {
  refreshedAt: string | null; refreshing: boolean; lastEditAt?: string;
  totals: { germanWords: number; sourceWords: number };
  checks: { code: string; tier: 1 | 2 | 3; count: number; errorCount: number; previousCount?: number;
            sub?: { key: string; count: number } }[];          // e.g. { key: "content", count: 1204 } for U1
}

// GET api/dataQuality/unmapped/{side}   side = german | greek | hebrew; &content=true (default) &top=10
interface UnmappedResponse {
  side: "german" | "greek" | "hebrew"; total: number;
  byClass: { key: string; label: string; count: number; legit: boolean }[];   // key = UPOS or orig class, "notNaTr", "HTo", "ketiv"
  byBook: { bibleBookId: number; count: number }[];
  top: { key: string; label: string; original?: string; transliteration?: string; gloss?: string; pos?: string; count: number }[];
  // key = lemma~PoS (german) or base Strong's (greek/hebrew) -> explorer q
}

// GET api/dataQuality/coverage?metric=german|source
interface CoverageResponse {
  metric: "german" | "source";
  books: { bibleBookId: number; chapters: { chapter: number; total: number; unaligned: number }[] }[];
}

// GET api/dataQuality/checks/{code}?page=1&pageSize=50&pos&lang&class&chapter&minCount&group
interface CheckResponse {
  code: string; total: number; page: number; pageSize: number;
  groups?: { key: string; label: string; count: number; share?: number; flags?: string[] }[]; // aggregated checks (M1, M2, L2-L4, M3 matrix cells)
  matrix?: { rows: string[]; columns: string[]; cells: { row: string; column: string; count: number; expected: boolean }[] }; // M3, L5
  items: Finding[];                                   // shape from data-quality-suggestions.md 2.2, plus:
}
// Finding += { chapterHasMorphology?: boolean; fixed?: boolean }   (fixed = no longer reproduces after refresh)

// GET api/dataQuality/original/search?q=iesous&limit=20
interface OriginalSearchResponse {
  q: string; folded: string;
  candidates: { strongs: string; lang: "G" | "H"; lemma: string; transliteration: string; gloss: string;
                occurrences: number; matchedBy: "strongs" | "script" | "translit" | "skeleton" | "gloss" }[];
}

// GET api/dataQuality/original/{strongs}            strongs = base number, G2424 / H0430
interface OriginalDetailResponse {
  strongs: string; lang: "G" | "H"; lemma: string; transliteration: string; gloss: string; origClass: string;
  occurrences: number; aligned: number; unmapped: number;
  renderings: { lemma: string; pos: string; count: number; share: number; flags: string[] }[];   // by count desc
}

// GET api/dataQuality/german/search?q=gott&pos=NOUN&limit=20
interface GermanSearchResponse {
  q: string; candidates: { lemma: string; pos: string; count: number; matchedBy: "lemma" | "form" }[];
}

// GET api/dataQuality/german?lemma=Gott&pos=NOUN      (pos optional = all)
interface GermanDetailResponse {
  lemma: string; pos?: string; count: number;
  sources: { key: string;                    // "G2316", "H0430", "added", "none"
             lang?: "G" | "H"; lemma?: string; transliteration?: string; gloss?: string; origClass?: string;
             count: number; share: number; flags: string[] }[];
  forms: { form: string; count: number; otherLemmas: { lemma: string; pos: string; count: number }[] }[];
}

// GET api/dataQuality/hits?strongs=G2424&lemma=der&pos=DET&page=1&pageSize=20
//   one of strongs / lemma required; strongs=added|none allowed with lemma
interface HitsResponse { total: number; page: number; pageSize: number; items: Finding[] }   // check = "X" (explorer)

// POST api/dataQuality/refresh  -> 202 { refreshing: true }   (409 if already running)
```

Flags (`flags[]`): `minority`, `posConflict`, `contentFunction`, `properNoun`. Severity per `Finding` from the check
rules in the suggestions doc. Search folding and matching order as in suggestions 2.4 (in-memory lexicon index).

## Open questions

1. Roles: maintainer only or reviewers too (menu entry and endpoint policy)?
2. Should the explorer hits and findings show the verse text inline (±5 words like the bulk edit), or only the
   reference + word (smaller payload)? Mockup shows the short context.
3. Jumps in a new tab (keeps list state) or same tab with restored scroll?
4. Heatmap metric default: German side (what users read) or source side (what the AI skipped)?
5. E1 as a ranked list per day is enough, or is a real timeline chart wanted (new component)?
6. Thresholds (minority < 5 %, heat steps 2/5/10/20 %) fixed in the backend, or adjustable on the page?
