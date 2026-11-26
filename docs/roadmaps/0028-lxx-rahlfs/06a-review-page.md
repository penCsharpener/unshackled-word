# Review page `/edit/lxx` (frontend design, 0028.7f)

Draft for the implementer. The skeleton is in the repo:

- `src/UnshackledWord.Web/src/pages/LxxReviewPage.tsx`
- `src/UnshackledWord.Web/src/pages/lxxReview/*`
- `src/UnshackledWord.Web/src/lib/maintainer/lxxReview.ts`
- i18n `lxxReview.*` in `de.ts`/`en.ts`

The contracts are in `Domain.WebApi/Maintainer/LxxReviewContracts.cs`. Decisions from the backend core ([index.md](index.md), "0028.7f partial"):

- Chapters are **Rahlfs** chapters.
- The server decides `Source`. The page never sends one.
- Safe confirm covers only listed words with AI `high`. A `low` word never counts as safe, and an nt-form prefill alone is not safe.
- Discard keeps the nt-form proposals.

## Route, access, menu

- Route: `edit/lxx/:bookId?/:chapter?`, with `handle: { layout: "fill" }`. From md up, the page does not scroll: the list and the table scroll on their own.
- Without `bookId`, the page redirects to the first book with open work and its first open chapter. A `bookId` without a chapter takes the book's first open chapter.
- Search params (only per-viewer view state):
  - `f` is the filter: `open` (default), `low` (units with an AI `low` word) or `all`.
  - `form` is `og` / `th` / `both` (Daniel only, default `both`).
- Access follows `TskReviewPage`:
  - `loading` renders null.
  - `anonymous` sends the user to `/login`.
  - A user without `hasWriteRole` sees `admin.forbidden`.
- Menu: `UserMenu` (write role) gets the item `{ key: "lxxReview", to: "/edit/lxx", label: t("lxxReview.nav") }` after `tskReview`.

## Layout

```
Desktop (md+)
┌ header: title, intro, filter Select, form Select (Daniel), "Kapitel bestätigen (sichere)", "KI neu für Auswahl (n)" ┐
├──────────────┬──────────────────────────────────────────────────────────────────────────────────────────────┤
│ book list    │ unit card  ☐ Rut 1:1   [offen] [KI · Lauf 12] [2 unsicher]          Vers verwerfen  Vers bestätigen │
│  Ruth   12   │   Hebrew line (RTL, buttons): וַיְהִ֗י  בִּימֵי֙  שְׁפֹ֣ט ...                                     │
│   1  4 ·2    │   flags (FormError-styled list, muted): "Wort 25: kein Strong's in TBESG"                     │
│   2  8       │   table: # | Griechisch | Lemma | Strong's | Bestimmung | Hebräisch | Sicherheit | Quelle      │
│  1 Kgt  40   │          active row has accent left border; the editor row opens below the active row        │
│   …          │   note (proposal.note), FormError (save / 409), "gespeichert" StatusBadge                    │
│              │ unit card …                                                                                  │
└──────────────┴──────────────────────────────────────────────────────────────────────────────────────────────┘
```

- **Left (`ChapterList`):** a `nav` about `w-64`. It holds a `ul` of books: name from `useBibleStructure` + `bookName`, and the book's `openUnits`. Each book has a nested `ul` of chapters. Each chapter is a `Link` to `/edit/lxx/{book}/{chapter}?{params}` that shows the chapter number and `openUnits`. When the chapter has AI words, it also shows `lowWords` as muted text "2 unsicher".
  - Mark the current chapter with `aria-current="page"` and the accent token.
  - The chapter label carries the hint `lxxReview.list.rahlfs` ("Kapitel nach Rahlfs") once at the top. These are not the Elberfelder chapters.
