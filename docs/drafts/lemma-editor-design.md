# Lemma editor – "Lemmata bearbeiten" (design draft)

Status: draft, nothing implemented. Mockup: `docs/drafts/lemma-editor.html` (one responsive file; toolbar switches theme, a 375 px frame and the states Joh 1,1–5 / large chapter with ≈ 4 400 rows / no chapter / loading / load error / logged out, plus a checkbox that makes the next save return a conflict). Sample data is Joh 1,1–5 (Elb 1871) with planted problems: 6 missing lemmas, one missing part of speech ("eines,"), one AI value that the part of speech does not allow (Tempus on "Licht", 1,4).

## Scope

A tool for logged-in users to correct the German lemma and morphology of every Elb1871 word of one chapter. Not public, no reader features. Out of scope (only hinted at): review/merge queue, provenance per value, "KI diesen Versbereich neu taggen lassen".

## Entry and route

- **Menu entry** "Lemmata bearbeiten" in the `UserMenu` dropdown (rendered only when logged in, like "Einladungen" for admins), between "Passkeys" and admin entries. Not in the top nav: it's a tool for the few contributors, and the header is already full on phones. Move it to the top nav once there are more editing tools (open question 1).
- **Route** `/edit/lemmas/:bookId/:chapter` (plus `/edit/lemmas` with no chapter). Guarded: logged out shows an `EmptyState` "Anmeldung erforderlich" with a sign-in button that returns here after login (no redirect away; deep links keep working).
- Later: a small "Lemmata dieses Kapitels bearbeiten" link on `ChapterPage` for logged-in users.

## Page layout (desktop / tablet ≥ 48rem)

App-like layout: the page itself does not scroll, only the grid does. Top to bottom:

1. **Header**: `h1` "Lemmata bearbeiten", summary line "Johannes 1 · 63 Wörter · 6 ohne Lemma · 1 Unstimmigkeit", and a muted hint on the editing model (below).
2. **`BibleChapterPicker`** (existing OT/NT bar and book grid), with chapter links pointing at the editor route.
3. **Filter bar**: search, part-of-speech filter, "Nur ohne Lemma (n)" toggle, "Spalten" menu, "Tastatur" help, "zeige x von y".
4. **Context line**: the verse of the active row in `font-scripture`, with the active word marked (`mark-same`). Lemma decisions need context; this avoids an extra verse-text column.
5. **Grid** (fills the remaining height, own scroll container).
6. **Save bar** (appears as soon as something is dirty).

**Why the grid gets its own scroll container:** a horizontally scrolling wrapper breaks `position: sticky` against the page, and both sticky axes are needed. A fixed-height scroller keeps the header row and the left columns sticky, and it is what virtualization needs anyway. The app header (`--app-header-h`) stays visible.

## Grid

**One row per word, one column set**, in this order:

| Col | Label (de) | Source | Edit | Width |
|---|---|---|---|---|
| gutter | Vers | `Verse` | – | 3.5rem, sticky |
| ctx | Wort im Kontext | `Elb1871Words.WordInContext` | read-only | 10.5rem, sticky, `font-scripture` |
| plain | Wort (bereinigt) | `Elb1871Words.PlainWord` | read-only | 9rem, hidden by default below 90rem |
| lemma | Lemma | `Elb1871Morphology.Lemma` | text | 10.5rem |
| upos | Wortart | `PartOfSpeech` | dropdown | 8.5rem |
| Degree … Gender | Steigerung, Verbform, Tempus, Person, Numerus, Modus, Kasus, Genus | morphology | dropdown | 7rem each |

- **Sticky**: the header row (top) plus the verse gutter and "Wort im Kontext" (left). On a 1280 px screen the full set is ≈ 88rem wide, so it scrolls sideways a little; the word you're editing never scrolls out of view.
- **Verse grouping**: the verse number shows only on a verse's first word, with a 2 px border between verses. Each verse is an ARIA `rowgroup` labelled "Vers n". No separate verse rows, so up/down moves word by word without gaps.
- **Display vs. editor**: cells show plain text (full German label). Only the active cell mounts a real `<input>` / `<select>`. With 4 400 rows × 9 controls, real controls in every cell would mean ~40 000 form elements, so this is a performance decision first.
- **Labels and codes**: the DB stores codes; the UI shows the full label, lower-case as delivered ("präsens", "substantiv"). The code is in the cell's `title` ("präsens (Pres)") and in the open dropdown ("präsens · Pres"). Code→label maps come from the backend (one request, cached with React Query `staleTime: Infinity`).
- **Virtualization**: no new dependency at first. Rows are div-based (`role="grid"`), and each verse `rowgroup` gets `content-visibility: auto` with `contain-intrinsic-size`, so the browser skips layout and paint of off-screen verses. This works on `div`s, but not on table row groups, which is why there's no `<table>`. The mockup's ≈ 4 400-row chapter (Ps 119 size) renders and jumps to the end (Ctrl+End) in ~10 ms. If the real React version stutters on Ps 119, ask to add `@tanstack/react-virtual` (open question 4).

