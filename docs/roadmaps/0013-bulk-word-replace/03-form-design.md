# 0013 · Form design v2: find, review, map ("Stapel-Ersetzung")

Replaces v1 (direct apply with per-row replacement, dropped on 2026-11-15: article agreement such as `ein Weib` => `eine Frau`
cannot be solved by a word swap). The search side of v1 is kept. Click dummy: [click-dummy.html](click-dummy.html).

Route `/edit/revision/replace`, page `pages/BulkReplacePage.tsx`, write roles only (same guard as `MaintainerPage`; server
`Roles(Admin, Maintainer)`). Linked from the maintenance menu.

## Three steps, one verse at a time

| Step | Screen | Result |
|---|---|---|
| 1 Treffer | Filter form + result table with checkboxes | A queue of checked verses |
| 2 Text prüfen | The verse's full text in an editor, the replacement already applied as a suggestion | An edited text; "Weiter" saves it as a new version of the target revision |
| 3 Zuordnung prüfen | Existing `MappingEditor` on the saved version | "Bestätigen" marks the mapping reviewed; the verse is done and the next one opens |

Only a verse that went through step 2 can be saved, and only a verse that went through step 3 counts as done. Each verse is
submitted on its own, there is no batch commit. Skipping a verse ("Überspringen") writes nothing.

## Step 1: Treffer

Fields (existing components `Select`, `TextField`, `Button`, `ToggleMenu`, `EmptyState`, `FormError`):
- **Quelle** and **Ziel** revision selects (default Elberfelder 2027 both). Source = own variant of the verse, else Elb1871.
  Target = writable revisions only. Source != target is allowed; the suggestion is built from the target's current text.
- **Wort** (2–40 letters, one token), **auch Formen desselben Lemmas**, **Vorschlag für Ersetzung** (e.g. `Frau`; used only to
  prefill step 2, may be empty).
- **Strong's facets** after the search: one chip per Strong's with count, plus "ohne Zuordnung". Filtering is client side;
  hidden rows leave the queue.
- Table `BulkReplaceTable` (new component, rows virtualised with `@tanstack/react-virtual`, max 1000 server hits): checkbox,
  reference, excerpt with the hit marked, Strong's/lemma chips, flags ("Ziel hat v2", "≠ Quelle", "Artikel davor: ein"),
  and for verses reviewed earlier in this browser the status "geprüft". Toolbar: Alle / Keine / Umkehren,
  "nur Zeilen ohne Artikel davor" (quick rows).
- Button **Prüfen (n Verse)** starts the review of the checked verses. Rows of the same verse are merged into one queue
  entry (two hits in one verse = one review).

## Step 2: Text prüfen

- Header: reference, position in the queue ("12 / 87"), links to the reader and to the version history.
- Text field (multi-line `TextField`/`RevisionEditor` text area): prefilled with the target's current text, with the
  replacement applied to the found word(s) per the suggestion; the changed words are shown in a second read-only line with
  `wordDiff` marks (`lib/revision/wordDiff.ts`), so it is clear what the tool changed. The user may edit anything (article,
  neighbouring words, further modernisation).
- Hint when the word before the hit is an article/determiner (Elb1871 `Stts` of the previous position): "Artikel prüfen".
  No automatic grammar fixing.
- Optional note (default `Stapel: Weib => Frau`).
- Buttons: **Überspringen**, **Zurück zur Liste**, **Weiter zur Zuordnung** (saves the text through the existing
  `POST api/revision/verse/...`, `basedOnLevel` = target when a variant exists, else 0). A conflict (someone saved meanwhile)
  reloads the current text and asks again. Unchanged text cannot be saved.

## Step 3: Zuordnung prüfen

The existing `MappingEditor` for the saved `revisionVerseId` (carried links, "prüfen" marks on `changed`/`new` words, original
words without link). **Bestätigen** calls the existing `PUT api/revision/verseMapping/{id}` (marks `MappingReviewedAt`) and
opens the next verse of the queue. **Später prüfen** leaves the version saved and unreviewed (it stays in the revision
overview's unreviewed list) and opens the next verse.

## Queue and persistence

State in `localStorage` (`uw.bulkReplace`, wrapped in try/catch, the page works without): the filter (word, lemma toggle,
source, target, suggestion), the queue of `hebRefId`s and per verse `pending | saved | reviewed | skipped`. After a reload or
the next day "Fortsetzen (n offen)" restores the queue; verses already `reviewed` are not offered again, and the server's
current text is always re-read, never cached. "Warteschlange verwerfen" clears it.

## Quick rows

A row with no article/determiner before the hit and a plain suggestion is "quick". "Alle schnellen Verse bestätigen" in step
1 opens them one after another in a compact mode (text step with the suggestion applied, keyboard: `Enter` = Weiter, `Esc` =
Überspringen), so a long list such as `Weib` is not 400 slow clicks. The mapping step is still shown for each verse.

## Search API (unchanged from v1)

`POST api/revision/bulkReplace/search`
```json
{ "word": "Weib", "sameLemma": true, "sourceRevision": "elb2027", "targetRevision": "elb2027" }
```
```json
{ "lemmas": ["Weib"], "total": 423, "capped": false,
  "facets": [ { "strongs": "H802", "count": 412 }, { "strongs": null, "count": 7 } ],
  "rows": [ { "hebRefId": 1002022, "occurrence": 1, "found": "Weibe", "contextBefore": "… zu einem", "contextAfter": "und brachte …",
              "strongs": ["H802"], "lemma": "Weib", "articleBefore": "einem", "targetVersion": 2, "targetDiffers": false } ] }
```
Revisions are identified by `code` (unique) and resolved to the level server side (levels are unique among non-base
revisions). Source text = active `RevisionVerses` row of the source revision, else Elb1871; tokens from `RevisionWords` or
`Elb1871Words`, matched on `lower("PlainWord")`, with `sameLemma` joined to `Elb1871Morphology` via `BasePositionInVerse`.
Strong's: variant -> `RevisionMapping.StepWordId` -> `StepHebrewWords`/`StepGreekWords`; base -> `Elb1871*Mapping`. The
article flag reads `Stts` of the previous position. An expression index on `lower("PlainWord")` is likely needed (check
with `EXPLAIN ANALYZE` for "und", cap 1000).

New server code is only the search. Step 2 and 3 use the existing endpoints: `GET revision/verse/...` (current text),
`POST revision/verse/...` (save), `GET`/`PUT revision/verseMapping/{id}`. The suggestion (replace the found token, keep
punctuation and carry an initial capital) is computed client side in `lib/revision/bulkReplace.ts`.

## Accessibility and tests

- Live region announces hits, selection count and queue progress; focus moves to the count line after a search and to the
  text field when a verse opens. Table: one tab stop, arrow keys between row checkboxes, `Space` toggles.
- Vitest: validation, facet filtering drops hidden rows from the queue, merged queue entries for two hits in one verse,
  suggestion helper (capital, punctuation), queue restore from `localStorage` and its absence, step flow
  (save only after step 2, done only after step 3, skip writes nothing, conflict reloads), unchanged text disabled.
  xUnit: search query (variant vs base source, lemma forms, Strong's facets, article flag).
- i18n: new `bulkReplace` group (de/en).