- **Right:** the chapter's units in Swete order (`UnitCard`). Each card has these parts:
  - **Header:** a checkbox (native `input type="checkbox"` in a `label`, as in `UserRolesSection`) for the "KI neu" selection, and the `ref`. Badges as `StatusBadge`:
    - `open` / `confirmed` / `saved`;
    - `og`/`th` in Daniel;
    - `Zusatz` for additions;
    - `HasConfirmed` → "enthält bestätigte";
    - the proposal badge "KI · Lauf {{runId}}" with `formatStamp(createdAt)`.
  - **Actions:** `Button` secondary "Vers verwerfen" (only with a proposal) and primary "Vers bestätigen".
  - **Hebrew line (`HebrewLine`):** a `div dir="rtl" lang="he"` with `font-original text-2xl`, plus one `Button variant="inline"` per Hebrew word.
    - Its `title` is `Strongs · gloss · morphology`.
    - `aria-pressed` is true when the word is linked to the **active** Greek word.
    - Words linked to any word of the unit get the `bg-mark-same` token; words linked to the active word get the accent outline.
    - An addition (`hebrew` empty) shows "Zusatz ohne hebräische Vorlage" instead.
  - **Flags:** `proposal.flags` as a muted list. A flag with `position` scrolls to the word row when clicked.
  - **Table (`WordTable`):** a semantic `table`.
    - Columns: Position | Griechisch (`original-text`) | Lemma (`original-text`) | Strong's | Bestimmung | Hebräisch (the numbers of the linked words, e.g. `1, 3`, plus their text in `original-text`) | Sicherheit (`StatusBadge`: AI `high` → tone ok "sicher", `low` → tone warning "unsicher", no AI → none; `prefilled` adds the muted text "wie Vorbelegung") | Quelle (`StatusBadge` neutral: `nt-form` / `ai` / `human` + status `vorgeschlagen` / `bestätigt`).
    - A changed draft cell (different from the shown state) gets the `bg-mark-same` token and the sr-only text "geändert".
    - When the AI answer differs from the stored tag (e.g. an nt-form prefill corrected by the AI), the stored value is shown struck through under the AI value.
  - **Editor row (`WordEditorRow`):** a `tr` with one `td colSpan` under the active row. It is open while the active row is in edit mode. It holds `LemmaSearchField` and `MorphologyField` side by side (stacked on mobile), plus "Tag entfernen" (`Button` ghost; sets all three to empty, so the word is sent as links-only) and "Zurücksetzen" (back to the initial draft).
- **Mobile (< md):** the list becomes two `Select`s (book, chapter) under the header.
  - The header buttons wrap; "KI neu für Auswahl" stays visible as a sticky bottom bar (`sticky bottom-0` with `bg-surface`) while units are selected.
  - The table turns into one card per word: the `table` becomes a `ul`. Do not hide columns: every field stays. A card shows the Greek word large, with lemma/Strong's/code/Hebrew/confidence/source as a `dl` under it.
  - The Hebrew line wraps. The editor opens inside the card.
- **4k:** content `max-w-screen-2xl mx-auto`; the table never stretches beyond it.

## Components used (no new shared component)

| Part | Component |
|---|---|
| actions, Hebrew words, chapter jump, flag jump | `Button` (`primary` / `secondary` / `ghost` / `inline`), `Link` + `buttonClasses` |
| filter, form, mobile book/chapter, lexicon hits | `Select` |
| lexicon query, parse code | `TextField` (`size="sm"`, `type="search"` for the query, `invalid`) |
| cell and save errors, 409 | `FormError` |
| open/confirmed/saved, confidence, source, run | `StatusBadge` |
| empty book list, empty chapter, nothing open | `EmptyState` |
| discard confirm, safe-confirm confirm | `Dialog` |
| results of confirm / discard / safe confirm | `Toast` (`useToast`) |
| "KI neu für Auswahl" | `AgentRunDialog` with two **new optional props**, see below |

`AgentRunDialog` needs two props. They are prop extensions of an existing component (the CLAUDE.md rule "prefer a prop/variant"), so they need no new component. They are added in the skeleton and noted in `Components.md`:

- `modes?: AgentMode[]`: the offered modes, default `["redo", "fill-open", "lemma"]`; the first is preselected. `AgentMode` gains `"lxx-tag"`.
- `reviewHref?: (runId: number) => string`: the target of "Vorschläge prüfen", default `/edit/proposals/{runId}`.

**Page-local subcomponents** (under `pages/lxxReview/`, not shared, so `Components.md` is not touched): `ChapterList`, `UnitCard`, `HebrewLine`, `WordTable`, `WordEditorRow`, `LemmaSearchField`, `MorphologyField`, `SafeConfirmDialog`, `DiscardDialog`. If one is later needed elsewhere, propose it to the maintainer first and only then move it to `components/`.

## Draft model (page state)

`useUnitDrafts(units)` (`lib/maintainer/lxxReview.ts`) keeps a `Map<lxxVerseId, UnitDraft>`. A `UnitDraft` holds:

- `proposalId` (as shown);
- `words: Map<lxxWordId, WordDraft>`, where `WordDraft` = `{ lemma, strongs, morphology, hebrewWordIds }`;
- `saveState`: `idle` | `saving` | `saved` | `error` | `conflict`;
- `error?`.

`initialWordDraft(word)` picks the first match:

- the AI answer when one exists (`lemma`, `strongs`, `morphology`, `hebrewWordIds`);
- else the stored tag + stored links;
- else empty strings + stored links.

So the page shows what "Vers bestätigen" would store when nothing is touched. This is how the AI proposal gets reviewed.

The draft resets for a unit when the server's `proposalId` or the words' stored state changes (refetch after save, or a newer run). A dirty draft is **not** reset silently: it switches to `conflict` and asks.

Validation per word (`validateWordDraft`, pure, client side, mirrors the contract):

- all of lemma, Strong's and code, or none (error `lxxReview.error.partial`);
- Strong's must match `^G\d{4,5}[A-Z]?$` (five digits allowed, see the 0028.7 finding) → `lxxReview.error.strongs`;
- the code must be valid according to the live check (`useLxxMorphologyCheck`) → `lxxReview.error.morphology` with the server's `error` text appended;
- every `hebrewWordId` must be one of `unit.hebrew` (otherwise ignored; it can only happen after a refetch).

The errors show as `FormError` inside the editor row and as `invalid` + `aria-invalid` on the cell. "Vers bestätigen" is disabled while a word of the unit has an error, and the button's `title` says how many.

## Editing interactions

- **Active word:** clicking a row or focusing one of its cells makes it active (one per page; it starts at the first word of the first unit). The active row gets the accent left border. The Hebrew line of *its* unit shows its links.
- **Links:** clicking a Hebrew word toggles its id in the active word's `hebrewWordIds` (order: Hebrew reading order). Several Greek words may link to one Hebrew word, and one Greek word to several Hebrew words.
- **Lemma + Strong's (`LemmaSearchField`):** they change together, because the server checks that the lemma is the TBESG lemma of the Strong's number.
  - A `TextField type="search"` takes a Strong's number (`G746`, `g0746`) or the start of a lemma, accents optional.
  - The query is debounced 250 ms and needs at least 1 character for `G\d` or 2 otherwise. `useLxxLexicon(q)` loads up to 20 hits.
  - The hits go into a `Select` (label "Eintrag"), each shown as `G0746 · ἀρχή · beginning`. Choosing a hit sets `lemma` + `strongs`.
  - Enter in the query field takes the first hit when it is the only one.
  - With no hit: muted text "Kein TBESG-Eintrag" plus a hint that such words (names like Μωάβ) stay without a tag and get only links ("Tag entfernen").
- **Parse (`MorphologyField`):** a `TextField size="sm"`. It is upper-cased on blur, not while typing, so crasis codes with spaces and `+` survive.
  - Live check (`useLxxMorphologyCheck(code)`), debounced 300 ms. It shows the German (de) or English (en) description under the field when valid, or `FormError` with the server's error when not.
  - Empty is allowed only together with an empty lemma/Strong's.