### Fields per part of speech

One rule table, served by the backend together with the label maps (`allowedFeatures[pos]`) so that the UI and the save validation agree:

| Wortart | Allowed |
|---|---|
| verb, hilfsverb | Verbform; if `finit` also Tempus, Person, Numerus, Modus |
| substantiv, eigenname, artikel | Numerus, Kasus, Genus |
| adjektiv | Steigerung, Numerus, Kasus, Genus |
| adverb | Steigerung |
| pronomen | Person, Numerus, Kasus, Genus |
| numerale | Kasus, Genus |
| präposition, konjunktion, subjunktion, partikel, interjektion, (empty) | – |

- A field that isn't allowed and is empty shows a muted "–" with `aria-disabled` and a tooltip "für substantiv nicht vorgesehen". Arrow keys still land on it, so movement stays in a straight line; Tab skips it.
- A field that isn't allowed but **has a value** (AI error) is never hidden: it shows "⚠ ~~präsens~~", is counted as an "Unstimmigkeit", and Delete clears it.
- Changing Wortart (or Verbform away from `finit`) clears the fields that are no longer allowed, right away. The change shows up as dirty cells and can be reverted with "Zeile zurücksetzen" or "Verwerfen".

### Dirty, invalid, missing

- **Dirty row**: 3 px accent bar on the gutter, plus a "↺" button (reset row) and `sr-only` "geändert".
- **Dirty cell**: `mark-linked` background; tooltip "vorher: dativ (Dat)"; `sr-only` ", geändert". Colour is never the only signal.
- **Missing lemma / Wortart**: italic muted "fehlt" with a dashed underline.
- No new colour tokens: dirty uses `mark-linked`, error and conflict use `accent` (as `FormError` already does).

## Save model: batch save bar

**Decision: batch.** One sticky bar at the bottom: "**3** Wörter geändert · Wird als Vorschlag gespeichert (Strg+S)" with `Verwerfen` (ghost) and `Vorschlag speichern` (primary).

Why not per-row autosave:

- The editing model is suggest → maintainer merges. One save = one changeset (like one patch), which is far easier to review than dozens of one-field versions.
- Correcting a chapter is a pass. People change Wortart, then two features, then go back, and autosave would version every intermediate state.
- Fewer requests and one place for conflicts.

Details:

- `Verwerfen` asks in the bar itself ("3 Änderungen wirklich verwerfen? · Ja, verwerfen · Abbrechen", with focus on Abbrechen). No modal.
- Leaving with unsaved changes (chapter picker, route change, tab close) uses React Router's `useBlocker` plus `beforeunload`.
- **Saving**: the button shows "Speichern …" and is disabled; editing stays possible.
- **Success**: "✓ 3 Änderungen als Vorschlag gespeichert" for a few seconds (`aria-live="polite"`).
- **Error**: the bar keeps the changes and shows a `FormError` with "Erneut versuchen".
- The request sends only changed fields per word, plus the version (or `UpdatedAt`) the client loaded.

### Conflicts

Optimistic concurrency per word. The server saves the words without conflicts and returns the rest. A conflict row gets an accent outline, "!" in the gutter and a note below the row: "KI-Mapping hat am 12.11.2026, 10:14 das Lemma auf »derselbe« gesetzt. Dein Wert: »dasselbe«." with `Meinen Wert behalten` / `Serverwert übernehmen`. Saving is disabled until every conflict is resolved, and the bar says how many are left.

## Editing-model hint (not built)

- Muted line under the title: "Gespeicherte Änderungen sind Vorschläge unter deinem Namen; übernommen werden sie, wenn der Maintainer sie zusammenführt."
- The primary button says "Vorschlag speichern". For the maintainer role it could say "Speichern" (open question 2).
- Reserved for later: a provenance mark per row (KI / bestätigt / Vorschlag offen), the same vocabulary as the `WordDetailPopover` badge, plus a toolbar action "KI diesen Versbereich neu taggen lassen".

