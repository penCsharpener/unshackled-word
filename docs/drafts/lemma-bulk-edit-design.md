# Lemma editor – bulk lemma replace (design draft)

Status: draft, nothing implemented. Builds on `docs/drafts/lemma-editor-design.md` and the slice that is already in the code (`Endpoints/Lemmas`, `LemmaEditorPage`, `MorphologyGrid`, `SaveBar`, `lib/lemmas/*`, migration `0009_LemmaEdits.sql`). It answers open question 7 of that draft ("Lemma für alle »ward« … übernehmen").

## The proposal

Next to the Lemma column, a bulk-edit button opens a popup where you enter the correct lemma (`Jesu` → `Jesus`). A second, centred popup then lists every occurrence of the old lemma (whole Bible or a smaller scope) with the proposed replacement. You can deselect the occurrences that don't fit and confirm. Each row has a button that loads its full verse when you ask for it. The list is virtualized with `@tanstack/react-virtual`.

## What the dev DB says (checked read-only on `postgres001`)

| Fact | Value | Consequence |
|---|---|---|
| `Elb1871Words` rows | 722 753 | |
| `Elb1871Morphology` rows | 721 552 (1 201 words have no row) | Bulk edit only touches existing rows. A word without a morphology row has no lemma to match. |
| Distinct lemmas | 18 732 | |
| `Jesu` / `Jesum` / `Jesus` | 244 / 115 / 580, all `PROPN` | The real fix covers **two** source lemmas, so the first step must make `Jesum` visible too. |
| `Jesu`: `Case`/`Number` | all `Gen`/`S` | The morphology is right. Only the lemma copied the inflected form, so **replace the lemma only**. |
| `Jesu`: `WordInContext` variants | 6 (`Jesu`, `Jesu,`, `Jesu.`, `Jesu:` …) | Group by `PlainWord`, not `WordInContext`. |
| Largest lemmas | `der` 68 777, `und` 54 176, `sein` 26 422 | The occurrence list needs a cap and per-book counts. |
| Lemmas that differ only in case | 529 groups (e.g. `Herr`/`HERR`) | Match the exact (case-sensitive) lemma. Show case variants as hints, never merge them silently. |
| Capitalised lemma that is not `NOUN`/`PROPN` | 6 029 | Typical next bulk job (sentence-initial capitals): a part-of-speech filter is useful. |
| Index on `Elb1871Morphology."Lemma"` | **none** (only PK and `UQ_…(HebRefId, PositionInVerse)`) | Every lookup is a parallel seq scan of the 51 MB table. |
| Join index | `IDX_Elb1871Words_HebRef_Position` on `Elb1871Words` | The join is fine. |
| `Elb1871Words.Lemma` (legacy) for the `Jesu` rows | empty | Bulk edit writes `Elb1871Morphology` only, like the existing slice. |
| `Elb1871MorphologyEdits` rows | 0 | No audit data yet, so the table can still change shape freely. |

`EXPLAIN ANALYZE` of the occurrence query (morphology ⨝ words on `HebRefId, PositionInVerse`, `WHERE m."Lemma" = $1`, ordered by book/chapter/verse/position):

| Lemma | Rows | Plan | Time |
|---|---|---|---|
| `Jesu` | 244 | Parallel Seq Scan on Morphology → Index Scan `IDX_Elb1871Words_HebRef_Position` | 25 ms |
| `der` | 68 777 | Parallel Seq Scan on both → Parallel Hash Join | ≈ 100 ms |
| `Jesu`, with ±5-word context (2 `LATERAL string_agg` on `Elb1871RefAndPositions_unique`), `LIMIT 2000` | 244 | as above | 57 ms |
| `der`, with context, `LIMIT 2000` | 2 000 | as above | 70 ms |

Fast enough even today. An index on `"Lemma"` turns the `Jesu` case into an index scan of under 1 ms and makes the counts and autocomplete endpoint cheap, so it is worth adding (see Data).

## Alternatives and decisions

