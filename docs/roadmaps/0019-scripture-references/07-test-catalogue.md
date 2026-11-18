# 0019 · Test catalogue

xUnit v3 `[Theory]` + `[InlineData]` with Shouldly, in `tests/UnshackledWord.Domain.Tests/Model/BibleStructure/References/`. Expected spans are written as `c:v-c:v` strings (helper `Spans("2:1-2:5", "2:7")`; whole chapter `8:0-8:999`) so a row stays one line. Every row below is at least one test case; implementers add more, never fewer.

## A. Book aliases (`BookAliasCatalogTests`)

| Input | Profile | Book |
|---|---|---|
| `Offb`, `offb`, `OFFB`, `Offb.`, `Offenbarung` | German | 66 |
| `1Mo`, `1. Mose`, `1.Mose`, `1 Mose`, `Gen`, `I Mose`, `I. Mose` | German | 1 |
| `1Kor`, `1. Kor`, `1 Kor.`, `I Kor`, `1. Korinther` | German | 46 |
| `2Kön`, `2Koen`, `2 Kön`, `2Kö`, `II Kön`, `2. Könige` | German | 12 |
| `Röm`, `Roem`, `Römer`, `Roemer`, `RÖM` | German | 45 |
| `Hl`, `Hld`, `Hohelied`, `Hoheslied`, `Hohes Lied` | German | 22 |
| `Pred`, `Koh`, `Prediger` | German | 21 |
| `Jud`, `Judas` | German | 65 |
| `Ri`, `Richter` | German | 7 |
| `Hes`, `Ez`, `Hesekiel` | German | 26 |
| `Ijob`, `Hi`, `Hiob` | German | 18 |
| `Rev`, `John`, `Isa` | German | none (English names are not part of the search) |
| `John`, `Rev`, `1 Cor` | Legacy | 43, 66, 46 (MCP compatibility) |
| `Mat`, `Mrk`, `Jhn`, `Php`, `Jas`, `1Jn`, `Ezk`, `Jol`, `Nam`, `Sng`, `Jud`, `Jude`, `Luke`, `1Jo` | Osis | 40, 41, 43, 50, 59, 62, 26, 29, 34, 22, 65, 65, 42, 62 |
| `Sir`, `2Ma`, `Wis`, `3Ki`, `Bel` | Osis | non-canonical |
| `Offen` (prefix), `Apost`, `Matth` | German + prefix | 66, 44, 40 |
| `Phili`, `Jo` | German + prefix | none, candidates [50, 57] / several |
| `Off` with prefix off, `Xyz`, `` | German | none |

Plus: no alias maps to two books within one profile; every book 1–66 has at least one alias in German and Osis; the collision table of [03-book-aliases.md](03-book-aliases.md) as rows.

## B. Single references (`ScriptureReferenceParserTests`, profile German, Hebrew counting)

| Input | Book | Spans |
|---|---|---|
| `Offb 2,1` / `Offb 2:1` / `Offb. 2, 1` / `offb 2 , 1` / `Offb 2 1` | 66 | `2:1-2:1` |
| `Joh 3,16-18` / `Joh 3,16–18` / `Joh 3,16 - 18` / `Joh 3:16—18` | 43 | `3:16-3:18` |
| `Röm 8` | 45 | `8:0-8:999` |
| `Röm 8-9` / `Röm 8–9` | 45 | `8:0-9:999` |
| `1Mo 1,1-2,3` / `1. Mose 1,1–2,3` | 1 | `1:1-2:3` |
| `Ps 23,1f` / `Ps 23,1 f.` | 19 | `23:1-23:2` |
| `Ps 23,1ff` | 19 | `23:1-23:6` |
| `Joh 3,16a` / `Joh 3,16b` | 43 | `3:16-3:16` |
| `Jud 3` / `Judas 3` / `Phlm 10` / `Ob 4` / `3Joh 2` | 65/65/57/31/64 | `1:3`, `1:3`, `1:10`, `1:4`, `1:2` |
| `Jud 1,3` | 65 | `1:3-1:3` |
| `Offb` (book only) | 66 | `1:0-1:999`, `BookOnly` |
| `1. Korinther 13,4-7` / `I Kor 13,4–7` | 46 | `13:4-13:7` |

## C. Lists and ranges (German citation style)

