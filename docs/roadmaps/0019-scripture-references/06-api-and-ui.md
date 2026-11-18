# 0019 · API and UI

## Endpoints (FastEndpoints, public read, anonymous rate limit as for the other reads)

**`GET api/references/parse?q=Offb 2,1-5.7.9-10`** — `Endpoints/MetaBible/ParseReference/`, contracts in `Domain.WebApi/MetaBible/ParseReference/`. Profile `German` (German citation style), prefix matching on, whole input, Hebrew counting.

```json
{
  "references": [{
    "bookId": 66, "bookOnly": false,
    "display": "Offb 2,1–5.7.9–10",
    "spans": [{ "startChapter": 2, "startVerse": 1, "endChapter": 2, "endVerse": 5 }, …],
    "path": "/bible/66/2?v=1-5,7,9-10#v1"
  }],
  "error": { "code": "verseOutOfRange", "position": 9, "args": { "book": "Offb", "chapter": "2", "max": "29" } },
  "bookCandidates": [50, 57]
}
```

Always 200 (a non-reference is a normal answer, not a client error). `display` is always German citation style. A reference with several groups (`Röm 8,28; 12,1-2`) has one `path` to its first group; the card lists every group as its own link (`groups: [{ display, path }]`). Application service `ScriptureReferenceService` wraps parser + formatter + path so MCP can reuse it later.

**`GET api/passageText?spans=66002001-66002005,66002007&level=200`** — `Endpoints/Elberfelder/GetPassageText/`. Spans as ref-id pairs (single id = one verse), at most 10 spans and 40 verses (`truncated: true` beyond). Response reuses `ChapterVerseResponse` grouped per span (same shape as `CrossReferenceTextResponse`), respecting the reader level like `crossReferenceTexts`. Used by the search card and the lexicon preview.

**Lexicon segments.** `WordDetail` (`reading.lexicon`) and `ChapterOriginals` (`definition`) gain `definitionSegments: [{ text, ref?: { bookId, spans, path } }]` next to the existing string, so older clients and MCP keep working. `path` is computed in the backend with the same `ReaderPath`.

## Deep link

`/bible/{bookId}/{chapter}?v=1-5,7,9-10#v1`:

- `v` lists verses of **that chapter** only (cross-chapter span `1Mo 1,1–2,3` opens chapter 1 with `v=1-31`; `Röm 8,28; 12,1-2` gives two links, `/bible/45/8?v=28#v28` and `/bible/45/12?v=1-2#v1`; the card shows the full reference).
- Whole chapter: no `v`, no hash.
- `ChapterPage` parses `v` with a tiny pure helper `parseVerseSelection("1-5,7,9-10") → Set<number>` (`lib/bible/verseSelection.ts`; the app's own canonical format, not a reference parser), marks those verses with a new token pair `--uw-mark-passage` (light/dark) and scrolls to the hash as today. Invalid `v` is ignored.

## Search overlay

`useReferenceParse(query)` (TanStack Query, key `["referenceParse", query]`, `staleTime: Infinity`) runs on the same submitted, debounced query as the verse search.

- Result has a reference with a chapter → `ReferenceCard` at the top of the verses column: formatted reference as link (`path`), up to five verses of text from `passageText` (more: "… n weitere Verse"), several references as several rows. Verse full-text search is not started (`isVerseSearchQuery` stays as is; the overlay passes `""` while a reference is shown).
- Enter with no hit selected → navigate to the first reference's `path` and close.
- Book only (`Hiob`) → small "Zu Hiob 1" link row above the normal hits; full-text search runs.
- `bookCandidates` → "Meinten Sie: Philipper, Philemon?" links that replace the book in the input.
- Error with a chapter present (`Offb 23`) → inline hint from the error code (i18n `search.reference.errors.*`), full-text search still skipped.

## Lexicon text

New presentational component (propose in chat before building, per `CLAUDE.md`): **`ScriptureText`** — renders `definitionSegments`: plain segments through the existing Strong's linking (moved out of `HeaderWordInfo.LinkedStrongs`), reference segments as `Link` to `path` with hover/focus preview. Used in `WordDetailPopover` (definition `<details>`) and `WordInfoCard`/`HeaderWordInfo` (hover lexicon text).

Preview: extract the presentational part of `PreviewCard` from `CrossReferenceList` into **`PassagePreviewCard`** (props: anchor position, `title`, `verses`, `loading`, `error`). Callers keep their own data: `CrossReferenceList` its batched `crossReferenceTexts` query (one request per verse), `ScriptureText` a `passageText` query per hovered reference. Same look, no extra requests for cross references. Touch devices (`useCoarsePointer`): no hover; tap follows the link.

Both components go into `Components.md`.