| Question | Options | Decision | Why |
|---|---|---|---|
| **What matches** | a) lemma only · b) lemma + part of speech · c) lemma + PoS + full morphology | **Exact lemma, plus optional filters for part of speech and form (`PlainWord`)** | (c) is too narrow: `Jesu` is always Gen/S, but `Herr` spans every case, and the morphology is a property of the occurrence, not of the lemma. (a) is the common case, and the PoS filter covers `Licht` ADJ vs NOUN. Grouping (below) handles the rest by eye. |
| **Case sensitivity** | exact / `lower()` | **Exact.** The first step shows case variants and related lemmas with counts | 529 lemma groups differ only in case, and `HERR` vs `Herr` is a deliberate Elb1871 distinction. |
| **Several source lemmas** | one / many | **One per run in v1.** The first step lists `Jesum (115)` next to `Jesu (244)` so the second run is one click away | Keeps the review list, the compare-and-set check and undo simple (open question 1). |
| **Scope** | chapter / book / testament / whole Bible | **All four. The default is the whole Bible**, and the review shows counts per book with chips to narrow it | The maintainer's case is Bible-wide. Per-book counts make "too many" fixable in one click. Testament = `BibleBookId` 1–39 / 40–66. |
| **What is replaced** | lemma only / also PartOfSpeech | **Lemma only in v1** | Changing PoS triggers `clearInapplicable` (the dependent features go away), and that differs per row. That is a grid job, not a blind bulk job. A "set PoS" bulk action can follow with a preview of the cleared fields (open question 2). |
| **Preview → apply** | a) server-side preview token · b) predicate + exclusions · c) explicit word ids + compare-and-set | **(c)** | (a) needs server state with expiry for a single-maintainer app. (b) can include rows nobody saw, if they started matching after the preview. (c) is "what you saw is what you get": the server writes only the listed ids, and only where `Lemma` is still `from`. |
| **Atomicity** | skip stale rows / all-or-nothing | **All-or-nothing**, like `PUT /lemmas/chapter` | One confirmed review = one change set. On a conflict, return `409` with the stale ids; the client refetches the preview, marks those rows and keeps the selection. |
| **Size limits** | page the list / cap it | **Cap at 5 000 rows per preview and per apply**, one response, no paging | Paging plus deselect-across-pages plus group checkboxes is a lot of state for an edge case. Above 5 000 (`der`, `und`, `sein`) nobody reviews row by row. The response still returns `total` and `byBook`, so you narrow with the book chips. 5 000 rows with context ≈ 1 MB JSON (gzip ≈ 150 kB), which virtualization handles easily. |
| **Grouping** | flat / by `WordInContext` / by `PlainWord` + PoS | **Flat virtual list with group header rows by (`PlainWord`, `PartOfSpeech`)** and tri-state checkboxes | `WordInContext` splits on punctuation (6 groups for `Jesu`). The groups let you approve "all `Jesu` PROPN" at once and still drop a single row. |
| **Context** | full verse on demand only / short KWIC snippet in the row + full verse on demand | **Both**: ±5 words in the preview row (costs 30 ms), with the full verse fetched on demand | Without a snippet you would have to open hundreds of verses to decide. The full verse is the fallback for the hard cases. |
| **Unsaved grid edits** | merge into grid edits / allow and ignore / require a clean grid | **Require a clean grid** | Bulk apply is a direct server write. Grid edits are full-row replacements (`LemmaEditFields`), so a dirty grid row with the old `Jesu` would write the old lemma back on save. If dirty, the dialog's first step shows "Erst 3 Änderungen speichern" with `Speichern und weiter` / `Abbrechen`. |
| **Undo** | none / revert the last change set / full history | **Revert by change set.** "Rückgängig" right after apply, and a recent change-sets list later | Needs a change-set id on the audit rows (migration 0010). The revert is compare-and-set as well: it restores only rows whose lemma is still `to`, and reports the others as skipped. |
| **Two popups vs one** | two stacked modals / one dialog with steps | **One dialog in three steps: Suchen → Prüfen → Fertig** | Stacked modals break focus return and Esc handling. One dialog keeps the inputs ("from", "to", scope) visible above the list, so you can change them without going back. |

## Changes to the maintainer's proposal