- **Keyboard:** handlers sit on the chapter container. They react only when focus is **not** inside an input/select/textarea, except `Esc` and `Ctrl/Cmd+Enter`.

| Key | Action |
|---|---|
| `j` / `ArrowDown`, `k` / `ArrowUp` | next / previous word (across units) |
| `e` or `Enter` | open the editor row of the active word, focus the lexicon query |
| `p` | open the editor row, focus the parse code |
| `1`–`9`, `0` = 10 | toggle Hebrew word n of the active unit; two digits typed within 600 ms make one number (`1`,`2` → 12) |
| `x` | toggle the unit in the "KI neu" selection |
| `Esc` | close the editor row (keeps the draft), focus goes back to the row |
| `Ctrl/Cmd+Enter` | "Vers bestätigen" for the active unit |

The keys are listed once under the header as a collapsible `details` "Tastatur" (`lxxReview.keys.*`). The Hebrew words are also reachable by Tab (they are `Button`s).

## Actions and request bodies

All POSTs go through `api.post`, with paths relative to the API base (`/maintainer/lxx/...`).

- **Load:**
  - `GET /maintainer/lxx/review/books` → `LxxReviewBooksResponse`;
  - `GET /maintainer/lxx/review/{bibleBookId}/{chapter}` → `LxxReviewChapterResponse`;
  - `GET /maintainer/lxx/lexicon?q={encodeURIComponent(q)}&limit=20`;
  - `GET /maintainer/lxx/morphology?code={encodeURIComponent(code)}` (query parameter because of spaces and `+`).
- **Vers bestätigen** → `POST /maintainer/lxx/review/confirm`, with **every** word of the unit:

  ```json
  { "lxxVerseId": 812, "proposalId": 4711,
    "words": [ { "lxxWordId": 90001, "lemma": "καί", "strongs": "G2532", "morphology": "CONJ", "hebrewWordIds": [3301] },
               { "lxxWordId": 90025, "hebrewWordIds": [3310] } ] }
  ```

  - `proposalId` is the id shown (or `null`/omitted without a proposal).
  - A word whose three tag fields are empty is sent without them (links only; the stored tag stays as it is). Strings are trimmed; the code is upper-cased.
  - Response `ConfirmLxxUnitResponse`. With `action === ""` the toast says "Keine Änderung"; otherwise it says "Vers bestätigt ({{count}} Wörter)", plus "(bearbeitet übernommen)" for `edited-merged`.
- **Vers verwerfen** (only with a proposal): `Dialog` "KI-Vorschlag verwerfen?" with the text "Die NT-Form-Vorbelegungen bleiben erhalten." → `POST /maintainer/lxx/review/discard` with `{ "lxxVerseId": 812, "proposalId": 4711 }`. The draft of the unit resets to the stored state after the refetch.
- **Kapitel bestätigen (sichere)** → first a `Dialog` with the counts ("{{words}} sichere Wörter in {{units}} Versen bestätigen; {{low}} unsichere bleiben offen; {{dirty}} Verse mit Änderungen ausgelassen"), then `POST /maintainer/lxx/review/confirm-chapter-safe`:

  ```json
  { "bibleBookId": 8, "chapter": 1,
    "units": [ { "lxxVerseId": 812, "proposalId": 4711, "lxxWordIds": [90001, 90002] } ] }
  ```

  `safeUnits(units, drafts, visibleIds)` builds the list. "Seen on screen" means the units rendered under the current filter/form (`visibleIds`), each with a proposal, **no dirty draft** (dirty units must be confirmed per unit) and no `conflict`. The words listed are those with `ai.confidence === "high"`, a tag that is not `confirmed`, and a draft equal to the AI answer.

  The server re-checks the confidence. The response's `units`, `confirmedWords`, `skippedLow`, `skippedOther`, `mergedProposals` go into one toast. `staleUnits` mark those cards as `conflict`. The button is disabled when the list is empty.
