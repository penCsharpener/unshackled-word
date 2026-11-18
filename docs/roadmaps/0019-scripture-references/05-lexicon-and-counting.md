# 0019 · Lexicon references and verse counting

## Two sources of references in `StepStrongsLexicon."Lexicon"`

**Tagged (Greek, 41,473 tags).** The attribute is machine-readable, the content is what the reader sees:

```
<ref='Mat.5.29'>Mat.5:29</ref>
<ref='Heb.5.4; 7.11; 9.4'>Heb.5:4, 7:11, 9:4</ref>
<ref='Mat.25.33, 34'>Mat.25:33, 34</ref>
<ref='Gen.4.2-10'>Gen.4:2-10</ref>
<ref='Luk.4.34.'>Luk.4:34.</ref>          -- trailing dot inside attribute and text
<ref='Rev.5.1'>Rev.5:1;</ref>             -- list punctuation inside the text
<ref='Act.2.33; 5.31'>Act.2:33 5:31</ref>  -- text without separator
```

Algorithm: regex `<ref='(?<osis>[^']*)'>(?<text>.*?)</ref>` (case-insensitive, singleline). Parse `osis` with profile `Osis`, `WholeInput = true` after trimming trailing `.`/`;`/`,`. If that fails, parse `text` the same way. If both fail, or the book is non-canonical, the content becomes plain text. Trailing `;`, `,`, `.` of the content are moved out of the link into the following text segment so the link label is `Rev.5:1` and the `;` stays readable.

**Untagged (Hebrew and name entries, about 4,000).** Plain text like `first mentioned at 1Ch.8.39` or `Gen.36.11`. Scanner: candidate regex built from the `Osis` aliases, longest first:

```
(?<![\p{L}\d])(?<book>1Ch|2Ch|Gen|…|Rev)\.(?=\d)
```

then `ScriptureReferenceParser.ParseAt(text, match.Index, Osis)` consumes as much as fits (`1Ch.8.39`, `Gen.4.2-10; 5.1`) and the scanner continues after it. Requiring `Book.` + digit with no space is what keeps false positives near zero in English prose (`Job` the word never carries `.digit`).

## Segments

New pure class `LexiconTextSegmenter` (Domain, `Models/Study/`), replacing the tag-stripping step of `LexiconTextCleaner` for display:

```csharp
public sealed record LexiconSegment(string Text, LexiconRef? Ref);   // Ref null = plain text
public sealed record LexiconRef(int BookId, IReadOnlyList<VerseSpan> Spans);        // already in Hebrew counting
public static (IReadOnlyList<LexiconSegment> Segments, bool Truncated) Segment(string? raw, int maxLength, IVerseCountingMap map);
```

Order: extract `<ref>` tags into placeholders → strip other tags, decode entities, collapse whitespace (same rules as `Clean`) → scan the remaining text segments for untagged references → map spans to Hebrew counting → truncate. Truncation counts visible characters across segments and never cuts inside a reference segment (a reference that would cross the limit is dropped together with the text after it). `LexiconTextCleaner.Clean` stays for callers that need a plain string (MCP `LexiconTools`); its output must equal the concatenated segment texts (test).

Strong's numbers (`H0501H`) stay plain text in segments; the frontend keeps linking them (`LinkedStrongs`).

## English → Hebrew verse counting

STEP references use English counting = `BibleVerseCountingMapping."LxxRefId"`; the reader uses `HebRefId`. Only 2,026 of 31,174 rows differ.

- `IVerseCountingRepository.GetDifferingAsync()` (Infrastructure, Dapper): `SELECT "LxxRefId", "HebRefId" FROM … WHERE "LxxRefId" <> "HebRefId"`.
- `VerseCountingMap` (Domain, pure): built from those rows; `MapStart(lxxRefId) = MIN(HebRefId)`, `MapEnd(lxxRefId) = MAX(HebRefId)`, unmapped → identity (the rule of `TskRepository.GetOutgoingAsync`). `MapSpan(bookId, VerseSpan)` maps start and end; whole-chapter spans (`0`/`999`) map via their first and last real verse of the English chapter.
- `IVerseCountingMap` singleton in Application, loaded lazily once per process (2,026 rows, a few KB; the table changes only with a re-seed, so no invalidation).

Pinned examples (from `uw_0018_test`): English `Mal 4:5` (39004005) → 39003023; `Ps 3:1` (19003001) → 19003002 (the title is Hebrew verse 1 = English verse 0); `Ps 51:1` → 19051003; `Joel 2:28` → 29003001, `Joel 3:1` → 29004001; `Joh 3:16` → unchanged. One-to-many rows exist (8 English ids, e.g. Ps 51 title 19051000 → 19051001 and 19051002; 1Sam 20:42 → 9020042 and 9021001): as a start they map to the smallest, as an end to the largest Hebrew id.

After mapping, spans are validated against `VersesPerChapterHeb`; an invalid span (data error in STEP) makes that reference plain text and is counted in the coverage report (task 0019.10).