1. **One dialog with steps, not two popups.** The first step (from, to, scope) collapses into a summary line above the list and can be re-opened from there.
2. **"From" is prefilled and the entry point is the toolbar.** A "Lemma ersetzen …" `Button` sits in the filter bar and prefills `from` with the lemma of the active row. The small icon in the Lemma column header stays as a mouse shortcut with `tabIndex={-1}`, because the grid uses one roving tab stop and a tabbable button inside a `columnheader` would add a stray stop. The header label width is measured for `gridTemplate`, so reserve the icon width there.
3. **The first step shows numbers before you search the whole Bible**: count of `from`, count of `to` ("Jesus: 580 vorhanden, wird zusammengeführt" vs "neues Lemma", which catches typos), and related lemmas (`Jesum 115`). The backend provides these via a counts endpoint.
4. **Each row shows a short context snippet.** "Vers anzeigen" expands the full verse inside the list instead of opening another popup.
5. **Grouping by form** with group checkboxes.
6. **Explicit ids + compare-and-set + 409** instead of trusting the client's view or keeping a server token.
7. **Cap of 5 000 rows** with per-book narrowing instead of an unbounded list.
8. **Clean grid required** before applying, plus undo by change set.

## API contract

All routes sit in the `lemmas` group (`RouteGroupConfig`), are `[RequiresLogin]` and use `Roles(AppRoles.Admin, AppRoles.Maintainer)` (or `AppRoles.WithAdmin(AppRoles.Maintainer)`). Lemma comparison is exact and ordinal, and both values are trimmed first. One slice folder per endpoint under `Endpoints/Lemmas/`; contracts go into `Domain.WebApi/Lemmas/LemmaBulkContracts.cs`.

### 1. `GET /api/lemmas/lemma-counts?q=Jes&limit=20`

For the first step's autocomplete and the "from"/"to" counts. A prefix match that ignores case, so case variants show up next to each other.

```json
{ "items": [ { "lemma": "Jesus", "count": 580 }, { "lemma": "Jesu", "count": 244 }, { "lemma": "Jesum", "count": 115 } ] }
```

Validation: `q` NotEmpty, length 1–255; `limit` 1–50 (default 20).

### 2. `GET /api/lemmas/bulk/occurrences`

Query: `from` (required), `testament` (`ot`|`nt`, optional), `bookId` (optional), `chapter` (optional, needs `bookId`), `partOfSpeech` (optional code), `plainWord` (optional, exact). A GET because it reads only and the parameters are scalars.

```json
{
  "from": "Jesu",
  "total": 244,
  "capped": false,
  "limit": 5000,
  "byBook": [ { "bookId": 40, "count": 31 }, { "bookId": 41, "count": 12 } ],
  "groups": [ { "plainWord": "Jesu", "partOfSpeech": "PROPN", "count": 244 } ],
  "occurrences": [
    {
      "wordId": 552442, "bookId": 40, "chapter": 1, "verse": 1, "positionInVerse": 6,
      "wordInContext": "Jesu", "plainWord": "Jesu", "partOfSpeech": "PROPN",
      "case": "Gen", "number": "S", "gender": "Masc",
      "left": "Buch des Geschlechts", "right": "Christi, des Sohnes Davids, des"
    }
  ]
}
```

- `total`, `byBook` and `groups` are computed over **all** matches. `occurrences` holds at most `limit` rows in reading order. When `capped` is true, the UI does not allow applying and asks you to narrow the scope.
- Validation: `from` NotEmpty, trimmed length 1–255; `bookId` 1–66; `chapter` > 0, only with `bookId`; `testament` must not be set together with `bookId`; `partOfSpeech` must be valid in `ElbLemmaValueMaps.PartOfSpeech`; `plainWord` ≤ 255.
- Nulls are omitted (API convention), so a missing `case` means none.

### 3. `GET /api/lemmas/verse?bookId=40&chapter=1&verse=1`