## Keyboard (grid, roving tabindex)

| Key | Action |
|---|---|
| ↑ ↓ ← → | move between cells |
| Enter / F2 / double-click | edit; in the editor Enter commits and moves down (Shift+Enter up) |
| Tab / Shift+Tab | next/previous **editable** cell, wrapping to the next row (spreadsheet style); in the editor it commits first |
| a letter on Lemma | starts typing (replaces the value) |
| a letter on a dropdown cell | type-to-pick without opening: "p" → präposition, again "p" → pronomen …; matches the label or the code |
| Delete / Backspace | clear the value (also clears invalid values) |
| Esc | cancel the edit |
| Page ↑/↓, Ctrl+Home/End, Home/End | page by 15 rows, first/last row, first/last column |
| Ctrl+S | save |

Mouse: a click on a dropdown cell opens it right away; Lemma opens on a second click or a double-click (so a first click only selects). Lemma inputs get a `<datalist>` of the lemmas already used in the chapter.

Tab staying inside the grid is a deliberate data-entry choice. Leaving the grid works with the mouse or Shift+Tab from the first editable cell. Open question 5 asks whether a skip link "zur Speicherleiste" is needed.

## Filters

- **Suchen**: substring match, case-insensitive, on word in context and lemma.
- **Wortart**: alle / ohne wortart / each part of speech.
- **Nur ohne Lemma (n)**: toggle `Button` with `aria-pressed`.
- **Filters freeze while editing**: a row that stops matching after an edit (for example, you type a lemma while "Nur ohne Lemma" is on) stays in the list until the filter changes. Otherwise rows would vanish under the cursor.
- **No match**: `EmptyState` "Keine Wörter passen zum Filter" with "Filter zurücksetzen".
- Filters reset when the chapter changes.

## Columns

The "Spalten (n/13) ▾" menu has checkboxes for Wort (bereinigt) and the eight morphology columns. Vers, Wort im Kontext, Lemma and Wortart can't be hidden. The default hides "Wort (bereinigt)" below 90rem; it differs from the word in context only by punctuation and is in the context tooltip anyway. The choice is saved in localStorage.

## States

| State | UI |
|---|---|
| no chapter | `EmptyState` "Kein Kapitel gewählt" under the picker |
| loading | 14 skeleton rows with real column widths, `aria-busy`, context line "Kapitel wird geladen …" |
| load error | `EmptyState` with `FormError` text and "Erneut versuchen" |
| logged out / session expired | `EmptyState` "Anmeldung erforderlich" with sign-in button. On a 401 while saving, keep the changes in memory and show "Sitzung abgelaufen – anmelden und erneut speichern" |
| saving / saved / save error / conflict | see Save model |

## Phone (< 48rem container): one card per word

**Decision: cards, not horizontal scroll.** On 343 px, a sticky word column leaves about 200 px, which is one other column at a time, so you can't see a word's morphology together. Cards show only the fields the part of speech allows, which is 0–5 dropdowns instead of 8:

- Card header: "1,4 · **Licht** · Licht", plus a "geändert" badge and "↺".
- Lemma: full-width input with the datalist. Wortart: full-width select.
- The allowed features sit in a 2-column grid of native selects, each `min-height: 2.75rem` (touch target), with labels above.
- An invalid value appears as one line, "⚠ Tempus: präsens ist für substantiv nicht vorgesehen [Leeren]".
- Verse headings "Vers 4" are sticky under the app header. Each verse is a `content-visibility: auto` group.
- The page scrolls normally on phones (the browser bar collapses). The save bar is sticky at the bottom; the column menu and keyboard help are hidden.
- Layout switches on container width. Only one of the two DOMs is rendered, never both.

## Accessibility

- `role="grid"` with `aria-rowcount` / `aria-colcount` and `aria-rowindex` on each row. The verse gutter is a `rowheader`; header cells are `columnheader`s.
- One tab stop for the whole grid (roving `tabindex`). The editor control gets `aria-label` "Kasus für Gott,".
- Not-applicable cells use `aria-disabled`. Invalid and changed states have `sr-only` text, never colour alone. The focus ring is the `focus` token, inset 2px.
- The save bar is a `region` "Änderungen" with an `aria-live="polite"` message; the result count is live too.
- The context line is not live (it would be too chatty). Screen-reader users get the verse via the row header and the word cell.
- Light and dark use only existing tokens. `mark-linked` with `fg` passes contrast in both themes.

## Components

