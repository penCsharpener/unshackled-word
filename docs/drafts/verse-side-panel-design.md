# Verse side panel – "Querverweise / Grundtext" in the left margin (design draft)

Status: implemented (S1–S4, S6; S5 hover preview left out); open questions decided by the user (see end). Not revised verses only: cross-highlight of the Grundtext uses Elb1871 word ids, so words of a revised verse are not highlighted. Mockup:
`docs/drafts/verse-side-panel.html` (one file; toolbar switches theme, viewport 1280 / 1920 / 2560, the states loaded /
loading / error, and the eye panel under verse 2; the three-state control and `G` switch TSK / Grundtext / aus; hover
a German or Greek word for the cross-highlight, Greek words link to their Strong's page (target shown in the toolbar),
"+N" expands the long TSK list of Joh 3,6; the "Grundtext-Schrift" switch to `text-2xl` makes the originals taller
than the German so the row growth is visible, grown rows are outlined).

## Scope

The word box (`WordDetailPopover variant="margin"`) and the hover info (`WordInfoCard`) now always live in the **right**
margin. From 1920px the left margin is empty. This draft fills it with a **per-verse side panel**: next to each verse
either its **TSK cross references** (default) or its **Greek/Hebrew source text** (Grundtext), or nothing ("aus"),
switched by one small three-state control. Read-only, public, no login. Below 1920px the full TSK list moves into the
eye panel (S6, in scope).

Out of scope: editing TSK, grouping TSK by German keyword (needs a keyword ↔ Elb1871 word mapping), interlinear
gloss/transliteration lines, and any change to the right stack (HI/WD placement, pinning, R12–R14).

## What exists today (findings)