The full verse on demand. Returns the existing `LemmaWordRow[]` (`SelectRows` + `WHERE BibleBookId/Chapter/Verse`, served by `Elb1871Words_reference_idx`), so the expanded verse can also show the neighbours' lemmas. The public `chapterWords` endpoint would work too, but it loads a whole chapter per click (Ps 119 ≈ 4 400 words). Validation: `bookId` 1–66, `chapter` > 0, `verse` > 0. Allowed for any logged-in user, like `GET chapter`.

### 4. `POST /api/lemmas/bulk/apply`

```json
{ "from": "Jesu", "to": "Jesus", "wordIds": [552442, 552691], "note": "Flexionsform als Lemma" }
```

Responses:

- `200` `{ "changeSetId": 17, "updated": 243 }`
- `409` `{ "stale": [ { "wordId": 552691, "lemma": "Jesus" } ] }`: some rows no longer have lemma `from` (or have no morphology row). Nothing was written.
- `404` for unknown word ids (same as `PUT chapter`). `400` for validation errors.

Validation (FluentValidation, `Validator<BulkApplyRequest>`):

- `from`, `to`: NotEmpty, `Trim().Length` 1–255 (`LemmaEditFieldsValidator.MaxLemmaLength`), no control characters, and `to` must not equal `from` (ordinal, after trim).
- `wordIds`: NotEmpty, count ≤ `MaxBulkWords = 5000`, all > 0, distinct.
- `note`: optional, ≤ 500.

### 5. `POST /api/lemmas/bulk/{changeSetId}/revert`

Restores `BeforeValues."Lemma"` on every row of the change set whose current lemma still equals the set's `to`. The revert is a new change set (`RevertOf`). Partial success is fine here:

`200` `{ "changeSetId": 18, "reverted": 243, "skipped": [552700] }`, `404` unknown set, `409` already reverted.

### 6. Later: `GET /api/lemmas/changesets?limit=20`

A list of recent change sets (kind, from → to, count, user, time, reverted?) for an undo panel. Not needed for v1, because the "Fertig" step offers undo right away.

### Audit rows

One `Elb1871MorphologyEdits` row per word, in **the same shape as today**: full `LemmaEditFields` snapshots as `BeforeValues` / `AfterValues`, with PascalCase keys and nulls included (what `JsonSerializer.Serialize` writes in `LemmaRepository.ApplyAsync`). In bulk edits only `Lemma` differs between the two snapshots. The new `ChangeSetId` ties the rows to one `Elb1871MorphologyChangeSets` row. `PUT chapter` and `PUT words/{id}` can fill it too (kind `grid`), so every save becomes one reviewable unit. That fits the "one save = one patch" model.

### Apply, set-based (sketch; one transaction)

```sql
-- 1. lock and check (C# compares found/stale against the request)
SELECT w."Id"    AS "WordId"
     , m."Id"    AS "MorphologyId"
     , m."Lemma" AS "Lemma"
FROM unnest(@wordIds) AS i("WordId")
    JOIN "unshackled-word"."Elb1871Words" w ON w."Id" = i."WordId"
    LEFT JOIN "unshackled-word"."Elb1871Morphology" m ON m."HebRefId" = w."HebRefId" AND m."PositionInVerse" = w."PositionInVerse"
FOR UPDATE OF m

-- 2. if nothing is missing or stale: update and audit in one statement
WITH "Updated" AS
(
    UPDATE "unshackled-word"."Elb1871Morphology" m
    SET "Lemma" = @to
    FROM "unshackled-word"."Elb1871Words" w
    WHERE w."Id" = ANY(@wordIds)
        AND m."HebRefId" = w."HebRefId"
        AND m."PositionInVerse" = w."PositionInVerse"
        AND m."Lemma" = @from
    RETURNING w."Id" AS "WordId", m.*
)
INSERT INTO "unshackled-word"."Elb1871MorphologyEdits" ("ElbWordId", "HebRefId", "PositionInVerse", "UserId", "ChangeSetId", "BeforeValues", "AfterValues")
SELECT u."WordId"
     , u."HebRefId"
     , u."PositionInVerse"
     , @userId
     , @changeSetId
     , jsonb_build_object('Lemma', @from, 'PartOfSpeech', u."PartOfSpeech", 'Degree', u."Degree", ... 'Gender', u."Gender")
     , jsonb_build_object('Lemma', @to,   'PartOfSpeech', u."PartOfSpeech", 'Degree', u."Degree", ... 'Gender', u."Gender")
FROM "Updated" u
```