**Reused as is:** `AppShell`, `EmptyState` (no chapter / error / logged out / no match), `FormError`, `Button` (all actions), `Select` (part-of-speech filter, card dropdowns), `TextField` (search, card lemma).

**Props / variants to add (per the "prefer a prop" rule):**

| Component | Change |
|---|---|
| `BibleChapterPicker` | `chapterHref?: (ref: ChapterRef) => string` (default `chapterPath`), so the links go to `/edit/lemmas/:b/:c` |
| `UserMenu` | menu item "Lemmata bearbeiten" for every logged-in user |
| `Button` | visual pressed state for `aria-pressed="true"` on `secondary` (filter toggle; today it is only an attribute without style) |
| `Select` | `labelHidden` (sr-only label, for the in-cell editor), `invalid` style, option `title` (code tooltip); optional `selectRef` / autofocus for the cell editor |
| `TextField` | `size` (`sm` / `md`, like `Select`) and `labelHidden`; `list` already passes through as a native attribute |
| `AppShell` | way for a route to opt into the full-height layout without page padding at the bottom (e.g. route `handle: { layout: "fill" }`) |

**New components, proposed (not built, to confirm in chat):**

| Name | Purpose |
|---|---|
| `MorphologyGrid` | The editor grid: div-based ARIA grid, sticky header and left columns, verse rowgroups with `content-visibility`, active-cell editor, keyboard map, dirty/invalid/missing/conflict rendering; renders the card layout below 48rem (one component with two layouts, no separate card component). Props `words`, `labels`, `allowedFeatures`, `hiddenColumns`, `edits`, `conflicts`, `onEdit(wordId, field, value)`, `onResetRow`, `onResolveConflict`, `onActiveWordChange`. |
| `SaveBar` | Sticky pending-changes bar: count, hint, Discard with inline confirm, primary save, saving/success/error/conflict messages. Generic, so `MappingEditor` could use it later. Props `count`, `status`, `message`, `saveLabel`, `onSave`, `onDiscard`, `disabled`. |
| `ToggleMenu` | Button plus popover list of checkboxes (column visibility; reusable for other "show/hide" menus). Props `label`, `items` (`{ key, label, checked, disabled? }`), `onChange(key, checked)`, `onReset`. |

State (edits, filters, hidden columns) lives in the page plus a small `lib/lemmaEditor` module (dirty diff, allowed-feature rules, filter freezing), so the grid stays dumb and unit-testable.

## Data needed (backend, for the roadmap)

- `GET /api/elberfelder/{bookId}/{chapter}/morphology` returns words with `id, verse, position, wordInContext, plainWord, lemma, partOfSpeech, degree, verbForm, tense, person, number, mood, case, gender, version`.
- `GET /api/elberfelder/morphology/labels?lang=de` returns `{ fields: { partOfSpeech: [{ code, label }], … }, allowedFeatures: { [pos]: string[] }, finiteOnly: [...] }`.
- `PUT …/morphology` (login required) takes `[{ id, version, changes: { field: value } }]` and returns `{ saved: [...], conflicts: [{ id, server, changedBy, changedAt }] }`. The server re-checks `allowedFeatures` (FluentValidation).
- `Elb1871Morphology` does not exist in the migrations yet (today `Lemma` / `PartOfSpeech` / `GrammaticalKey` sit on `Elb1871Words`), so it needs a new numbered migration script. The mockup assumes Universal Dependencies codes (`NOUN`, `Pres`, `Dat` …) because the requested columns match UD feature names exactly.

## Open questions

1. Menu entry only in `UserMenu`, or also in the top nav for logged-in users?
2. Does the maintainer save directly ("Speichern") while everyone else creates a suggestion? And is a suggestion visible to others before it's merged?
3. Codes: UD (`NOUN`, `Pres`, `Sub`) or a German tagset (STTS)? And tense: only UD's präsens/präteritum (perfekt etc. analytic over hilfsverb + partizip), or the six traditional tenses on the main verb?
4. Is `content-visibility` enough for Ps 119, or may I add `@tanstack/react-virtual` if needed?
5. Tab: keep spreadsheet-style Tab inside the grid (current proposal), or follow the strict ARIA grid pattern where Tab leaves the grid?
6. Should the participle (partizip) get Kasus/Genus/Numerus when used as an adjective, or is that tagged as adjektiv?
7. Bulk action "Lemma für alle »ward« in diesem Kapitel übernehmen": wanted in v1?
8. Should labels in the English UI stay German (they are German grammar terms for a German text), or be translated by the backend's `lang`?
