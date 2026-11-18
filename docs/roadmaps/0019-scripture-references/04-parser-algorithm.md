# 0019 · Parser algorithm

One regex cannot read `Offb 2,1-5.7.9-10` *and* `Röm 8,28; 12,1-2` *and* `Mat.25.33, 34` without becoming unmaintainable (the current one already is at its limit). Proposal: **regex tokenizer + small hand-written parser + interpretation state machine**. Each layer is pure and tested on its own. All new code lives in `src/UnshackledWord.Domain/Models/BibleStructure/References/`.

## 1. Tokenizer (regex)

```csharp
[GeneratedRegex(@"(?<ws>\s+)|(?<num>\d+)|(?<word>\p{L}+)|(?<colon>:)|(?<comma>,)|(?<dot>\.)|(?<semi>;)|(?<dash>[-‐-―−])|(?<other>.)")]
```

Produces `Token(Kind, Text, Start, Length)`. Whitespace is kept as a token because two places need it (`1 Kor` vs. `Joh 3,1 und 2`, and the search convenience `Joh 3 16`). No interpretation happens here.

## 2. Book

At a book start the parser tries the longest run of up to four tokens that resolves through `BookAliasCatalog` ([03-book-aliases.md](03-book-aliases.md)): optional ordinal (`num` 1–3 or `word` I/II/III, optional `dot`, optional `ws`), then one or two `word`s separated by `ws` (`Hohes Lied`, `Klagelieder Jeremias`), then an optional `dot`. Longest match first, so `1 Kor` wins over `1` + `Kor`.

A `num` 1–3 followed by a word is a book ordinal **only if** the joined text resolves as a book. That keeps `Joh 3,1 und 2` (`1 und` is no book) and `Ps 23,1 f` working.

## 3. Grammar

```
input     := reference ( SEMI reference )*                -- a new reference starts where a book resolves
reference := book [ groups ]                              -- no groups: book only (search)
groups    := group ( SEMI group )*                        -- German: ";" separates chapters
group     := item ( verseSep item )*                      -- German: "." (and "u." / "und") separates verses
item      := point [ DASH point ] [ suffix ]
point     := NUM [ cvSep NUM ]                            -- greedy: always take "NUM cvSep NUM" if present
suffix    := "f" | "ff" | "a" | "b"                        -- "f"/"ff" may carry a DOT; "a"/"b" (half verse) are dropped
```

Two notations, chosen by the **profile**, not guessed from the input:

| Notation | Profile | `cvSep` | `verseSep` | after `;` | Example |
|---|---|---|---|---|---|
| German citation | `German`, `Legacy` | `,` (also `:` tolerated, `ws` in search: `Joh 3 16`) | `.`, `u.`, `und` | new chapter (or new book) | `Offb 2,1-5.7.9-10`, `Röm 8,28; 12,1-2` |
| STEP/OSIS | `Osis` (lexicon only) | `.` in attributes, `:` in display text | `,` | new chapter (always `c.v` in the data) | `Mat.25.33, 34`, `Heb.5.4; 7.11; 9.4` |

A `.` or `;` with no number after it ends the reference (sentence punctuation, `Luk.4.34.`). A `.` after a verse is a verse separator only if a number follows; `Joh 3,16.` is just Joh 3,16.

## 4. Interpretation (state machine)

State: current `chapter`, current `level` (`Chapter` or `Verse`). Per item:

1. Point with two numbers → `chapter = a`, verse `b`, `level = Verse`.
2. Point with one number **after `verseSep`** (`.`/`u.`; OSIS `,`) → verse in the current chapter. Only allowed when `level == Verse` (`Röm 8.9` is an error: `unexpectedToken`).
3. Point with one number **at the start or after `;`** →
   - single-chapter book (Obadja, Philemon, 2/3 Johannes, Judas) → verse in chapter 1, `level = Verse`;
   - otherwise → whole chapter, `level = Chapter` (`Röm 8; 12` = chapters 8 and 12; `Röm 8,28; 12` = 8,28 and chapter 12).
4. Range end with one number keeps the level of the start (`2,1-5` → verses 1–5; `8-9` → chapters 8–9); with two numbers it sets chapter and verse (`1Mo 1,1-2,3`).
5. `f` → end = start verse + 1; `ff` → end = last verse of the chapter.
6. Book only → chapter 1, whole chapter, `BookOnly = true`.

Walkthroughs:
- `Offb 2,1-5.7.9-10` → `2,1` chapter 2 / level Verse; `-5` → 2:1–2:5; `.7` → 2:7; `.9-10` → 2:9–2:10. One reference, three spans.
- `Offb 2,1-5; 7; 9-10` → 2:1–2:5, then chapter 7 whole, then chapters 9–10 whole.
- `Röm 8,28; 12,1-2` → 8:28, 12:1–12:2.

## 5. Result model

```csharp
public enum VerseCounting { Hebrew, English }
public enum AliasProfile { German, Legacy, Osis }
public sealed record ScriptureParseOptions(AliasProfile Profile, bool AllowPrefix, bool WholeInput, VerseCounting Counting);

public readonly record struct VerseSpan(int StartChapter, int StartVerse, int EndChapter, int EndVerse); // whole chapter: 0 / 999
public sealed record ScriptureReference(int? BookId, string? NonCanonical, IReadOnlyList<VerseSpan> Spans,
                                        int Start, int Length, bool BookOnly);
public sealed record ScriptureParseError(string Code, int Position, IReadOnlyDictionary<string, string> Args);
public sealed record ScriptureParseResult(IReadOnlyList<ScriptureReference> References, ScriptureParseError? Error,
                                          IReadOnlyList<int> BookCandidates);

public static class ScriptureReferenceParser
{
    public static ScriptureParseResult Parse(string? input, ScriptureParseOptions options);        // whole input
    public static ScriptureReference? ParseAt(string text, int start, ScriptureParseOptions options); // scanner, consumes what fits
}
```

`VerseSpan.ToRefIds(bookId)` yields the start/end ref ids (`PassageRange`-compatible). Error codes: `empty`, `unknownBook`, `ambiguousBook`, `missingChapter`, `chapterOutOfRange`, `verseOutOfRange`, `endBeforeStart`, `unexpectedToken`; `Position` points at the offending token so the UI can say where.

**Validation** against `BibleBook.VersesPerChapterHeb` only runs for `VerseCounting.Hebrew`. English-counting spans (lexicon) are validated after mapping ([05-lexicon-and-counting.md](05-lexicon-and-counting.md)), because `Mal 4` exists only in English counting.

## 6. Formatting

`GermanReferenceFormatter` gains `Format(ScriptureReference)`: `Offb 2,1–5.7.9–10`, groups joined with `; ` (`Röm 8,28; 12,1–2`), whole chapter `Röm 8`, chapters `Röm 8–9`, cross-chapter `1Mo 1,1–2,3`. Used in the German and the English UI alike. `ReaderPath(ScriptureReference)` → `/bible/66/2?v=1-5,7,9-10#v1` ([06-api-and-ui.md](06-api-and-ui.md)).