If the `UPDATE` count differs from the number of ids, roll back (a safety net, since step 1 already checked). 5 000 rows is one round trip, not the 3 × N statements per word that `ApplyAsync` runs today.

## Data / migration `0010_LemmaBulkEdit.sql`

```sql
CREATE INDEX IF NOT EXISTS "Elb1871Morphology_Lemma_idx" ON "unshackled-word"."Elb1871Morphology" ("Lemma");
-- autocomplete / case variants (prefix, case-insensitive)
CREATE INDEX IF NOT EXISTS "Elb1871Morphology_LemmaLower_idx" ON "unshackled-word"."Elb1871Morphology" (lower("Lemma") text_pattern_ops);

CREATE TABLE IF NOT EXISTS "unshackled-word"."Elb1871MorphologyChangeSets"
(
    "Id"         serial4      NOT NULL,
    "UserId"     int4         NOT NULL,
    "Kind"       varchar(20)  NOT NULL,   -- 'grid' | 'bulkLemma' | 'revert'
    "FromLemma"  varchar(255) NULL,
    "ToLemma"    varchar(255) NULL,
    "Scope"      jsonb        NULL,       -- the filters of the preview, for display only
    "Note"       varchar(500) NULL,
    "WordCount"  int4         NOT NULL,
    "RevertOf"   int4         NULL,
    "RevertedAt" timestamptz  NULL,
    "CreatedAt"  timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT "Elb1871MorphologyChangeSets_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "Elb1871MorphologyChangeSets_Users_FK" FOREIGN KEY ("UserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "Elb1871MorphologyChangeSets_RevertOf_FK" FOREIGN KEY ("RevertOf") REFERENCES "unshackled-word"."Elb1871MorphologyChangeSets" ("Id")
);

ALTER TABLE "unshackled-word"."Elb1871MorphologyEdits"
    ADD COLUMN IF NOT EXISTS "ChangeSetId" int4 NULL
        CONSTRAINT "Elb1871MorphologyEdits_ChangeSets_FK" REFERENCES "unshackled-word"."Elb1871MorphologyChangeSets" ("Id");
CREATE INDEX IF NOT EXISTS "Elb1871MorphologyEdits_ChangeSetId_idx" ON "unshackled-word"."Elb1871MorphologyEdits" ("ChangeSetId");
```

- The occurrence query, with the `Lemma` index: index scan on Morphology → nested loop over `IDX_Elb1871Words_HebRef_Position`. Scope filters apply on `w."BibleBookId"` / `w."Chapter"` after the join. That is fine because the lemma is the selective part. `byBook` and `groups` are one `GROUP BY` each over the same matched set (one CTE, three result sets via `QueryMultiple`).
- Context snippet: two `LATERAL string_agg` lookups on `Elb1871RefAndPositions_unique`, computed only for the ≤ 5 000 returned rows (measured at 57–70 ms without the new index).
- The seed rule ("only the mapping seed may be updated afterwards") is unaffected: bulk edits are runtime data like the grid edits.

## UI flow