| Input | Spans |
|---|---|
| `Offb 2,1-5.7.9-10` / `Offb 2,1–5. 7. 9–10` / `Offb 2:1-5.7.9-10` | `2:1-2:5`, `2:7`, `2:9-2:10` |
| `Offb 2,1-5; 7; 9-10` | `2:1-2:5`, `7:0-7:999`, `9:0-10:999` (`;` = new chapter) |
| `Offb 2,1-5 u. 7` / `Offb 2,1-5 und 7` | `2:1-2:5`, `2:7` |
| `Offb 2,1-5,7` | `2:1-5:7` (greedy point) |
| `Offb 2,1; 3,4` / `Offb 2,1; 3:4` | `2:1`, `3:4` |
| `Röm 8; 12` | `8:0-8:999`, `12:0-12:999` |
| `Röm 8; 9,1` | `8:0-8:999`, `9:1` |
| `Röm 8,28; 12` | `8:28`, `12:0-12:999` |
| `Röm 8,28; 12,1-2` | `8:28`, `12:1-12:2` |
| `Röm 8,28.31; 12,1-2.9` | `8:28`, `8:31`, `12:1-12:2`, `12:9` |
| `Joh 3,16; Röm 8,28` | two references: 43 `3:16`, 45 `8:28` |
| `Joh 3,16; 1Kor 13,4` / `Joh 3,16; 1 Kor 13,4` | two references, second 46 |
| `Joh 3,1 und 2` | `3:1`, `3:2` (`1 und` is no book) |
| `Ps 23,1f.; 5,2` | `23:1-23:2`, `5:2` |
| `Joh 3,16.` (sentence dot) | `3:16` |
| `Phlm 4.10` / `Jud 3; 5` | `1:4`, `1:10` / `1:3`, `1:5` (single-chapter book) |

## D. Errors (code, position)

`""` empty · `Joh` with `WholeInput` and Hebrew validation → book only, not an error · `Xyz 3,16` unknownBook@0 · `Joh 3,0` verseOutOfRange · `Joh 3,99` verseOutOfRange (max 36) · `Offb 23` chapterOutOfRange (max 22) · `Joh 3,18-16` endBeforeStart · `Joh 4-3` endBeforeStart · `Joh 3,16-` unexpectedToken · `Joh 3,,16` unexpectedToken · `Röm 8.9` unexpectedToken (verse list without verse) · `Joh 3,16,18` unexpectedToken (not German style; the hint suggests `3,16.18`) · `Rev 2:1` unknownBook · `Phili 1` ambiguousBook with candidates · `Joh 3,16 xyz` unexpectedToken@9.

## E. Osis attributes and scanner (`LexiconTextSegmenterTests`, `ScriptureScannerTests`)

| Input | Result |
|---|---|
| `Mat.5.29` | 40 `5:29` |
| `Heb.5.4; 7.11; 9.4` | 58 `5:4`, `7:11`, `9:4` |
| `Mat.25.33, 34` | 40 `25:33`, `25:34` |
| `Gen.4.2-10` / `Luk.4.34.` | 1 `4:2-4:10` / 42 `4:34` |
| `<ref='Rev.5.1'>Rev.5:1;</ref> x` | link text `Rev.5:1`, then text `; x` |
| `<ref='Act.2.33; 5.31'>Act.2:33 5:31</ref>` | one link, spans `2:33`, `5:31` |
| `<ref='Sir.3.1'>Sir.3:1</ref>` | plain text `Sir.3:1` |
| `<ref='Xyz.1.1'>Xyz.1:1</ref>` | plain text |
| `first mentioned at 1Ch.8.39; <br> son of` | text, link 13 `8:39`, text `; son of` |
| `the Job.1.1 and Job is` | one link (`Job.1.1`), the word `Job` stays text |
| `see 2Co.6.7 , al.` | link `2Co.6.7`, text ` , al.` |
| invented text with 3 refs, `maxLength` in the middle of the 2nd | 1 link, truncated, no half link |
| any input | concatenated segment texts == `LexiconTextCleaner.Clean(input).Text` |

## F. Verse counting (`VerseCountingMapTests`, fixture rows copied from `BibleVerseCountingMapping`)

`Mal 4:5` → `Mal 3:23` · `Mal 4` (whole) → `3:19-3:24` · `Ps 3:1` → `3:2` · `Ps 51:1-2` → `51:3-51:4` · `Ps 51:0` start/end → `51:1` / `51:2` · `Joel 2:28-32` → `3:1-3:5` · `Joel 3:1` → `4:1` · `Joh 3:16` unchanged · unknown id unchanged.

## G. Formatter and path (`GermanReferenceFormatterTests`, `ReaderPathTests`)

`Offb 2,1–5.7.9–10` / `Röm 8` / `Röm 8–9` / `1Mo 1,1–2,3` / `Röm 8,28; 12,1–2` / `Joh 3,16; Röm 8,28` / `Offb 2,1–5; 7; 9–10` (round trip: format → parse gives the same spans) and paths `/bible/66/2?v=1-5,7,9-10#v1`, `/bible/45/8`, `/bible/1/1?v=1-31#v1`, `/bible/45/12?v=1-2#v1`.

## H. Frontend (Vitest)

`parseVerseSelection`: `1-5,7,9-10`, `7`, `5-3` (ignored), `x`, `` · `ReferenceCard`: renders, Enter navigates, candidates replace the book · `ScriptureText`: links, Strong's links in text, preview on hover and focus, no preview on coarse pointer · `ChapterPage`: `?v=` marks exactly those verses.