- **KI neu für Auswahl (n):** see below.

## States

| State | Display |
|---|---|
| books loading | list skeleton: three muted `li` lines; the right side shows nothing until the redirect |
| books error | `FormError lxxReview.error.books` + `Button` "Erneut versuchen" (`refetch`) |
| no book with open work | `EmptyState title=lxxReview.empty.books` ("Keine offenen LXX-Vorschläge") with a hint to Wartung > LXX (prefill) or "KI neu" in the reader; the page still allows `/edit/lxx/{b}/{c}` directly (filter `all`) |
| chapter loading | the table area shows `EmptyState variant="comingSoon"` skeleton rows |
| chapter 404 / error | `FormError lxxReview.error.chapter` (404: "Dieses Kapitel gibt es in der LXX nicht.") |
| empty chapter (no units) | `EmptyState lxxReview.empty.chapter` |
| no open proposals (filter `open`/`low`, nothing matches) | `EmptyState lxxReview.empty.open` + `Button` "Alle Verse zeigen" (sets `f=all`) |
| validation error per cell | `invalid` on the field, `FormError` in the editor row, a count in the confirm `title`, the row's sr-only "fehlerhaft" |
| saving | the card's buttons are disabled; the primary button shows "Speichert …"; other cards stay usable (one mutation per card; keep per-unit `saveState`, not a single `isPending`) |
| conflict / 409 | the card shows `FormError lxxReview.error.conflict` ("Der KI-Vorschlag wurde inzwischen ersetzt oder geschlossen.") + `Button` "Neu laden" (refetch the chapter, then reset this unit's draft); the draft is kept until then; safe confirm skips it |
| saved | `StatusBadge tone="ok"` "gespeichert" on the card + toast; the unit stays visible in this session even when the filter `open` would now hide it (session set `keptVisible`), so the list does not jump |
| run started | `AgentRunDialog` progress; when it closes, invalidate the review keys |

## React Query keys and invalidation

Keys live in `lxxReviewKeys` under the root `["maintainer", "lxxReview"]`. Do **not** put them under `["maintainer", "lxx"]`: that is the exact key of `lxxMaintainerKeys.status`, and a prefix invalidation of the status would refetch every review query.

| Key | Query |
|---|---|
| `["maintainer","lxxReview","books"]` | books |
| `["maintainer","lxxReview","chapter",bookId,chapter]` | chapter (`staleTime 0`, no polling) |
| `["maintainer","lxxReview","lexicon",q]` | lexicon (`staleTime 5 min`, `placeholderData: keepPreviousData`) |
| `["maintainer","lxxReview","morphology",code]` | morphology check (`staleTime Infinity`: the parser is static) |

After confirm, discard and safe confirm (`onSuccess`, also on 409 for the chapter), `invalidateAfterReview(queryClient, bookId, chapter)` invalidates:

- the review chapter key and the books key;
- `lxxMaintainerKeys.status` (Wartung card counts);
- the reader's prefix keys `["bible","chapterLxx"]` and `["bible","lxxWord"]` through `invalidateReaderLxx` from `lib/maintainer/lxx.ts`.

After the `AgentRunDialog` closes: review chapter + books (the run creates proposals). `agentKeys.runs` is refreshed by the dialog itself.

## "KI neu für Auswahl"

- **Selection:** the unit checkboxes or `x`. Shift+click selects the range from the last clicked unit. The button label shows the count; with no selection it is disabled.
- **Request:** the agent run API takes one verse range, so `selectionRange(units, selectedIds)` builds `{ book: bibleBookId, fromChapter: chapter, fromVerse: min(verse), toChapter: chapter, toVerse: max(verse) }` (Rahlfs, both inclusive; subverses and both Daniel forms of the boundary verses come with it). This goes to `AgentRunDialog` as `initial`, with `modes={["lxx-tag"]}`, `bookName`, and `reviewHref={() => "/edit/lxx/{book}/{chapter}"}`.
- **Before opening,** the page shows a short notice in the dialog intro. It is passed as part of the page, above the dialog trigger, as `lxxReview.ai.notice`:
  - when the range includes units that were not selected ("Der Bereich V. {{from}}–{{to}} umfasst auch {{extra}} nicht gewählte Verse.");
  - when selected units have confirmed tags ("{{count}} Verse mit bestätigten Tags werden übersprungen.").
- **Cost and start:** the dialog loads `POST /maintainer/agent-runs/estimate` with `{ book, fromChapter, fromVerse, toChapter, toVerse, profile, mode: "lxx-tag", model? }` as it does today, and shows packages/tokens/cost. Only the maintainer's click on "Starten" sends `POST /maintainer/agent-runs` (the same body + `maxTokens?`/`maxCostUsd?`). Nothing on the page starts a run by itself; opening the dialog only estimates.

## i18n keys (namespace `lxxReview`, added to `de.ts`/`en.ts`)

The full list with both languages is in the skeleton (`i18n/de.ts`, `i18n/en.ts`, block `lxxReview`). The main groups are:

- `nav`/`title`/`intro`
- `list.*` (`rahlfs`, `open`, `low`, `book`, `chapter`)
- `filter.*` (`label`, `open`, `low`, `all`), `form.*`
- `col.*` (table headers)
- `badge.*` (`open`, `confirmed`, `saved`, `addition`, `hasConfirmed`, `run`, `high`, `low`, `prefilled`, `changed`)
- `source.*` / `status.*`
- `hebrew.*` (`addition`, `linked`)
- `edit.*` (`query`, `entry`, `noEntry`, `noEntryHint`, `code`, `clearTag`, `reset`)
- `action.*` (`confirm`, `discard`, `confirmSafe`, `aiRedo`, `saving`, `reload`, `showAll`, `retry`)
- `discard.*`, `safe.*`
- `done.*` (toasts)
- `error.*` (`books`, `chapter`, `notFound`, `partial`, `strongs`, `morphology`, `conflict`, `save`, `invalidCount`)
- `empty.*`
- `ai.*` (`extra`, `skipConfirmed`)
- `keys.*`

In addition, `agent.mode["lxx-tag"]` ("LXX taggen" / "Tag LXX") and `agent.dialog.modeHint["lxx-tag"]`.

## Maintainer decisions (2026-11-26)

1. **"auch bestätigte" checkbox:** "KI neu für Auswahl" offers "auch bestätigte Verse neu taggen" (en "also re-tag confirmed verses"), default off. It sits in `AgentRunDialog` (mode `lxx-tag`) and is sent as `includeConfirmed` with estimate and start (backend field on `AgentRunRangeRequest`, stored on the run by migration 0053, `AgentWorker --include-confirmed`).
2. **Gaps:** a selection with gaps runs as one run per contiguous block of verses. Each block has its own estimate, the dialog lists the blocks and shows the summed estimate; "Lauf starten" posts the blocks one after the other (the next one when the previous run completed). No unselected verse is ever included, so `selectionRange` returns `blocks` and the earlier "extra units" notice is gone.
3. **Daniel:** a run covers only the text form shown on screen: the `form` filter `og`/`th` goes along as `textForm` (backend: `AgentRunRangeRequest.TextForm`, 400 for other values or other modes, the LXX builder builds only units of that form). With `form=both` both forms are run.
4. **Default filter:** `open` (every verse with an open proposal, nt-form or AI); `low` is a switchable filter.

Deviations of the build from the sketch above: `ai.extra` was replaced by `ai.selection` / `ai.skipConfirmed` / `ai.daniel` (shown under the header buttons, not in the dialog); `AgentRunDialog` got two more optional props, `blocks` and `textForm`; `onMorphologyValidity` carries the code too (validity is keyed by word and code); the mobile layout is chosen by `useMediaQuery` instead of CSS-hidden duplicates.