```
┌ Lemma ersetzen ─────────────────────────────────────────────── ✕ ┐
│ 1 Suchen   2 Prüfen   3 Fertig                                    │
│ Von  [Jesu          ] 244×     Nach [Jesus         ] 580× vorhanden│
│ Ähnlich: Jesum 115 · Jesus 580                                     │
│ Bereich (●Ganze Bibel ○AT ○NT ○Buch [Matthäus▾] ○Kapitel [1▾])     │
│ Wortart [alle ▾]                         [Abbrechen] [Vorkommen ▸]│
└───────────────────────────────────────────────────────────────────┘

┌ Lemma ersetzen: Jesu → Jesus · Ganze Bibel · 244 Treffer ✎ ──── ✕ ┐
│ Bücher: [Mt 31] [Mk 12] [Lk 40] [Joh 52] [Apg 60] …  (Filter-Chips)│
│ [☑ alle] 243 von 244 ausgewählt                   Suche [______]   │
│ ┌───────────────────────────────────────────────────────────────┐ │
│ │ ☑ ▾ Jesu · eigenname · 244                                    │ │
│ │ ☑   Mt 1,1   …Buch des Geschlechts [Jesu] Christi, des …   ⌄  │ │
│ │ ☐   Mt 1,18  Die Geburt [Jesu] Christi war aber also: …    ⌄  │ │
│ │       └ Vers: Die Geburt Jesu Christi war aber also: Als …    │ │
│ │ ☑   Mt 1,21  … (virtual list, ~2rem rows) …                ⌄  │ │
│ └───────────────────────────────────────────────────────────────┘ │
│ ⚠ 3 Wörter im Raster sind ungespeichert → [Speichern und weiter]  │
│                         [Zurück] [243 Wörter ändern (Strg+Enter)] │
└───────────────────────────────────────────────────────────────────┘

┌ Lemma ersetzen ─────────────────────────────────────────────── ✕ ┐
│ ✓ 243 Wörter: Jesu → Jesus geändert.   [Rückgängig] [Schließen]   │
│ Weiter: »Jesum« (115) → Jesus  [Starten]                          │
└───────────────────────────────────────────────────────────────────┘
```

- **Entry**: a "Lemma ersetzen …" `Button` in the filter bar (`aria-haspopup="dialog"`), prefilled from the active row, plus an icon in the Lemma column header (mouse only). Rendered only when `canEdit`.
- **Step 1 → 2**: "Vorkommen" fetches the occurrences (React Query, key `["lemmas","bulk",params]`, `staleTime: 0`). Empty → `EmptyState` "Keine Vorkommen". `capped` → `EmptyState` "Mehr als 5 000 Treffer, bitte eingrenzen" with the book chips still active.
- **Selection state** is a `Set` of deselected word ids (the default is all selected). Group checkbox: checked / unchecked / mixed (`aria-checked="mixed"`). A "Nur abgewählte" toggle reuses the `Button` `aria-pressed` style. The local search filters the rows shown, not the selection.
- **Apply**: `Strg+Enter` or the primary button. While it runs, the list is read-only and the button shows "Wird geändert …". A `409` refetches the preview, marks stale rows ("inzwischen geändert zu »Jesus«") and keeps the deselections. Any other error → `FormError` with "Erneut versuchen".
- **After success**: invalidate `["lemmas","chapter",…]` so the grid reloads (it already has `staleTime: 0`) and refresh the grid's lemma `datalist`. "Rückgängig" calls revert. Closing returns focus to the opening button.
- **Phone (< 48rem)**: the dialog goes full screen. Each row is two lines (reference + snippet) with a 2.75rem touch target for the checkbox.

## Accessibility and keyboard

- Native `<dialog>` with `showModal()`: focus trap, inert background, Esc and the top layer come for free (better than the hand-made `role="dialog"` in `WordDetailPopover`, which is a non-modal popover). It is labelled by the title (`aria-labelledby`) and the step line (`aria-describedby`). Esc asks for confirmation only while an apply is running. On open, focus goes to "Nach" (step 1) or to the list (step 2).
- **The list** is `role="listbox"` with `aria-multiselectable="true"`, a single tab stop and `aria-activedescendant` (the active row stays mounted through `rangeExtractor`, which is also how `MorphologyGrid` keeps its active row). Rows are `role="option"` with `aria-selected`, `aria-setsize` = total and `aria-posinset` = index + 1, because virtualization hides the true count. Group headers are options too, with `aria-checked="mixed"` via an `sr-only` label ("Gruppe Jesu, eigenname, 244, teilweise ausgewählt").
- Keys in the list: ↑ ↓ move, Page ↑/↓, Home/End, Space toggles, Shift+Space selects a range, → / Enter expands the verse, ← collapses it, Ctrl+A selects all and Ctrl+Shift+A selects none. In the group header, Space toggles the group.
- The selection counter is `aria-live="polite"`, debounced (it says "243 von 244 ausgewählt", not every keystroke). The result step text is `role="status"`.
- The expanded verse is a row of its own inside the virtual list (measured with `measureElement`), with the word marked by `mark-same` like the grid's context line. It is not live; its loading state reads "Vers wird geladen …".
- Tokens only. The highlight uses `mark-same`, stale rows use `accent` with an "!" glyph, never colour alone.