- **Layout** (`styles/index.css`): `main` is capped at `max-w-page-wide` (120rem) with `px-8`. `.reader-col` is 65ch
  (≈ 35rem at the column's 16px sans font), left-aligned from 64rem with room for the right margin, centred from 120rem.
  `MarginStack` is absolutely positioned at `left: calc(100% + 2rem)` of the column and measures the verse's `top` with
  `getBoundingClientRect` on every render (one stack at a time). `side="left"` and `.margin-stack[data-side="left"]`
  are now dead code.
- **Verse row** (`ChapterPage`): `div.verse-row.relative#v{n}` → `<p>` (verse number, word `Button`s, eye and pen
  `map-trigger`s) → optional mapping panel (`OriginalVerse` or `MappingEditor`) → optional `RevisionEditor`. The eye/pen
  panels sit **below** the `<p>` and make the row taller; the row's top edge never moves.
- **TSK**: `ITskRepository.GetOutgoingAsync(startHebRefId, endHebRefId, limit)` already takes a **range** (LXX → Hebrew
  numbering via `BibleVerseCountingMapping`). Only `WordDetailService` uses it, for one verse, capped at 20
  (`CrossReferencesTruncated`). The UI list is a private `CrossReferences` function inside `WordDetailPopover`
  (`bookAbbreviation`, links `/bible/{book}/{chapter}#v{verse}`, `<details>` closed above 5). There is no chapter-level
  TSK endpoint.
- **TSK volume** (`assets/tskxref.txt`): median 9 references per verse, p90 21, p99 38, max 93. Per chapter: median
  ≈ 225, p99 ≈ 720, max ≈ 1 660 (Ps 119). Every row also carries `Scope`, the **English KJV keyword** ("beginning", "God").
- **Grundtext**: only per verse, through `GET elberfelder/verseMapping/...` (`GetVerseMappingResponse`: original words
  with `position`, `original`, `transliteration`, `lemma`, `strongs`, `gloss`, `morphologyCode`, plus German ↔ original
  `links`). `OriginalVerse` renders it (RTL for Hebrew, `font-original`, lemma/Strong's/morphology toggles,
  `bg-mark-linked` cross-highlight with the hovered German word). `chapterWords` only carries `senses` (key + form) per
  German word, not the original word order, so it cannot render a source text.

## Layout and breakpoints

**The panel appears from 120rem (1920px, the existing `fhd` breakpoint) and nowhere below.** Between 1024 and 1919px
the column is left-aligned and the right margin is needed for the WD card, which grows to 1.75 × 18rem; squeezing a
left panel in at 1440 leaves the card ≈ 10rem of room and moves the column at every resize step. At 1920 the space is
simply there.

**Width budget at 1920** (column 65ch ≈ 35.75rem at the column's 16px sans font, centred): the column's left edge is
≈ 42rem from the screen edge (≈ 41.5rem with a classic scrollbar). Left: 2rem gap + **32rem panel** + ≈ 7.5rem to the
screen edge, enough for the `px-8` page padding and a scrollbar. Right: unchanged (2rem gap + 18rem stack; the WD card
grows to 31.5rem and still has ≈ 40rem of room). The panel hugs the column, like the right stack.

**Token** `--side-panel-w`: `32rem` from 120rem, `40rem` from 160rem (2560px). At 2560 the column's left edge is
≈ 62rem from the screen edge, so 2rem + 40rem leaves ≈ 20rem. The panel is absolute, so it may reach past `main`'s
120rem box into the outer gutter; `main` itself stays capped and centred, so the column, the right stack and the
header do not move. 40rem is the cap: wider Greek/Hebrew lines (≈ 70 characters) get hard to read, and TSK gains
nothing. At 3840 the 2560 layout applies with wider empty gutters; a 4k screen at 150–200 % scaling is 1920–2560 CSS px
anyway, so it lands on one of the two layouts.

```
1280px (64–119rem): no left panel, no mode control; right stack as today; full TSK in the eye panel (S6)
┌──────────────────────────────────────────────────────────────────────────┐
│ header ··· [AT|NT picker] ··· slider ··· 🔍 ☾ DE                          │
├──────────────────────────────────────────────────────────────────────────┤
│ ‹      Johannes 3      ›                                                 │
│ ¹ Es war aber ein Mensch aus den   [eye][pen]    ┌ HI ────────────┐       │
│   Pharisäern, mit Namen Nikodemus,               │ ἄνθρωπος G444  │       │
│ ² dieser kam zu ihm bei Nacht ...                ├ WD ────────────┤       │
│                                                  │ Querverweise ▸ │       │
│                                                  └────────────────┘       │
└──────────────────────────────────────────────────────────────────────────┘

1920px (≥ 120rem), TSK mode: panel | column | right stack
┌────────────────────────────────────────────────────────────────────────────────────────────────┐
│     ┌ Querverweise (TSK)  [≡|Αα|⊘] ┐ ‹     Johannes 3     ›                                    │
│     │ (sticky rail header)         │                                                           │
│     ├──────────────────────────────┤                                                           │
│     │ Joh 3,10 · Joh 7,47–49       │ ¹ Es war aber ein Mensch aus      ┌ HI ─────────┐         │
│     │                              │   den Pharisäern, mit Namen ...    │ ἄνθρωπος    │         │
│     │ Joh 7,50–51 · Joh 12,42–43 · │ ² Dieser kam zu ihm bei der Nacht  ├ WD ─────────┤         │
│     │ Joh 19,38–39 · Ri 6,27 ·     │   und sprach zu ihm: Rabbi, wir    │ ...         │         │
│     │ Jes 51,7 · Phil 1,14 · [+15] │   wissen, daß du ...               └─────────────┘         │
│     │ Joh 1,51 · Mt 5,18 · ...     │ ³ Jesus antwortete und sprach ...                          │
│~7rem│<─────────── 32rem ──────────>│<2rem>  ≈ 36rem column  <2rem> 18rem (grows →)              │
└────────────────────────────────────────────────────────────────────────────────────────────────┘

1920px, Grundtext mode: rows grow to max(German, original); German keeps its line breaks
│     │ Ἦν δὲ ἄνθρωπος ἐκ τῶν        │ ¹ Es war aber ein Mensch aus den Pharisäern,   │
│     │ Φαρισαίων, Νικόδημος ὄνομα   │   sein Name Nikodemus, ein Oberster der Juden. │
│     │ αὐτῷ, ἄρχων τῶν Ἰουδαίων·    │   (extra space: row grows to the panel)        │
│     │ οὗτος ἦλθεν πρὸς τὸν ...     │ ² Dieser kam zu ihm bei der Nacht ...          │

2560px: main stays 120rem and centred; only the panel widens to 40rem into the outer gutter
┌─────────┬──────────────────────────────── 120rem main ─────────────────────────────┬─────────┐
│ ~20rem  │ panel 40rem (may cross main's edge) | column 36rem | right stack 18rem    │ (empty) │
└─────────┴───────────────────────────────────────────────────────────────────────────┴─────────┘
```

**Below 1920 (fallback, in scope as S6):** no panel and no control. Grundtext is already one click away (eye icon).
The eye's original-text panel gets a second section with the verse's **full** TSK list (`CrossReferenceList
variant="inline"`, same chapter query), so phones and laptops get it without a new control. The word box keeps its
verse-level list (max 20).

## Per-verse alignment: one grid cell per verse row, two panel behaviours

The verse row becomes a one-cell grid. Its existing children (the `<p>` with words and eye/pen, the eye panel, the
revision panel) move into one wrapper `div.verse-main`; the side panel is a second item in **the same cell**, pulled
into the margin with a negative inline margin:

```css
.verse-row  { display: grid; grid-template-areas: "verse"; }          /* from 120rem only */
.verse-main { grid-area: verse; }
.verse-side {                                                        /* VerseSidePanel */
  grid-area: verse;
  width: var(--side-panel-w);
  margin-inline-start: calc(-1 * (var(--side-panel-w) + var(--spacing) * 8));
  align-self: start;
}
.verse-side[data-mode="tsk"] {   /* TSK: does not count for the row height, clips */
  position: absolute; inset-block: 0; overflow: clip;   /* containing block = the grid cell (row is relative) */
  align-self: stretch;   /* abspos in a grid: `start` would shrink it to its content */
  margin-inline-start: 0; right: calc(100% + var(--spacing) * 8);
}
/* Grundtext: in flow, so the cell (= row) is max(German block, original block) tall; never clipped */
```

- **TSK** stays out of flow: absolute, as tall as the verse row, clipped with "+N" (below). The column never moves.
- **Grundtext is never cut.** The panel is an in-flow grid item in the same cell as the German block, so the row height
  is `max(German, Grundtext)` by plain CSS: no measuring, no `ResizeObserver`, no layout loop, and the next verse's
  panel can never be overlapped because the next row starts below the taller of the two. Top alignment is exact for
  both (same cell, `align-self: start`).
- **Effect on the reading column**: when the original needs more lines than the German, the German verse gets **extra
  space below it** (line breaks, width and position unchanged). Typical: rare in the NT (Greek at `text-lg` in 32rem is
  shorter than German at `text-2xl` in 36rem), more frequent in short Hebrew verses. It only happens in Grundtext mode;
  in TSK and "aus" the rows are exactly as today. On a mode switch the rows change height, so the switch keeps the
  verse at the top of the viewport in place (remember the first visible `#v{n}` before, restore its offset after;
  CSS `overflow-anchor` alone is not reliable for a whole-page reflow).
- **Alignment for free**: top = the verse row's top, no measuring, no scroll/resize listeners. Tall verses give the
  panel more room; an opened eye/pen panel makes the row taller, so the side panel grows with it and shows more
  references (its top stays put).
- **Reading-column stability**: the panel never changes the column's width, position or line breaks (TSK is out of
  flow; Grundtext only adds height below a verse). The `#v{n}` scroll targets, `scroll-mt-32`, the revised-verse accent bar (`-left-3`, inside the 2rem gap) and the
  right `MarginStack` (which measures `#v{n}` against `colRef`) are untouched.
- **First-line alignment**: a token offset `--side-panel-pad` (`calc((1lh of the verse) - 1lh of text-sm) / 2`, ≈ 0.5rem
  at `laptop:text-2xl`) so the first reference line sits on the verse's first line.
- **Rejected – grid row per verse** (`[panel | text]`): the panel column would have to be part of `.reader-col`, which
  either shrinks the 65ch text or widens/shifts the column and breaks the `left: 100%` geometry of the right stack and
  the centring rules. Re-laying out every verse row also puts the HI/WD stability work at risk.
- **Rejected – MarginStack per verse**: `MarginStack` measures with `getBoundingClientRect` on every render; 176 of them
  re-measuring on each hover is the jitter we just removed. Stacking/"pushing down" panels (margin-note layout) breaks
  the "verse N next to verse N" promise after the first long original.
- **Rejected – JS `min-height` on the row** (measure the panel, write `--side-min-h`): same visual result as the grid
  cell, but one frame late (flash of overlap) and an observer per verse. The grid cell gets it from layout itself.
- **DOM cost**: one wrapper `div` per verse row, only a class change below 120rem (`.verse-row` stays a block there).

### Long TSK lists: clip, "+N", expand

- References are **inline text links** (`text-sm`, separated by thin middots), not padded chips: ≈ 8–9 per line in 32rem,
  so a 3-line German verse (≈ 7.5rem) holds ≈ 6 lines ≈ 50 references. That covers ≈ 99 % of verses.
- When the list does not fit, a **"+N"** button (`Button variant="ghost" size="sm"`) sits on the last visible line,
  right-aligned over a `surface` fade. N = hidden count, computed by one shared `ResizeObserver` for all panels (a link
  is hidden if its bottom exceeds the panel's client height). The same pass rounds the visible height down to whole
  lines (`max-height` in `lh` steps) so no half-cut line shows. No observer work below 120rem.
- Click "+N" (or **focus inside the panel**, so keyboard users never land on a clipped link) → the panel **expands as an
  overlay**: `inset-block-end: auto`, `max-height: min(60dvh, …)`, own scroll, `surface` background, border, shadow,
  `z-index` above the following verses' panels. "Weniger" / `Esc` / click outside / focus leaving collapses it. Only one
  panel is expanded at a time. The text column does not move.

## The toggle: three states TSK / Grundtext / aus

- **Where**: a **sticky rail header** in the left margin, level with the chapter title: `aside.side-rail` (absolute,
  `right: calc(100% + 2rem)`, full column height, `pointer-events: none` except its header) with a
  `position: sticky; top: calc(var(--app-header-h) + 1rem)` header showing the current heading ("Querverweise (TSK)" /
  "Grundtext"; nothing in "aus") and the control. It stays reachable while scrolling Ps 119, also when the panel is off.
- **Control: a small segmented control of three icon buttons** (`role="group"`, `aria-label` "Randspalte"), each a
  `Button variant="secondary" size="sm"` with **`aria-pressed`** (exactly one pressed): list icon "Querverweise (TSK)",
  stroked "Αα" glyph "Grundtext", crossed-out circle "Randspalte ausblenden". Inline SVGs like eye/pen, `aria-hidden`,
  the label in `aria-label` and `title`. Recommended over a single cycling button: a cycling button cannot use
  `aria-pressed` honestly (three states), hides the next state behind a click, and needs two clicks to go back; three
  small buttons cost ≈ 7rem and show the state at a glance. No new component (three `Button`s in a `div`).
- **Keyboard**: three Tab stops in a row (not a radio group: each button acts immediately). Shortcut **`G`** cycles
  TSK → Grundtext → aus → TSK on the chapter page (same guard as the arrow keys, `isTypingTarget`), only from 120rem; it
  is in each button's `title` ("… (G)"). A polite live region announces "Querverweise werden angezeigt" / "Grundtext
  wird angezeigt" / "Randspalte ausgeblendet". Focus stays where it was.
- **"aus"**: no panels are rendered and no panel data is fetched; rows are exactly as today. The rail header keeps only
  the control.
- **Global, not per verse.** A per-verse override would add a third icon to 176 rows (next to eye and pen) and makes the
  margin a patchwork; the eye already gives a per-verse Grundtext in depth.
- **Persisted in `localStorage`** key `uw.sidePanel` = `"tsk" | "original" | "off"`, default `"tsk"` (also for unknown
  values), read/write in `try/catch` like `uw.readerLevel`. Not a user setting: the reader is public and has no
  settings store.

## TSK panel

- **Label**: `bookAbbreviation(book, i18n.language)` + `{chapter},{verse}` or `{verse}–{endVerse}` (same as the word
  box). Consecutive references to the **same chapter** collapse into "Joh 9,16.33" only when the API returns them
  adjacent (no reordering for that).
- **Link**: `/bible/{bookId}/{chapter}#v{verse}`; `aria-label` in full form "Johannes 9,16".
- **Order**: TSK order (keyword by keyword, which follows the verse's word order), duplicates removed (first wins). No
  grouping and **no keyword in the UI** (decided): the keyword is English KJV wording. The API still returns it
  (`keyword`) for a later grouping by mapped German word.
- **Hover preview** (S5, cheap): after 300ms hover/focus on a link, a small card with the target verse text, fetched
  via the existing `chapterText` query of the target chapter (react-query cache, one request per target chapter; ranges
  show the first verse + "…"). Rendered in the left margin, not the right stack.
- **States**: loading → rail header `role="status"` "Querverweise werden geladen …", panels empty (no 176 spinners);
  error → rail header text + "Erneut versuchen" (`bible.retry`); a verse without TSK → empty panel (no per-verse "none"
  noise); a chapter without any → one muted line in the rail header.
- **Performance**: **one request per chapter**, not per verse. Ps 119 ≈ 1 700–2 000 rows ≈ 60 KB JSON, ≈ 15 KB gzip;
  median chapter ≈ 8 KB. Static data → `staleTime: Infinity`, prefetched with the chapter words when the panel is in
  TSK mode at ≥ 120rem. IntersectionObserver lazy loading per verse would mean up to 176 requests and empty panels while
  scrolling; rejected.

## Grundtext panel

- **Content**: the verse's original words in `position` order, `font-original`, `text-lg`, `dir="rtl"` + `lang="he"`
  for Hebrew (`lang="arc"` for the Aramaic parts of Daniel/Ezra), `dir="ltr"` + `lang="grc"` for Greek; Hebrew is
  right-aligned against the gap so it reads towards the German text. No transliteration/gloss lines (compact, parallel
  reading); Strong's, lemma and morphology are one hover away.
- **Words**: each word is a link to `/bible/strongs/{strongs}` (decided: a click opens the Strong's page, it does not
  pin the word box; `aria-label` "λόγος, G3056: Strong's-Seite öffnen"); words without Strong's are plain text.
- **Hover/focus on an original word**: highlights the linked German word(s) in the column (`bg-mark-linked`) and shows
  `WordInfoCard` (original, lemma, Strong's, morphology) in the right stack at that verse, like a German hover. **Hover
  on a German word** highlights its original word(s) in the panel. Same rules as the open eye panel (links by revision
  word id for revised verses). A pinned WD still wins (R14).
- **Relation to the eye panel (no duplication)**: the eye stays the per-verse **study and editing** tool (lemma /
  Strong's / morphology toggles, mapping editor, save). The side panel is new in that it shows **the whole chapter in
  the original next to the German at once**, for parallel reading, with zero clicks per verse. Implemented as a
  `variant` of `OriginalVerse`, so both share word rendering and highlighting.
- **Never clipped**: if the original needs more height than the German verse, the in-flow grid cell (see alignment)
  grows the row; no "…", no overlay, no hidden words. Measured in the mockup at `text-lg`: Joh 3,1–6 all fit beside
  the German (e.g. Joh 3,1: 78px German, 66px Greek at 1920); with the Greek at `text-2xl` every verse grows
  (Joh 3,1: 78 → 123px). So the size token decides how often rows grow; keep the panel at `text-lg` (Hebrew with
  points may need `text-xl`, to be checked with real Hebrew chapters in S3).
- **States**: as TSK, own texts; a verse without originals → muted "–".
- **Data**: a new chapter endpoint (below), fetched only when the mode is Grundtext; one request per chapter.

## Accessibility

- **One landmark**: the rail is `<aside aria-label="Randspalte">` containing an `h2` (the visible heading) and the
  three-button mode control (`role="group"`, `aria-label` "Inhalt der Randspalte" / "Side panel content"; in "aus" the
  `h2` stays as `sr-only` "Randspalte"). The 176 per-verse panels are **not** landmarks: each is `role="group"` with
  `aria-label` "Querverweise zu Vers 16" / "Grundtext von Vers 16".
- **Focus order**: DOM order is verse words → eye → pen → panels below the verse → side panel → next verse, so a
  keyboard user meets a verse's references right after the verse. Each side panel is **one Tab stop** (roving
  tabindex as in the lemma grid): Tab enters at the first link, `←/→` move (visual direction, RTL-aware), `Home/End`,
  `Enter` follows, `Esc` collapses an expanded panel, Tab leaves. Without this a chapter has up to ≈ 2 000 Tab stops.
- **Screen readers**: verse references are read as "Johannes 9,16" (full names in `aria-label`), "+9" as
  "9 weitere Querverweise anzeigen" with `aria-expanded`. Hebrew/Greek words carry `lang` so voices switch.
- **Reduced motion**: expanding/collapsing and the mode switch have no animation at all (a 150ms fade at most, off under
  `prefers-reduced-motion`).
- **Contrast**: links `text-accent`, separators and "+N" `text-fg-muted`, highlight `bg-mark-linked`; all existing
  tokens with light and dark values. The expanded overlay uses `surface`, `border`, the existing popover shadow.
- **Mobile/tablet**: nothing new until S6 (TSK section in the eye panel, full width, same `CrossReferenceList`).

## i18n keys (namespace `bible.sidePanel`, de / en)

```ts
bible: { sidePanel: {
  rail: "Randspalte" / "Side panel",
  modeGroup: "Inhalt der Randspalte" / "Side panel content",
  headingTsk: "Querverweise (TSK)" / "Cross references (TSK)",
  headingOriginal: "Grundtext" / "Source text",
  modeTsk: "Querverweise (TSK) zeigen (G)" / "Show cross references (TSK) (G)",
  modeOriginal: "Grundtext zeigen (G)" / "Show source text (G)",
  modeOff: "Randspalte ausblenden (G)" / "Hide side panel (G)",
  announceTsk: "Querverweise werden angezeigt" / "Showing cross references",
  announceOriginal: "Grundtext wird angezeigt" / "Showing source text",
  announceOff: "Randspalte ausgeblendet" / "Side panel hidden",
  verseTsk: "Querverweise zu Vers {{verse}}" / "Cross references for verse {{verse}}",
  verseOriginal: "Grundtext von Vers {{verse}}" / "Source text of verse {{verse}}",
  more: "+{{count}}" / "+{{count}}",
  moreLabel: "{{count}} weitere Querverweise anzeigen" / "Show {{count}} more cross references",
  less: "Weniger" / "Show less",
  eyeSection: "Querverweise (TSK)" / "Cross references (TSK)",   // S6, section in the eye panel
  loadingTsk: "Querverweise werden geladen …" / "Loading cross references …",
  loadingOriginal: "Grundtext wird geladen …" / "Loading source text …",
  errorTsk: "Querverweise konnten nicht geladen werden." / "Could not load cross references.",
  errorOriginal: "Grundtext konnte nicht geladen werden." / "Could not load the source text.",
  emptyChapter: "Keine Querverweise in diesem Kapitel." / "No cross references in this chapter.",
  noOriginal: "Kein Grundtext für diesen Vers" / "No source text for this verse",
  strongsLink: "{{original}}, {{strongs}}: Strong's-Seite öffnen" / "{{original}}, {{strongs}}: open Strong's page",
  preview: "Vorschau {{reference}}" / "Preview {{reference}}",
} }
```

## Components (check against `Components.md`)

Reused: `Button` (the three mode buttons, "+N", "Weniger", retry), `OriginalVerse` (new variant), `WordInfoCard` / `MarginStack`
(right side only), `Link`. Changed: `WordDetailPopover` (uses the extracted list, no visible change), `MarginStack`
(drop the dead `side="left"`; `side` prop and the `data-side="left"` CSS go).

Proposed (to be confirmed in chat before building):

| Component | Props | Why not a variant |
|---|---|---|
| `CrossReferenceList` | `references: CrossReference[]`, `books`, `variant: "details" \| "inline"`, `truncated?`, `onPreview?(ref)` | Extracted from `WordDetailPopover` (private today); `details` = current collapsible list, `inline` = middot-separated side-panel/eye-panel list. One list, three places. |
| `VerseSidePanel` | `verse`, `label`, `mode: "tsk" \| "original"`, `expanded`, `onExpandedChange`, `children` | Grid-cell placement, `mode` picks clip + "+N" + overlay (TSK) or in-flow, never clipped (Grundtext); roving focus and the group label; content-agnostic. |

`OriginalVerse` gets `variant?: "panel" | "margin"` (default `panel` = today). `margin` drops the `map-panel` frame and
the three toggles, renders `text-lg`, words as Strong's links, and needs only `language`, `originalWords`, `linkedIds`,
`onHoverWord`, so its `data` prop is widened to `Pick<GetVerseMappingResponse, "language" | "originalWords">` and the
toggle props become optional (required only for `panel`).

New tokens (`styles/index.css`, next to `--margin-card-w`): `--side-panel-w` (`32rem` from 120rem, `40rem` from
160rem), `--side-panel-pad`. Breakpoints: existing `fhd` (120rem) plus a new `--breakpoint-qhd: 160rem` used only for
the token; JS `useMediaQuery("(min-width: 120rem)")`. CSS: `.verse-row` grid, `.verse-main`, `.verse-side`.

**Render cost**: `ChapterPage` re-renders on every hover. `VerseSidePanel` is `memo`ised; per-verse data comes from a
`Map<verse, …>` built once in `useMemo`; the cross-highlight set is passed only to the hovered verse's panel (others get
a stable empty set). Only the active mode is rendered.

## API / backend

Both under the `Elberfelder` route group (`api/elberfelder/...`), `AllowAnonymous()`, FastEndpoints REPR, Dapper,
contracts in `Domain.WebApi/Elberfelder/...`, SQL per the SQL-formatting skill.

**1. `GET chapterCrossReferences/bookId/{BibleBookId:int}/chapterId/{ChapterId:int}`**

```jsonc
{ "verses": [ { "verse": 16, "references": [
    { "bibleBookId": 62, "chapter": 4, "verse": 9, "endVerse": 10, "keyword": "so loved" } ] } ] }
```

- Service `ChapterCrossReferenceService` (Application/Features/CrossReferences) calls the **existing**
  `ITskRepository.GetOutgoingAsync` with the chapter's first and last HebRefId and a safety limit (5 000). The
  normalisation `BuildCrossReference` moves out of `WordDetailService` into a shared mapper used by both. Dedupe per
  verse. Verses without references are omitted.
- `CrossReferenceResponse` gains optional `Keyword` (from `Tsk.Scope`); the word box ignores it.
- Check the plan of the TSK SQL for Ps 119: two correlated subqueries per row against `BibleVerseCountingMapping`; if
  slow, rewrite as `LEFT JOIN LATERAL` and add an index on `BibleVerseCountingMapping("LxxRefId")` in a new numbered
  migration script.

**2. `GET chapterOriginals/bookId/{BibleBookId:int}/chapterId/{ChapterId:int}?level=`**

```jsonc
{ "language": "greek", "verses": [ { "verse": 16, "revisionVerseId": 812,          // only for revised verses
    "words": [ { "id": 9001, "position": 1, "original": "οὕτως", "lemma": "οὕτω", "strongs": "G3779", "morphologyCode": "ADV" } ],
    "links": [ { "germanId": 40211, "originalId": 9001 } ] } ] }
```

- Reuses the verse-mapping read SQL with a verse range instead of one verse; `level` picks base vs. revision links
  (`germanId` = Elb1871 word id or revision word id, as in `ChapterPage.linked`). No `gloss`/`transliteration`
  (not shown) to keep Ps 119 at ≈ 2 500 words ≈ 250 KB JSON, ≈ 50 KB gzip; ensure response compression is on.
- Per-language: `language` per verse only if a chapter can mix Hebrew/Aramaic (Dan 2, Ezra 4), otherwise per chapter.

## Slice plan sketch (each runs end to end)

| Slice | Content | Done when |
|---|---|---|
| S1 | Backend endpoint 1 + shared mapper + tests; `useChapterCrossReferences`; `CrossReferenceList` extracted (word box unchanged); tokens; verse-row grid + `.verse-main` wrapper; rail header with the control in two states TSK / aus, `uw.sidePanel`, `G`; `VerseSidePanel` (TSK, clip, no "+N") from 120rem; dead `MarginStack` left side removed; roadmap 0012 text updated; `Components.md`. | At 1920 and 2560 Joh 3 shows TSK next to each verse (32 / 40rem), aligned with opened eye/pen panels; "aus" hides it and survives reload; at 1919 nothing; word box TSK as before. |
| S2 | "+N" measurement, overlay expand, focus-within, roving tabindex, `Esc`, tests. | Gen 1,1 at 1920: "+N" expands, Tab walks verses one stop per panel. |
| S3 | Backend endpoint 2; `OriginalVerse variant="margin"` with Strong's links; third state Grundtext (in-flow, row growth), scroll position kept on mode switch, live region. | Joh 3,1 shows the whole Greek, row grown, verse 2 below it; Ps 23 RTL; a Greek word opens its Strong's page. |
| S4 | Cross-highlight both ways, `WordInfoCard` on original hover, revised verses. | Hover "Welt" ↔ κόσμον highlights both; pinned WD unaffected. |
| S5 | Hover preview of target verses (optional). | Hovering "1Jo 4,9" shows the verse after 300ms. |
| S6 | Below 1920: full TSK section (`CrossReferenceList inline`) in the eye's original panel (in scope). | At 800px and 1280px the eye panel lists the verse's full TSK. |

## Roadmap 0012 reconciliation (lines that must change)

- Decisions block, "**From 1920px** HI and WD go to the margin on the side nearer the pointer…" (line 40) → "HI and WD
  always use the right margin; from 1920px the left margin holds the verse side panel (draft verse-side-panel)."
- **R7**: "from FHD it is in the left/right page margin, whichever is nearer to the pointer" → "from 1024px in the right
  margin" (also fixes the stale "below FHD header centre" wording).
- **R10**: add "From 1920px the left margin holds the verse side panel (TSK / Grundtext / aus), the right margin HI/WD.
  In Grundtext mode a verse row may be taller than its German text."
- **R11**: drop "from 1920px up in the margin (left or right) nearer to the pointer".
- **0012.6f** and **0012.6b2f** done-when texts ("in the nearer margin", "in the margin on the pointer's side") →
  "in the right margin"; scope line 22 ("margin from 1920px") and the assumption at lines 72–74 are outdated too.
- Outside the roadmap: the CSS comment above `--margin-card-w` and the `MarginStack` row in `Components.md`
  ("left or right of the text").

## Risks

- **Hover re-render cost** over 176 panels and up to ≈ 4 500 links/words in Ps 119; mitigated by memo, stable props and
  rendering only the active mode. Measure with the React profiler on Ps 119 before S2.
- **Overlay overlap**: an expanded panel near the chapter end may reach past the column bottom (into the footer); cap
  with `max-height` and own scroll; never let it change document height.
- **HI/WD stability**: S4 adds a second hover source (original words) to the right stack; must keep "pin wins" (R14) and
  the reserved `min-h-24` slot.
- **Numbering**: TSK targets fall back to LXX ids where `BibleVerseCountingMapping` has no row, and cross-chapter
  ranges are cut to their first verse (existing behaviour, now far more visible).
- **SQL plan** of the TSK query at chapter scale (see API).
- **Row growth in Grundtext mode**: uneven gaps in the German column (mostly short Hebrew verses) and a reflow on every
  mode switch; mitigated by keeping the top verse in place and by "aus"/TSK leaving rows untouched. The verse-row grid
  and wrapper touch the DOM that `MarginStack` measures (`#v{n}` stays on the row, so its top is unchanged); re-check
  the HI/WD placement and the R12 chapter-end push-up with a grown last verse.
- **Panel past `main` at 2560**: the 40rem panel crosses `main`'s padding box; nothing may clip it (no `overflow` on
  `main`/`AppShell`), and it must not cause horizontal scroll at exactly 160rem with a scrollbar (check 2560 − 15px).

## Decided (user, 2026-11-14)

1. **Breakpoint**: from 120rem (1920px), where the column has margins on both sides; nothing earlier. Panel width
   32rem, 40rem from 160rem (2560px). The Grundtext is **never clipped**; rows grow instead (see alignment).
2. **Off state**: yes, three states TSK (default) / Grundtext / aus; segmented control of three `Button`s with
   `aria-pressed`, `G` cycles, persisted in `localStorage` (`uw.sidePanel`).
3. **TSK keyword**: not shown in the panel; kept in the API.
4. **Click on an original word**: opens its Strong's page.
5. **Fallback below 1920 (S6)**: in scope of the roadmap (full TSK section in the eye panel).
