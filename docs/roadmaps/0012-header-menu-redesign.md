# Roadmap 0012: Header menu with chapter picker (one row on FHD+, full-width book panel)

Frontend only. Replaces the quick attempt from 2026-11-14 (picker overlaid on the header with `.sticky-picker` at
`z-index: 40`), which the maintainer rejected. Meant to be reviewed first and then implemented from scratch by a
stronger model.

## Goal

The sticky top menu and the Old/New Testament picker become one coherent component family that behaves the same on every
page that shows the picker (reader, `/bible` book page, lemma editor) and works from mobile to 4k.

What is wrong today (seen on the maintainer's screen, 2026-11-14):

- On wide screens (FHD+) the picker was pulled over the header with a negative margin. It **covers menu entries** (the
  left button text, "Lemmata bearbeiten", is overwritten) because it is positioned independently of the header's flex
  row.
- A **line remains below the buttons** (`.sticky-picker` has `border-bottom`, and the header is a separate surface).
- When a testament is opened, the **books and chapters no longer fill the screen width**: the panel inherits the
  `max-w-page` of the overlaid wrapper (72rem) instead of the full width.
- Before that attempt: a second row with a visible gap between header and picker on wide screens.

Out of scope: the hover-info content itself (already built: header centre below 1920px, margin from 1920px; only its
placement and pinning are in scope, R10–R14), the
search overlay, the level slider's own design, the chapter page content, backend.

## Status and final rules (2026-11-14, after the click dummy review) — implemented

These rules **replace** conflicting statements in R1–R14 and the assumptions below (breakpoints: lg 1024px, laptop
1200px, fhd 1920px). Click dummy: `docs/drafts/0012-header-menu-click-dummy.html`.

- **From 1200px** the testament buttons and the level slider are in the header row (one 3rem row, centred between nav and
  icons); below that they are a sticky row under the header. No line below the buttons.
- **Open panel:** full-width overlay under the bar, closes on chapter pick, `Esc`, outside click. `/bible` keeps its
  inline default-open panel.
- **From 1024px** the text column is left-aligned with the right margin reserved for the cards; from 1920px it is centred
  again. Font size steps up at 1200px (not 1536px).
- **HI** (hover info) is a small box in the margin from 1024px (never pinned, follows every hover); **WD** (word details)
  shows below it on hover (display only) and is **pinned by a click** (close button, clickable "Alle Stellen"). Cards line
  up with the hovered/pinned verse and are pushed up near the chapter end. Below 1024px: HI in the header, WD popover.
- HI and WD always use the **right** margin (the nearer-pointer rule was dropped); from 1920px the left margin holds the verse side panel (TSK / Grundtext / aus, see `docs/drafts/verse-side-panel-design.md`).
- Implemented in `HeaderPickerBar`, `MarginStack`, `WordInfoCard`, `BibleChapterPicker` (`panel` prop) and
  `WordDetailPopover` (`variant`, `pinned`). Known gap: the mapping editor's hover info is no longer shown in the header
  from 1200px; the lemma editor keeps its picker in the page (overlay panel).

## Requirements (all maintainer statements, 2026-11-14)

| # | Requirement |
|---|---|
| R1 | **FHD and above (>= 1920px):** the Old/New Testament buttons are part of the header row. The header is one row, 3rem high. No second row, no gap. |
| R2 | The testament buttons sit in the **centre** of that row, between the nav (logo, Bibel, Lemmata bearbeiten) on the left and search, language, theme, user on the right. **Nothing may be overlapped or overwritten** at any width from 1920 up to 4k, in German and English, logged in and out. |
| R3 | **No line** (border/shadow) below the buttons; header and picker read as one bar. |
| R4 | A **gap-free** layout: no stray whitespace between header, picker and content at any width. |
| R5 | **Open state:** clicking a testament opens the book/chapter panel at the **full screen width** (not limited to the page column), as an overlay below the header that does not push the text. It closes on chapter pick, `Esc` and click outside; focus returns to the testament button. |
| R6 | **Below FHD:** keep the current two-row behaviour (header, then picker below it, sticky), but with R3/R4 applied and the open panel full width too (R5). |
| R7 | **Hover info:** below FHD it stays in the header centre (own row on mobile); from 1024px it is in the right page margin (already built; must keep working with the new header). |
| R8 | The same picker behaves identically on the reader, `/bible` and the lemma editor. |
| R10 | **Reading column position:** between 1024 and 1920px the chapter text is **not centred**: it is left-aligned with margin only on the right (the right margin is where the info panels live). From 1920px up the text is centred again with margins on both sides: the left margin holds the verse side panel (TSK / Grundtext / aus), the right margin HI/WD; in Grundtext mode a verse row may be taller than its German text. |
| R11 | **Word menu (click popover, `WordDetailPopover`):** from 1024px up it is shown in the **right margin**; also from 1920px up. Below 1024px it stays the popover it is today. |
| R12 | **Hover info placement:** the margin hover info sits directly next to the text column, not at the screen edge, and never overlaps or "hangs" beyond the previous/next chapter buttons (screenshot 2026-11-14: info far left next to the `‹` button, still shown after the pointer left). It disappears when the pointer leaves the word. |
| R13 | **Pin:** a click on a word **pins** the margin hover info (original, lemma, Strong's, morphology) so it stays while the pointer moves; the "Alle Stellen" button in it becomes clickable (today the panel has `pointer-events: none`, so it cannot be clicked). A click elsewhere, `Esc` or clicking the same word again unpins. Pinned info replaces/merges with the word menu of R11 (one panel per word, not two). |
| R14 | Hover over another word while a word is pinned does not change the pinned panel (pin wins). |
| R9 | Design tokens only, no raw `<button>`, no arbitrary Tailwind values; light/dark; DE/EN; keyboard operable; new components proposed in chat first and registered in `Components.md`; no new dependency. |

## Assumptions (not confirmed; change them before implementing)

- The **level slider** (`LevelSlider`) stays next to the testament buttons: right of them inside the header row on FHD+,
  right end of the picker row below FHD. If it does not fit on FHD it moves into the open panel header.
- The picker on `/bible` (book page) keeps `defaultTestament="ot"`, i.e. it is open by default there, and on that page
  the panel is **inline** (not an overlay), because the page has nothing else.
- Reader/chapter navigation arrows and chapter title stay below the header as they are.

- **Hover info below 1920px:** from 1024px the hover info could also move into the right margin (R11 does this for the
  word menu); until confirmed it stays in the header centre between 1024 and 1919px. The pinned panel (R13) is the
  one that lives in the margin from 1024px.
- Word menu and pinned hover info are merged into **one** panel (hover info on top, word menu content, "Alle Stellen").

## Root cause and shape

The first attempt tried to move a page-level element over the global header with CSS. That cannot work: the picker is
rendered by the page (inside `<main>`), the header by `AppShell`; they do not know each other's width. The fix is
structural: the header owns a **slot** for the picker, pages put the picker into the slot (React portal or context), and
the open panel is its own full-width layer anchored under the header.

```
FHD+:   [nav .........][ Altes Testament | Neues Testament | slider ][ search DE EN theme user ]   <- 3rem, one row
Open:   +-------------------------- full-width panel (books, chapters) ---------------------------+
<1920:  [nav ... hover info ... search DE EN theme user]
        [ Altes Testament | Neues Testament ............................................ slider ]
```

## Tasks

| # | Task | Depends on | Done when |
|---|------|-----------|-----------|
| 0012.1f | Header slot: `AppShell` renders an empty `PickerSlot` container in the centre of the header row (CSS grid `1fr auto 1fr` so the centre cannot overlap the nav or icons). `ChapterPage`, `BiblePage`, `LemmaEditorPage` render the picker through the slot on FHD+ and in place below FHD (one `BibleChapterPicker`, mounting point chosen by breakpoint). Remove the `fhd:` overlay classes and the `.sticky-picker` override added on 2026-11-14. | — | At 1920px on `/bible/1/1` the header is a single 48px row, "Altes Testament" and "Neues Testament" are centred, "Bibel", "Lemmata bearbeiten" and the user menu are fully readable (logged in, DE and EN); resizing 1280 → 1920 → 3840 never overlaps. |
| 0012.2f | Remove the line and gaps: no `border-bottom` on the picker, header and picker share the `surface` background, spacing only from tokens, the `-mt-8`/`py-8` compensation in `ChapterPage` replaced by a layout that does not need negative margins. | 0012.1f | At 1920, 1366 and 390 px there is no visible line below the buttons and no empty band between header, picker and the chapter title (zoomed screenshots compared). |
| 0012.3f | Full-width open panel: when a testament is open, the panel is an overlay layer (`fixed`, `inset-x-0`, top = header height, `max-height` = viewport minus header, scrolls inside) with the book grid using the full width; content below does not move. Closes on chapter pick, `Esc` (focus back to the testament button), click outside; opening the other testament switches. | 0012.1f | At 1920 and 2560 px, opening "Altes Testament" shows all 39 books across the whole screen width; picking "1. Mose 3" navigates and closes it; `Esc` and outside click close it; the chapter text underneath does not jump. |
| 0012.4f | Below FHD parity: picker row below the header stays sticky, same overlay panel (0012.3f), slider at the right end of the row, hover info in header centre (mobile: own row). | 0012.3f | At 1366 and 390 px the reader shows header, picker row and a full-width open panel; hover/focus on a word still shows its info in the header centre. |
| 0012.5f | Other pages: `/bible` (default-open, inline panel) and the lemma editor use the same component and pass the same checks. | 0012.4f | `/bible` and `/edit/lemmas` at 1920 and 390 px show the same picker look and behaviour as the reader; no duplicated picker markup left. |
| 0012.6f | Hover info on the new header: margin panel (FHD+) and header centre (below) verified; `MarginWordInfo` clears the full-width panel (z-order) and the picker slot. Update/extend tests (`BibleChapterPicker.test.tsx`, a header layout test), `Components.md` (new/changed components, proposed in chat first). | 0012.5f | Hovering a word at 1920 shows the info in the nearer margin while the picker is closed; with the panel open nothing is hidden behind it; `bun run check` green; `Components.md` lists every new or changed component. |
| 0012.6b1f | Reading column and margin zones (R10, R12): a layout with three zones from 1024px: text column + right margin (1024–1919px, text left-aligned, no left margin), centred column with margins on both sides from 1920px. The margin info panel is positioned relative to the text column edge (not `fixed` to the screen edge), clears the prev/next chapter buttons and is removed on `mouseleave`/`blur` of the word. | 0012.6f | At 1280px the chapter text starts at the left page padding and the right margin holds the info; at 1920 and 2560px the text is centred and the info appears right next to the column in the right margin; hovering the first word of a verse no longer shows info beside the `‹` button, and leaving the word clears it. |
| 0012.6b2f | Word menu in the margin (R11): `WordDetailPopover` content is rendered in the right margin from 1024px (also from 1920px), popover below 1024px. | 0012.6b1f | Clicking *Gott* at 1280px shows the word menu in the right margin, at 1920px in the right margin as well, at 800px as the popover; the "Alle Stellen" button is reachable by keyboard and mouse. |
| 0012.6b3f | Pin (R13, R14): click pins the margin info (unified with the word menu), pointer events enabled on the pinned panel, "Alle Stellen" clickable and navigates to the word's occurrences; unpin via same-word click, outside click or `Esc`; hover on other words is ignored while pinned. Hover info stays non-interactive until pinned. | 0012.6b2f | Click *Gott*, move the mouse away and over other words: the panel stays on *Gott*; clicking "Alle Stellen" opens its occurrences; `Esc` unpins and hover works again. |
| 0012.7f | Verification gate at widths 390, 768, 1366, 1920, 2560, 3840 in light and dark, DE and EN, logged in and out: screenshots of closed/open picker per width reviewed by the maintainer. `rg "sticky-picker"` and `rg "fhd:-mt"` return nothing unless the final design keeps them. | 0012.6f | Maintainer signs off the screenshot set. |

## Open questions (answer before implementation)

1. Slider placement on FHD+ (assumption above) — in the header row, or inside the open panel?
2. Should the open panel on FHD+ keep the page's reading column width for the *text* behind it dimmed (scrim) or not?
3. Is a breakpoint other than 1920px wanted for the one-row header (e.g. 1600px), now that the row only needs ~900px for the
   nav, picker and icons? Hover info in the margin needs real margins, the one-row header does not.

## Risks worth naming before starting

- **Portal/slot timing.** The picker is mounted by the page but rendered into the header; on route changes the slot must
  exist before the first render and must not flash an empty row. Mitigation: `AppShell` always renders the slot, pages
  use a layout effect to find it.
- **Two mount points, one component.** Below FHD the picker is in the page flow, above it in the header. A breakpoint
  check in JS (`matchMedia`) can disagree with CSS for one frame; use one source of truth (CSS container or a single
  `useMediaQuery`) and test resizing across 1920.
- **Overlay vs. sticky.** A `fixed` panel under a `sticky` header needs the header height as a CSS variable
  (`--app-header-h`, already present, changes at 48rem and 120rem); if the header height ever becomes content-driven the
  panel's top offset breaks.
- **Hover info regression.** The margin panel (`MarginWordInfo`, `fixed`) and the full-width overlay share the top area;
  z-order and `pointer-events` have to be checked together, not separately.
- **Left-aligned text between 1024 and 1919px** changes where the eye lands on laptop screens and moves the text when
  crossing 1920px; check the reader's verse anchors (`#v…` scroll) and the mapping editor, which has its own width.
- **Pinned panel vs. keyboard users.** Today hover info is mouse-only and the word menu is the accessible path; pin must
  also work on `Enter`/`Space` and keep focus order sane (panel after the word, `Esc` returns focus to the word).
- **Scope creep into the reader layout.** Removing the negative margins in `ChapterPage` changes the reader's vertical
  rhythm; keep the visual result of the chapter title and text identical.