## Components

**Reused as is:** `Button` (all actions, scope and book chips with `aria-pressed`, the "Nur abgewählte" toggle), `TextField` (from / to with `list` for the autocomplete, `size="sm"`, local search), `Select` (book, chapter, part of speech), `EmptyState` (no occurrences / too many / load error), `FormError`.
**Not reused:** `SaveBar` (it stands for pending local changes, and bulk is an immediate server write; its inline-confirm pattern is copied, not the component) and `ToggleMenu` (there are no show/hide lists here).

**Props to add:**

| Component | Change |
|---|---|
| `MorphologyGrid` | `headerAction?: Partial<Record<ColumnKey, ReactNode>>` (or a dedicated `onBulkLemma?: () => void`) for the header icon; `columnWidth` reserves its width. |
| `Button` | none: `aria-haspopup` already passes through via `ButtonHTMLAttributes`. |

**New components (proposed, to confirm in chat, then register in `Components.md`):**

| Name | Purpose |
|---|---|
| `Dialog` | Generic modal on native `<dialog>`: `open`, `onClose`, `title`, `description?`, `size` (`md` / `lg`; full screen below 48rem), `initialFocusRef?`, `footer?`, focus return. There is no modal in the app yet. Invite/passkey confirmations and the mapping editor can use it later. |
| `LemmaBulkEdit` | The three-step content inside `Dialog`: form, the virtualized selectable occurrence list (`useVirtualizer`), group rows, verse expansion, apply/undo states. Props `initialFrom`, `bookId`/`chapter` (for the scope defaults), `dirtyCount`, `onSaveGrid`, `onApplied`. The logic (selection set, group tri-state, range select, flattening groups + rows + expanded verses into virtual items) goes in `lib/lemmas/bulk.ts` with unit tests, like `edits.ts`. |

A third, generic `SelectableList` could come out of `LemmaBulkEdit` once a second user exists (e.g. Strong's occurrences). Not before then.

## Risks found while reading the code (independent of bulk)

- **`PUT /lemmas/chapter` has no concurrency check, and it replaces the full row.** A grid open in another tab with stale data overwrites a bulk change on its next save of that word. Cheap fix: send the loaded values (or Postgres `xmin` as a version) with each change, and have the server answer `409` per word, as the editor draft's "Conflicts" section already planned.
- **The page chunks saves over 2 000 changes** (`chunk(changes, MAX_CHANGES_PER_REQUEST)`), so a large grid save is all-or-nothing only per chunk, not as a whole.
- **`ApplyAsync` runs 3 statements per word.** It's fine at 2 000, but the bulk path should use the set-based SQL above.

## Open questions

1. Several source lemmas in one run (`Jesu` + `Jesum` → `Jesus`), or one per run with the "Weiter" suggestion?
2. A bulk "set part of speech" (with preview of the dependent features that get cleared) later, or keep PoS changes in the grid only?
3. Is 5 000 the right cap? Should the maintainer be able to apply a capped set without reviewing it ("alle 26 422 ändern"), or never?
4. Should bulk edits by the maintainer go through the same suggestion/merge flow later, or always write directly (today the whole slice writes directly)?
5. Should undo be limited to the last change set in v1, or ship the change-set list (`GET /lemmas/changesets`) right away?
6. Is the ±5-word snippet enough, or should the row show the whole verse truncated to one line?
7. Keyboard shortcut to open the dialog from the grid? `Strg+H` is browser history and `Strg+F` is find; a free option would be `Alt+L`.
8. Should `PUT chapter` / `PUT words/{id}` also write a change set (`Kind = 'grid'`)? That's recommended, since it gives every save one reviewable unit.
9. Should the legacy `Elb1871Words.Lemma` column (used by `IX_Elb1871Words_GermanKey_Book` for word stats) be kept in sync, or retired in favour of `Elb1871Morphology.Lemma`?
