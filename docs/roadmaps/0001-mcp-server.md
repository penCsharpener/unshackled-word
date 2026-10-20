# Roadmap 0001: Bible Study MCP Server (.NET / C#)

## Goal

An [MCP](https://modelcontextprotocol.io) server that lets an AI assistant (Claude Desktop, Claude Code, any MCP
client) support a user in studying the Bible with the data in the `unshackled-word` Postgres database:

- read the **Elberfelder 1871** text of any passage,
- show the underlying **Greek (NT)** or **Hebrew (OT)** words with lemma, Strong's number, morphology and gloss,
- pull in **cross-references from the Treasury of Scripture Knowledge (TSK)**,
- build **alignments / interlinears** between German and the original language on request,
- support **word studies** (lexicon, how the Elberfelder translates a word, occurrences, full-text search),
- add **people/places**, **textual variants**, MCP **resources** and **prompts**,
- run as an HTTP service in `compose.yml`.

The server is **read-only**. It never writes to the database.

## Decisions (interview 2026-10-17)

| Topic | Decision |
|---|---|
| Transport | **HTTP only** (Streamable HTTP via `ModelContextProtocol.AspNetCore`). No stdio. |
| Query layer | **Repositories**: interfaces in `Application`, implementations in `Infrastructure`, so WebApi / BibleTagger can reuse them. MCP tools stay thin. |
| Tests | **Unit tests only** for now (NSubstitute / fake `IDbReader`). An integration test database comes later and is not part of this roadmap. |
| Language | Tool names, descriptions and output labels in **English**. Bible text stays German. References are *displayed* in German notation (`Joh 3,16`). |
| Auth | **None** for now. The server is meant for local / docker-network use. |
| German book names | **In code**, in `BibleBook` (Domain), not in the `BibleBooks` table. |
| Scope | Everything: core + cross-references, interlinear, word study + search, extras. |
| Docker | Yes, Dockerfile + `compose.yml` service as one of the last steps. |

## How to use this roadmap

- Work through the steps **in the order of the table**, or in any order allowed by the **Depends on** column.
- One step = one small, self-contained change (ideally one commit / one PR). After every step the solution compiles
  and all tests pass.
- When a step is finished and its **Done when** criteria are met, change its `[ ]` to `[x]`.
- Each step lists the exact files to create or change. Do **not** touch files that are not listed unless the build
  requires it; if a step turns out to need more, update this roadmap first.
- Read the [Conventions](#conventions-for-all-steps) once before starting any step.

## Steps

| ✔ | ID | Step | Depends on | Done when |
|---|---|---|---|---|
| [ ] | S01 | Fix `BibleReferenceRange` constructor and `IsMultpleVerses` | – | Range given in reverse order is swapped; tests for swap, same verse, multi-verse/chapter/book pass |
| [ ] | S02 | German book abbreviations in `BibleBook` | – | `BibleBook.FindByAbbreviation("Joh")` returns book 43; every book has ≥ 1 German abbreviation; tests pass |
| [ ] | S03 | `BibleReferenceParser` (text → `BibleReferenceRange`) | S01, S02 | All formats from the step's test table parse; invalid input returns an error message, never throws |
| [ ] | S04 | `RefIdRange` + `Testament` helper | S01 | Chapter-only, verse and range inputs produce correct start/end ref ids; OT/NT detected; tests pass |
| [ ] | S05 | `GermanReferenceFormatter` (ref id → `Joh 3,16`) | S02 | Single refs, verse ranges and chapter ranges format as specified; tests pass |
| [ ] | S06 | `StrongsNumber` value object + parser | – | All formats from the step's test table parse to language/number/extra; tests pass |
| [ ] | S07 | Create `UnshackledWord.Tooling.McpServer` (HTTP) with `list_bible_books` tool | S02 | `dotnet run` serves MCP at `/mcp`; MCP Inspector lists and calls `list_bible_books`; new test project passes |
| [ ] | S08 | Test wiring for `Infrastructure.Tests` + `FakeDbReader` | – | `Infrastructure.Tests` references Infrastructure/Application; `FakeDbReader` records SQL + params; its own tests pass |
| [ ] | S09 | `IVersificationRepository` (HebRefId ↔ LxxRefId) | S08 | Both methods implemented, registered in `AddRepositories()`, unit tests verify SQL fragments and parameters |
| [ ] | S10 | `IElb1871VerseRepository` | S08 | Range and LxxRefId methods implemented, registered, unit-tested |
| [ ] | S11 | `PassageFormatter` | S05 | Formats verses as specified; empty input gives "No verses found"; tests pass |
| [ ] | S12 | Tool `get_passage` | S03, S04, S07, S10, S11 | Tool returns German text for `Joh 3,16-18`; invalid reference returns error text; too large range is truncated with a note; tests pass |
| [ ] | S13 | `IStepGreekWordRepository` | S08 | Method implemented incl. morphology join, registered, unit-tested |
| [ ] | S14 | `IStepHebrewWordRepository` | S08 | Method implemented, registered, unit-tested |
| [ ] | S15 | `OriginalTextFormatter` | S05 | Greek and Hebrew word tables per verse formatted as specified; tests pass |
| [ ] | S16 | Tool `get_original_text` | S03, S04, S07, S09, S13, S14, S15 | NT reference returns Greek, OT returns Hebrew; tests with substituted repositories pass |
| [ ] | S17 | `ITskRepository` (outgoing + incoming) | S08 | Both methods implemented, registered, unit-tested |
| [ ] | S18 | `CrossReferenceGrouper` | S17 | Groups by verse + `Scope`, keeps order, applies limit and reports truncation; tests pass |
| [ ] | S19 | `CrossReferenceFormatter` | S05, S18 | Groups formatted with German references (+ optional text); tests pass |
| [ ] | S20 | Tool `get_cross_references` | S03, S04, S07, S09, S10, S17, S19 | Outgoing and incoming references with optional text; tests pass |
| [ ] | S21 | `IAlignmentRepository.GetGreekAlignmentAsync` | S08 | Method implemented, registered, unit-tested |
| [ ] | S22 | `IAlignmentRepository.GetHebrewAlignmentAsync` | S21 | Method implemented, unit-tested |
| [ ] | S23 | `IAlignmentRepository.GetUnmappedOriginalWordsAsync` | S21 | Greek + Hebrew variants implemented, unit-tested |
| [ ] | S24 | `InterlinearBuilder` | S21, S23 | Builds German-order and original-order rows incl. added and untranslated words; tests pass |
| [ ] | S25 | `InterlinearFormatter` | S05, S24 | Both orders and the column options formatted as specified; tests pass |
| [ ] | S26 | Tool `get_interlinear` | S03, S04, S07, S09, S22, S25 | NT and OT interlinear for a verse in both orders; one-chapter limit enforced; tests pass |
| [ ] | S27 | `IHebrewMorphemeRepository` | S08 | Method implemented, registered, unit-tested |
| [ ] | S28 | `HebrewWordAnalysisFormatter` | S05, S27 | Prefix / root / suffix lines formatted as specified; tests pass |
| [ ] | S29 | Tool `get_hebrew_word_analysis` | S03, S04, S07, S09, S14, S28 | Morphemes of an OT verse returned; NT reference returns a clear error; tests pass |
| [ ] | S30 | `get_interlinear`: option `morphemes` for OT | S26, S27 | With `morphemes=true` each Hebrew word lists its morphemes; default output unchanged; tests pass |
| [ ] | S31 | `IStrongsLexiconRepository` | S06, S08 | Method implemented, registered, unit-tested |
| [ ] | S32 | Tool `lookup_strongs` + `LexiconFormatter` | S07, S31 | `G26` and `H1254A` return entries; long lexicon text shortened unless `full=true`; tests pass |
| [ ] | S33 | `IWordStudyRepository.GetElberfelderRenderingsAsync` | S06, S08 | Greek + Hebrew SQL implemented, registered, unit-tested |
| [ ] | S34 | Tool `get_elberfelder_renderings` | S05, S07, S33 | Renderings with counts and first occurrence formatted; tests pass |
| [ ] | S35 | `IWordStudyRepository.FindOccurrencesAsync` (paged) | S33 | Paged SQL + count implemented, unit-tested |
| [ ] | S36 | Tool `find_occurrences` | S04, S05, S07, S35 | Paging and optional book-range filter work; tests pass |
| [ ] | S37 | `ISearchRepository.SearchElberfelderAsync` | S08 | German FTS SQL with ranking + paging implemented, registered, unit-tested |
| [ ] | S38 | Tool `search_elberfelder` | S05, S07, S37 | Results with reference + text + page info; tests pass |
| [ ] | S39 | `ISearchRepository.SearchOriginalAsync` | S37 | Original-language FTS SQL implemented, unit-tested |
| [ ] | S40 | Tool `search_original` | S05, S07, S39 | Results with reference + original text; tests pass |
| [ ] | S41 | `IPeopleAndPlacesRepository` | S08 | Persons, places and other entries of a passage found via Strong's numbers; unit-tested |
| [ ] | S42 | Tool `get_people_and_places` | S03, S04, S07, S09, S41 | Entries grouped by type with brief text; tests pass |
| [ ] | S43 | `ITextualVariantRepository` | S08 | SBL apparatus + edition flags query implemented, unit-tested |
| [ ] | S44 | Tool `get_textual_variants` | S03, S04, S07, S09, S43 | NT verse returns apparatus and words missing in NA or TR; OT returns clear error; tests pass |
| [ ] | S45 | MCP resource templates | S12, S32 | `bible://elb1871/{book}/{chapter}` and `bible://strongs/{number}` readable in MCP Inspector; tests pass |
| [ ] | S46 | MCP prompts `study_passage`, `word_study` | S12, S16, S20, S32, S34 | Prompts listed and rendered in MCP Inspector; tests pass |
| [ ] | S47 | Dockerfile + `compose.yml` service | S12 | `docker compose up` starts the server; MCP Inspector connects to `http://localhost:7655/mcp` |
| [ ] | S48 | README: MCP server section | S47 | README explains running, compose, and connecting Claude Code / Claude Desktop / MCP Inspector |

---

## Background knowledge

### Tables used

All tables live in schema `"unshackled-word"` (see `src/UnshackledWord.Tooling.Postgres.Migration/scripts/`).

| Purpose | Tables | Keyed by |
|---|---|---|
| Elberfelder verse text (+ German FTS vector) | `Elb1871Verses` | `HebRefId` (unique), `LxxRefId` |
| Elberfelder words | `Elb1871Words` | `HebRefId`, `PositionInVerse` |
| Greek NT words (STEP) | `StepGreekWords` | `LxxRefId`, `PositionInVerse` |
| Hebrew OT words (STEP) | `StepHebrewWords` | `LxxRefId`, `PositionInVerse` |
| Hebrew words split into prefix / root / suffix | `StepHebrewWordsNormalized`, `StepHebrewWordsNormalizedToHebrewWords` | `StepHebrewWordsId`, `PositionInWord` |
| German ↔ Greek alignment | `Elb1871GreekMapping` | `ElbWordId` → `StepWordId` (`StepGreekWords.Id`) |
| German ↔ Hebrew alignment | `Elb1871HebrewMapping` | `ElbWordId` → `StepWordId` (`StepHebrewWords.Id`) |
| Morphology code → readable parts | `StepGreekMorphology`, `StepHebrewMorphology` | `Code` |
| Lexicon | `StepStrongsLexicon` | `LanguageId` (0 Hebrew, 1 Aramaic, 2 Greek – see `StrongsLanguage`), `Number`, `Extra` |
| Cross-references | `Tsk` | `LxxRefId` → `RelatedStartLxxRefId` / `RelatedEndLxxRefId` |
| Versification bridge | `BibleVerseCountingMapping` | `HebRefId` ↔ `LxxRefId` |
| Original-language verse text (+ FTS) | `StepBibleVerses` | `HebRefId`, `LxxRefId` |
| Persons / places / other names | `StepPersonLexicon`, `StepPlaceLexicon`, `StepOtherLexicon` | `Strongs` (their `LxxRefId` is only the **first occurrence**) |
| SBL GNT apparatus | `SblApparatus` | `LxxRefId` |

### Reference ids and versification

A reference id is `BookId * 1_000_000 + Chapter * 1_000 + Verse` (`BibleReference.RefId`), e.g. John 3:16 = `43003016`.
A whole chapter is the range `BookId*1_000_000 + Chapter*1_000 + 0` … `+ 999`.

There are two verse numberings:

- **`HebRefId`** – Hebrew/German numbering. Used by `Elb1871Words`, `Elb1871Verses`, the mapping tables.
- **`LxxRefId`** – English/LXX numbering. STEP files have the header `Eng (Heb) Ref`; the importer stores the English
  reference as `LxxRefId`. `Tsk` also uses it.

They differ in some OT books (Psalm headings, Joel 3/4, Malachi 3/4, …). **Rules:**

1. The user always addresses passages in Elberfelder (`HebRefId`) numbering.
2. To read STEP or TSK data, translate via `BibleVerseCountingMapping` (this is what `AiMappingImportRunnerRepository`
   does when it builds the alignments, so it is consistent with the mapping data).
3. Never compare a `HebRefId` with an `LxxRefId` directly.
4. To display a TSK target, translate its `LxxRefId` back to a `HebRefId`.

`BookId` 1–39 is OT (Hebrew), 40–66 is NT (Greek).

### Alignment semantics (from `AiMappingImportRunner`)

- One mapping row per (German word, original word). Several rows per `ElbWordId` → the German word covers several
  original words; `GermanWordPart` says which part of the German word belongs to which original word.
- `IsAddedWord = true` → German word without equivalent in the original (traditionally italic). Its `StepWordId` is
  `NULL` and `ParentGermanWordId` points to the German word it belongs to.
- `Elb1871*Mapping.PositionInVerse` = position of the German word in the verse.
- An original word that appears in no mapping row is untranslated (e.g. Greek article).

---

## Conventions for all steps

### Build, test, Definition of Done

```sh
dotnet build UnshackledWord.slnx
dotnet test
dotnet test --project tests/UnshackledWord.Domain.Tests --filter-method "*BibleReferenceParser*"
```

Every step is done only when, in addition to its own **Done when**:

- `dotnet build UnshackledWord.slnx` succeeds without new warnings,
- `dotnet test` passes,
- new public classes have unit tests,
- the step's checkbox is ticked in this file.

### Where code goes

| Kind | Location | Namespace |
|---|---|---|
| Domain helpers (parser, formatter of refs, Strong's) | `src/UnshackledWord.Domain/Models/BibleStructure/` and `.../Models/Study/` | `UnshackledWord.Domain.Models.…` |
| Read models returned by repositories | `src/UnshackledWord.Domain/Models/Study/` | `UnshackledWord.Domain.Models.Study` |
| Repository interfaces | `src/UnshackledWord.Application/Repositories/Study/` | `UnshackledWord.Application.Repositories.Study` |
| Repository implementations | `src/UnshackledWord.Infrastructure/Repositories/Study/` | `UnshackledWord.Infrastructure.Repositories.Study` |
| Repository registration | `src/UnshackledWord.Infrastructure/Repositories/ServiceCollectionExtensions.cs` → `AddRepositories()` | |
| MCP tools | `src/UnshackledWord.Tooling.McpServer/Tools/` | `UnshackledWord.Tooling.McpServer.Tools` |
| Output formatters (pure, static) | `src/UnshackledWord.Tooling.McpServer/Formatting/` | `UnshackledWord.Tooling.McpServer.Formatting` |
| Pure builders/groupers | `src/UnshackledWord.Tooling.McpServer/Services/` | `UnshackledWord.Tooling.McpServer.Services` |
| Domain tests | `tests/UnshackledWord.Domain.Tests/Model/…` | mirrors source |
| Repository tests | `tests/UnshackledWord.Infrastructure.Tests/Repositories/Study/` | mirrors source |
| Tool / formatter tests | `tests/UnshackledWord.Tooling.McpServer.Tests/` | mirrors source |

Do **not** reuse the old `TskDbo` – it does not match the `Tsk` table. Create new read models in `Models/Study`.
Read models are `public sealed class` with `{ get; init; }` properties so Dapper can map them; property names must
equal the SQL column aliases.

### SQL style

Follow `src/UnshackledWord.Tooling.WebApi/Endpoints/Elberfelder/GetWordsForChapter/Endpoint.cs`:

- raw string literal (`"""…"""`), one column per line with leading comma,
- quoted, schema-qualified identifiers and short aliases (`ew`, `ev`, `egm`, `ehm`, `sgw`, `shw`, `t`, …),
- `WHERE 1=1` followed by one `AND` per condition,
- explicit `ORDER BY` on reference + position,
- column aliases matching the read-model property names.

**Difference to the endpoint:** never interpolate values. Every value is a Dapper parameter (`@StartRefId`,
`= ANY(@LxxRefIds)` with an `int[]`, `@Strongs`). Inputs come from an LLM.

Execute through `IDbReader.ReadAsListAsync<T>(sql, param)`; return `IReadOnlyList<T>` (`.ToList()`).

### Repository template

```csharp
public sealed class Elb1871VerseRepository : IElb1871VerseRepository
{
    private readonly IDbReader _dbReader;

    public Elb1871VerseRepository(IDbReader dbReader)
    {
        _dbReader = dbReader;
    }

    public async Task<IReadOnlyList<ElbVerse>> GetByHebRefIdRangeAsync(int startRefId, int endRefId, CancellationToken token = default)
    {
        const string sql = """
                           SELECT ev."HebRefId"
                                , ev."LxxRefId"
                                , ev."VerseText"
                           FROM "unshackled-word"."Elb1871Verses" ev
                           WHERE 1=1
                               AND ev."HebRefId" BETWEEN @StartRefId AND @EndRefId
                           ORDER BY ev."HebRefId"
                           """;

        var rows = await _dbReader.ReadAsListAsync<ElbVerse>(sql, new { StartRefId = startRefId, EndRefId = endRefId });
        return rows.ToList();
    }
}
```

### Repository test template (with `FakeDbReader` from S08)

```csharp
public class Elb1871VerseRepositoryTests
{
    [Fact]
    public async Task GetByHebRefIdRangeAsync_PassesRangeAsParameters()
    {
        var reader = new FakeDbReader().Returns(new ElbVerse { HebRefId = 43003016, LxxRefId = 43003016, VerseText = "Denn also..." });
        var sut = new Elb1871VerseRepository(reader);

        var result = await sut.GetByHebRefIdRangeAsync(43003016, 43003018);

        result.Count.ShouldBe(1);
        reader.LastSql.ShouldContain("\"Elb1871Verses\"");
        reader.LastSql.ShouldContain("BETWEEN @StartRefId AND @EndRefId");
        reader.LastParam("StartRefId").ShouldBe(43003016);
        reader.LastParam("EndRefId").ShouldBe(43003018);
    }
}
```

### MCP tool template

Tools are `static` methods on a `static` class marked `[McpServerToolType]`. Services are injected as method
parameters (supported by the C# MCP SDK); this keeps tools trivially testable. Tools **never throw** for user errors:
they return a short English error text the model can act on.

```csharp
[McpServerToolType]
public static class PassageTools
{
    [McpServerTool(Name = "get_passage", ReadOnly = true)]
    [Description("Returns the German Elberfelder 1871 text of a Bible passage. ...")]
    public static async Task<string> GetPassage(
        IElb1871VerseRepository verses,
        [Description("Bible reference in German or English notation, e.g. 'Joh 3,16-18', 'John 3:16', 'Röm 8', '1Mo 1,1-5'.")] string reference,
        CancellationToken cancellationToken)
    {
        var parsed = BibleReferenceParser.Parse(reference);
        if (parsed.Value is null)
        {
            return parsed.Message!;
        }
        ...
    }
}
```

Tool tests call the static method directly with `Substitute.For<IRepository>()` and assert on the returned string.

### Output format

- Plain text / Markdown, compact. One header line per verse: `Joh 3,16`.
- Tables only where columns help (original words, interlinear).
- Every tool that limits output appends a line like `(Truncated: showing 30 of 112 cross-references. Ask with offset=30 for more.)`.
- English labels, German Bible text.

---

## Step details

### S01 – Fix `BibleReferenceRange` constructor and `IsMultpleVerses`

**Why:** The constructor in `src/UnshackledWord.Domain/Models/BibleStructure/BibleReferenceRange.cs` compares the
*properties* `Start > End` before they are assigned (both default), so a reversed range is never swapped.
`IsMultpleVerses` checks `IsMultipleBooks` twice instead of also checking chapters.

**Change:**
- Compare the parameters: `if (start > end) { Start = end; End = start; return; }`.
- `IsMultpleVerses => IsMultpleChapters || Start.Verse < End.Verse;`
- Do not rename the misspelled properties (they are public API used elsewhere).

**Tests** (`tests/UnshackledWord.Domain.Tests/Model/BibleStructure/BibleReferenceRangeTests.cs`):
reversed input is swapped; same start/end → all `IsMultiple*` false; verse range in one chapter; chapter range;
book range; `ToString()` for each case.

### S02 – German book abbreviations in `BibleBook`

**Change** `src/UnshackledWord.Domain/Models/BibleStructure/BibleBook.cs` (and where `AllBooks` is built):
- Add `public string GermanName { get; }` and `public string[] GermanAbbreviations { get; }` and constructor parameters.
- Fill them for all 66 books. Use the common Elberfelder/Luther abbreviations, first entry = display abbreviation:
  `1Mo|Gen` (Genesis), `2Mo|Ex`, `3Mo|Lev`, `4Mo|Num`, `5Mo|Dtn`, `Jos`, `Ri`, `Rt`, `1Sam`, `2Sam`, `1Kön|1Kö`,
  `2Kön|2Kö`, `1Chr`, `2Chr`, `Esr`, `Neh`, `Est`, `Hi|Hiob`, `Ps`, `Spr`, `Pred|Koh`, `Hl`, `Jes`, `Jer`, `Kla|Klgl`,
  `Hes|Ez`, `Dan`, `Hos`, `Joe|Joel`, `Am`, `Ob|Obd`, `Jon`, `Mi`, `Nah`, `Hab`, `Zeph|Zef`, `Hag`, `Sach`, `Mal`,
  `Mt`, `Mk`, `Lk`, `Joh`, `Apg`, `Röm`, `1Kor`, `2Kor`, `Gal`, `Eph`, `Phil`, `Kol`, `1Thes|1Thess`, `2Thes|2Thess`,
  `1Tim`, `2Tim`, `Tit`, `Phlm|Phim`, `Hebr|Heb`, `Jak`, `1Petr|1Pet`, `2Petr|2Pet`, `1Joh`, `2Joh`, `3Joh`, `Jud`, `Offb`.
  German names: `1. Mose`, …, `Offenbarung`.
- `FindByAbbreviation` searches English abbreviations, German abbreviations, `Name` and `GermanName`,
  case-insensitive, ignoring spaces and a trailing dot (`"1 Kor."` = `"1Kor"`).
- **Ambiguity:** English `Jo` (Joshua and John) and German `Jos`/English `Jos` – English list wins for exact English
  matches, but German `Joh` must resolve to John. Add a test for `Jo`, `Jos`, `Joh`, `Phil`, `Ph`.

- Umlaut spellings: also accept `Roem`, `1Koen`, `2Koen` as German abbreviations (users without umlaut keyboards).

**Tests:** every book has ≥ 1 German abbreviation and a German name; `FindByAbbreviation` for `Joh`, `1Mo`, `Röm`,
`Roem`, `Offb`, `1 Kor.`, `Genesis`, `Johannes`.

### S03 – `BibleReferenceParser`

**Create** `src/UnshackledWord.Domain/Models/BibleStructure/BibleReferenceParser.cs`:
`public static Result<BibleReferenceRange> Parse(string input)` (uses the existing `Result<T>` in
`Domain/Models/Result.cs`: `Value` on success, `Message` on error).

Supported formats (chapter/verse separator `,` or `:`; range `-` or `–`):

| Input | Result |
|---|---|
| `Joh 3,16` / `John 3:16` | 43:3:16 – 43:3:16 |
| `Joh 3,16-18` | 43:3:16 – 43:3:18 |
| `Joh 3,16-4,2` | 43:3:16 – 43:4:2 |
| `Joh 3` | 43:3:1 – 43:3:last verse (`BibleBook.GetVerseCountInChapter(chapter, isHebrewCounting: true)`) |
| `Joh 3-4` | 43:3:1 – 43:4:last |
| `1Mo 1,1` / `1. Mose 1,1` / `Gen 1:1` | 1:1:1 |
| `Röm 8,28-39` | 45:8:28 – 45:8:39 |

Errors (return `Message`, never throw): unknown book (`Unknown book 'Xyz'. Use e.g. 'Joh 3,16' or 'John 3:16'.`),
chapter 0 / beyond book, verse 0 / beyond chapter, empty input. Use a single compiled `Regex`; split book part
from the numeric part at the last space before the first digit that follows a letter (books may start with a digit).

**Tests:** one `[Theory]` with all table rows, one with all error cases.

### S04 – `RefIdRange` + `Testament`

**Create** `src/UnshackledWord.Domain/Models/BibleStructure/RefIdRange.cs`:

```csharp
public readonly record struct RefIdRange(int StartRefId, int EndRefId)
{
    public static RefIdRange From(BibleReferenceRange range);
    public static RefIdRange WholeChapter(int bookId, int chapter); // verse 0 .. 999
    public Testament Testament { get; }          // from StartRefId / 1_000_000 (1–39 Old, 40–66 New)
    public bool SpansTestaments { get; }          // start OT, end NT
    public int ChapterCount { get; }              // number of distinct chapters, for limits
}
public enum Testament { Old, New }
```

**Tests:** single verse, chapter, cross-chapter, OT vs NT, spanning testaments.

### S05 – `GermanReferenceFormatter`

**Create** `src/UnshackledWord.Domain/Models/BibleStructure/GermanReferenceFormatter.cs`:
- `Format(int refId)` → `Joh 3,16` (first German abbreviation from S02).
- `Format(int startRefId, int endRefId)` → `Joh 3,16-18`, `Joh 3,16-4,2`, `Joh 3,16 - Apg 1,1`.

**Tests:** each case, plus books starting with a digit (`1Kor 13,4`).

### S06 – `StrongsNumber`

**Create** `src/UnshackledWord.Domain/Models/Study/StrongsNumber.cs`:
`public readonly record struct StrongsNumber(StrongsLanguage Language, int Number, string? Extra)` with
`static Result<StrongsNumber> Parse(string input)` and `ToString()` → STEP format (`G0026`, `H1254A`).

| Input | Language | Number | Extra |
|---|---|---|---|
| `G26`, `g26`, `G0026` | Greek | 26 | null |
| `H1254A` | Hebrew | 1254 | `A` |
| `{H7225G}` | Hebrew | 7225 | `G` |
| `H9003/{H7225G}` | Hebrew | 7225 | `G` (take the braced root part) |
| `26` | error: "Prefix G (Greek) or H (Hebrew) is required." |

Aramaic: the input format is also `H…`; the language is decided by the lexicon row later, so `Parse` only yields
Hebrew or Greek.

**Tests:** one `[Theory]` with all rows, error cases.

### S07 – Create `UnshackledWord.Tooling.McpServer`

**Create** `src/UnshackledWord.Tooling.McpServer/UnshackledWord.Tooling.McpServer.csproj` (`Microsoft.NET.Sdk.Web`,
`DockerDefaultTargetOS` Linux), references `Persistence.Postgres` and `Infrastructure` projects.

**Packages:** add to `Directory.Packages.props`: `ModelContextProtocol.AspNetCore` (latest stable – 2.2.0 at the
time of writing; check the package README for the current API before coding). Reuse `Serilog.AspNetCore`.

**Program.cs** (same shape as WebApi):

```csharp
public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Configuration.AddEnvironmentVariables("UNSHACKLEDWORD_");
        AddLocalSecrets(builder.Configuration);

        builder.Services.AddSerilog((_, logger) => logger.ReadFrom.Configuration(builder.Configuration));
        builder.Services.AddPostgresPersistence();
        builder.Services.AddInfrastructureServices();
        builder.Services.AddRepositories();
        builder.Services.AddMcpServer()
            .WithHttpTransport(options => options.Stateless = true)
            .WithToolsFromAssembly();

        var app = builder.Build();
        app.MapMcp("/mcp");
        app.Run();
    }

    static partial void AddLocalSecrets(ConfigurationManager builder);
}
```

- `appsettings.json` with Serilog console config, `Properties/launchSettings.json` with `http://localhost:5143`.
- `Tools/MetaTools.cs`: tool `list_bible_books` – returns `Id | German name | German abbr. | English name | Testament`
  for all 66 books from `BibleBook.AllBooks` (no DB access). Description tells the model it can use these
  abbreviations in all other tools.
- Add the project to `UnshackledWord.slnx` under `/src/`.

**Create** `tests/UnshackledWord.Tooling.McpServer.Tests/` (copy the csproj shape of `UnshackledWord.Domain.Tests`,
reference the McpServer project), add it to `.slnx` under `/tests/`. Test: `list_bible_books` returns 66 lines with
`Joh` and `Offb`.

**Manual check:** `dotnet run --project src/UnshackledWord.Tooling.McpServer`, then
`npx @modelcontextprotocol/inspector` → Streamable HTTP → `http://localhost:5143/mcp` → call `list_bible_books`.

### S08 – Test wiring + `FakeDbReader`

- `tests/UnshackledWord.Infrastructure.Tests/UnshackledWord.Infrastructure.Tests.csproj`: add project references to
  `src/UnshackledWord.Infrastructure` and `src/UnshackledWord.Application`.
- **Create** `tests/UnshackledWord.TestTooling/FakeDbReader.cs` implementing `IDbReader`
  (TestTooling needs a reference to `Application`):
  - `FakeDbReader Returns<T>(params T[] rows)` – queues rows for the next `ReadAsListAsync<T>` call (FIFO, so a test
    can prepare several calls),
  - `List<(string Sql, object? Param)> Calls`, `string LastSql`, `object? LastParam(string name)` (reads the property
    of the anonymous param object via reflection),
  - `ReadFirstOrDefaultAsync` / `ExecuteScalarAsync` work the same way, `ReadMultipleAsListAsync` throws
    `NotSupportedException`.
- **Tests** for `FakeDbReader` itself in `Infrastructure.Tests` (queue order, param lookup).

### S09 – `IVersificationRepository`

Read model `VerseKey { int HebRefId; int LxxRefId; }`.

```csharp
Task<IReadOnlyList<VerseKey>> GetByHebRefIdRangeAsync(int startRefId, int endRefId, CancellationToken token = default);
Task<IReadOnlyList<VerseKey>> GetByLxxRefIdsAsync(int[] lxxRefIds, CancellationToken token = default);
```

```sql
SELECT bvcm."HebRefId"
     , bvcm."LxxRefId"
FROM "unshackled-word"."BibleVerseCountingMapping" bvcm
WHERE 1=1
    AND bvcm."HebRefId" BETWEEN @StartRefId AND @EndRefId
ORDER BY bvcm."HebRefId", bvcm."LxxRefId"
```

Second method: `AND bvcm."LxxRefId" = ANY(@LxxRefIds)`, same order.
Register in `AddRepositories()`. Tests: SQL fragments + parameters for both methods.

### S10 – `IElb1871VerseRepository`

Read model `ElbVerse { int HebRefId; int LxxRefId; string VerseText; }`.
Methods: `GetByHebRefIdRangeAsync(start, end)` (SQL in the template above) and `GetByHebRefIdsAsync(int[] hebRefIds)`
(`AND ev."HebRefId" = ANY(@HebRefIds)`). Tests as in the template.

> Note: `Elberfelder/GetVersesOfChapter/Endpoint.cs` filters `Elb1871Verses` by `"BibleBookId"` / `"Chapter"`, which
> do not exist in the schema. Don't copy that query. (Fixing the endpoint is out of scope for this roadmap.)

### S11 – `PassageFormatter`

`static string Format(IReadOnlyList<ElbVerse> verses, string? truncationNote = null)`:

```
Joh 3,16  Denn also hat Gott die Welt geliebt, ...
Joh 3,17  Denn Gott hat seinen Sohn nicht ...
```

Header line `Elberfelder 1871 – Joh 3,16-17` first. Empty list → `No verses found.` Tests for all cases.

### S12 – Tool `get_passage`

`Tools/PassageTools.cs`, parameters: `reference` (string). Flow: `BibleReferenceParser.Parse` → `RefIdRange.From`
→ if `ChapterCount > 3`, cut to the first 3 chapters and add a truncation note → repository → `PassageFormatter`.
Description: *"Returns the German Elberfelder 1871 text. Max. 3 chapters per call."*
Tests (substitute `IElb1871VerseRepository`): valid reference, invalid reference returns parser message and never
calls the repository, more than 3 chapters → truncation note.

### S13 – `IStepGreekWordRepository`

Read model `GreekWord` with the columns below. Method `GetByLxxRefIdsAsync(int[] lxxRefIds)`.

```sql
SELECT sgw."Id"
     , sgw."LxxRefId"
     , sgw."PositionInVerse"
     , sgw."Greek"
     , sgw."Transliteration"
     , sgw."Lemma"
     , sgw."DisambiguatedStrongs" "Strongs"
     , sgw."Morphology"
     , sgm."PartOfSpeech"
     , sgm."Tense"
     , sgm."Voice"
     , sgm."Mood"
     , sgm."Person"
     , sgm."Case"
     , sgm."Number"
     , sgm."Gender"
     , sgw."Gloss"
     , sgw."German"
     , sgw."IsInNestleAland"
     , sgw."IsInTextusReceptus"
FROM "unshackled-word"."StepGreekWords" sgw
    LEFT JOIN "unshackled-word"."StepGreekMorphology" sgm ON sgw."Morphology" = sgm."Code"
WHERE 1=1
    AND sgw."LxxRefId" = ANY(@LxxRefIds)
ORDER BY sgw."LxxRefId", sgw."PositionInVerse"
```

### S14 – `IStepHebrewWordRepository`

Read model `HebrewWord`. Method `GetByLxxRefIdsAsync(int[] lxxRefIds)`.

```sql
SELECT shw."Id"
     , shw."LxxRefId"
     , shw."PositionInVerse"
     , shw."Hebrew"
     , shw."Transliteration"
     , shw."RootDisambiguatedStrongsInstance" "Strongs"
     , shw."DisambiguatedStrongs"
     , shw."Grammar" "GrammaticalKey"
     , shw."Gloss"
FROM "unshackled-word"."StepHebrewWords" shw
WHERE 1=1
    AND shw."LxxRefId" = ANY(@LxxRefIds)
ORDER BY shw."LxxRefId", shw."PositionInVerse"
```

`Grammar` is the code of the whole word incl. prefixes; readable morphology comes per morpheme in S27–S30.

### S15 – `OriginalTextFormatter`

Two static methods, `FormatGreek(IReadOnlyList<GreekWord>, Func<int,int> lxxToHeb)` and `FormatHebrew(...)`.
Group by verse, header with the German reference of the **HebRefId**, then a table:

```
Joh 3,16
| # | Greek | Translit. | Lemma | Strong's | Morph. | Gloss |
| 1 | Οὕτως | Houtōs | οὕτω | G3779 | ADV | thus |
```

Greek morphology column: `Morphology` code plus readable parts joined (`V-AAI-3S aorist active indicative 3rd sg`).
Hebrew: no Lemma column; Hebrew text is right-to-left – put it in its own column, no extra marks.

### S16 – Tool `get_original_text`

`Tools/OriginalTextTools.cs`, parameter `reference`. Flow: parse → `RefIdRange` (max 1 chapter, else truncate) →
`IVersificationRepository.GetByHebRefIdRangeAsync` → `LxxRefIds` → Greek repo if `Testament.New`, Hebrew repo if
`Testament.Old` → formatter. Range spanning both testaments → error text. Tests: NT calls only the Greek repo, OT only
the Hebrew repo, spanning → error.

### S17 – `ITskRepository`

Read model `TskCrossReference { int Id; int LxxRefId; string Scope; int RelatedStartLxxRefId; int? RelatedEndLxxRefId; }`.

Outgoing (`GetOutgoingAsync(int[] lxxRefIds)`):

```sql
SELECT t."Id"
     , t."LxxRefId"
     , t."Scope"
     , t."RelatedStartLxxRefId"
     , t."RelatedEndLxxRefId"
FROM "unshackled-word"."Tsk" t
WHERE 1=1
    AND t."LxxRefId" = ANY(@LxxRefIds)
ORDER BY t."LxxRefId", t."Id"
```

Incoming (`GetIncomingAsync(int lxxRefId)`) – which verses point to this verse:

```sql
SELECT t."Id"
     , t."LxxRefId"
     , t."Scope"
     , t."RelatedStartLxxRefId"
     , t."RelatedEndLxxRefId"
FROM "unshackled-word"."Tsk" t
WHERE 1=1
    AND t."RelatedStartLxxRefId" <= @LxxRefId
    AND COALESCE(t."RelatedEndLxxRefId", t."RelatedStartLxxRefId") >= @LxxRefId
ORDER BY t."LxxRefId", t."Id"
```

### S18 – `CrossReferenceGrouper`

`Services/CrossReferenceGrouper.cs`, pure. Input: TSK rows + limit. Output:
`IReadOnlyList<CrossReferenceGroup>` (`SourceLxxRefId`, `Scope`, `IReadOnlyList<(int Start, int? End)> Targets`)
plus `TotalCount` and `IsTruncated`. Keeps DB order, groups consecutive rows with same source + scope, cuts after
`limit` targets total. Tests: grouping, order, truncation, empty.

### S19 – `CrossReferenceFormatter`

```
Cross-references (TSK) for Joh 3,16
[Joh 3,16] "God so loved"
  - 1Joh 4,9-10: Daran ist die Liebe Gottes ...
  - Röm 5,8: Gott aber erweist seine Liebe ...
```

`Scope` is the TSK keyword text (English). Target references converted LxxRefId → HebRefId via a passed
`Func<int,int>`; text only if a verse text dictionary is passed; ranges show only the first 3 verses' text.

### S20 – Tool `get_cross_references`

Parameters: `reference`, `direction` (`outgoing` default | `incoming`), `includeText` (default `true`),
`limit` (default 30, max 100). Flow: parse → versification → TSK repo → grouper → versification for targets
(`GetByLxxRefIdsAsync`) → verse texts (`GetByHebRefIdsAsync`) only when `includeText` → formatter.
`incoming` accepts only a single verse (error text otherwise). Tests for both directions, includeText false never
calls the verse repository, limit applied.

### S21 – `IAlignmentRepository.GetGreekAlignmentAsync`

Read model `AlignmentRow` (`ElbWordId`, `HebRefId`, `ElbPosition`, `WordInContext`, `GermanWordPart`, `IsAddedWord`,
`ParentGermanWordId`, `StepWordId`, `OriginalPosition`, `Original`, `Transliteration`, `Lemma`, `Strongs`,
`GrammaticalKey`, `Gloss`). Method `GetGreekAlignmentAsync(int startRefId, int endRefId)`:

```sql
SELECT ew."Id" "ElbWordId"
     , ew."HebRefId"
     , ew."PositionInVerse" "ElbPosition"
     , ew."WordInContext"
     , egm."GermanWordPart"
     , egm."IsAddedWord"
     , egm."ParentGermanWordId"
     , sgw."Id" "StepWordId"
     , sgw."PositionInVerse" "OriginalPosition"
     , sgw."Greek" "Original"
     , sgw."Transliteration"
     , sgw."Lemma"
     , sgw."DisambiguatedStrongs" "Strongs"
     , sgw."Morphology" "GrammaticalKey"
     , sgw."Gloss"
FROM "unshackled-word"."Elb1871Words" ew
    LEFT JOIN "unshackled-word"."Elb1871GreekMapping" egm ON ew."Id" = egm."ElbWordId"
    LEFT JOIN "unshackled-word"."StepGreekWords" sgw ON egm."StepWordId" = sgw."Id"
WHERE 1=1
    AND ew."HebRefId" BETWEEN @StartRefId AND @EndRefId
ORDER BY ew."HebRefId", ew."PositionInVerse", sgw."PositionInVerse"
```

### S22 – `IAlignmentRepository.GetHebrewAlignmentAsync`

Same read model and shape, with `Elb1871HebrewMapping ehm` and `StepHebrewWords shw`:
`shw."Hebrew" "Original"`, `NULL "Lemma"`, `shw."RootDisambiguatedStrongsInstance" "Strongs"`,
`shw."Grammar" "GrammaticalKey"`.

### S23 – `IAlignmentRepository.GetUnmappedOriginalWordsAsync`

`GetUnmappedGreekWordsAsync(int[] lxxRefIds)` / `GetUnmappedHebrewWordsAsync(int[] lxxRefIds)` returning
`UnmappedWord` (`StepWordId`, `LxxRefId`, `PositionInVerse`, `Original`, `Strongs`, `Gloss`):

```sql
SELECT sgw."Id" "StepWordId"
     , sgw."LxxRefId"
     , sgw."PositionInVerse"
     , sgw."Greek" "Original"
     , sgw."DisambiguatedStrongs" "Strongs"
     , sgw."Gloss"
FROM "unshackled-word"."StepGreekWords" sgw
WHERE 1=1
    AND sgw."LxxRefId" = ANY(@LxxRefIds)
    AND NOT EXISTS (
        SELECT 1
        FROM "unshackled-word"."Elb1871GreekMapping" egm
        WHERE egm."StepWordId" = sgw."Id"
    )
ORDER BY sgw."LxxRefId", sgw."PositionInVerse"
```

Hebrew: same with `StepHebrewWords` / `Elb1871HebrewMapping`.

### S24 – `InterlinearBuilder`

`Services/InterlinearBuilder.cs`, pure. Input: alignment rows, unmapped words, `Func<int,int> lxxToHeb`.
Output per verse:

- `GermanOrder`: one entry per German word (ordered by `ElbPosition`) with its list of original words; added words
  flagged and linked to their parent word.
- `OriginalOrder`: one entry per original word (ordered by `OriginalPosition`, merged with the unmapped words) with
  the German word(s) rendering it, or none.

Tests: 1:1 word, German word → 2 original words (`GermanWordPart`), added word with parent, untranslated word,
German word with no mapping row at all (shown as "not aligned").

### S25 – `InterlinearFormatter`

German order:

```
Joh 3,16
| German | Original | Strong's | Morph. | Gloss |
| Denn | γάρ | G1063 | CONJ | for |
| also | Οὕτως | G3779 | ADV | thus |
| [hat] (added) | – | | | |
```

Original order: same columns starting with `Original`, untranslated words with German `—`.
Option `columns` (flags: `Transliteration`, `Lemma`, `Morphology`, `Gloss`; default `Morphology|Gloss`).

### S26 – Tool `get_interlinear`

Parameters: `reference`, `order` (`german` default | `original`), `columns` (comma-separated). Flow: parse →
`RefIdRange` (max 1 chapter) → versification → Greek or Hebrew alignment + unmapped words → builder → formatter.
Description explains both orders and that German words in "(added)" have no counterpart in the original.

### S27 – `IHebrewMorphemeRepository`

Read model `HebrewMorpheme` (`StepHebrewWordsId`, `PositionInWord`, `IsRoot`, `Hebrew`, `StrongsNumber`, `Grammar`,
`SuffixCode`, `PartOfSpeech`, `Stem`, `Form`, `Person`, `Gender`, `Number`, `State`). Method
`GetByStepWordIdsAsync(int[] stepHebrewWordIds)`:

```sql
SELECT shwn2h."StepHebrewWordsId"
     , shwn2h."PositionInWord"
     , shwn."IsRoot"
     , shwn."Hebrew"
     , shwn."StrongsNumber"
     , shwn."Grammar"
     , shwn."SuffixCode"
     , shm."PartOfSpeech"
     , shm."Stem"
     , shm."Form"
     , shm."Person"
     , shm."Gender"
     , shm."Number"
     , shm."State"
FROM "unshackled-word"."StepHebrewWordsNormalizedToHebrewWords" shwn2h
    INNER JOIN "unshackled-word"."StepHebrewWordsNormalized" shwn ON shwn2h."StepHebrewWordsNormalizedId" = shwn."Id"
    LEFT JOIN "unshackled-word"."StepHebrewMorphology" shm ON shwn."Grammar" = shm."Code"
WHERE 1=1
    AND shwn2h."StepHebrewWordsId" = ANY(@StepHebrewWordIds)
ORDER BY shwn2h."StepHebrewWordsId", shwn2h."PositionInWord"
```

### S28 – `HebrewWordAnalysisFormatter`

```
1Mo 1,1
1. בְּרֵאשִׁית (bere'shit)
   - prefix  בְּ   H9003  preposition "in"
   - root    רֵאשִׁית  H7225G noun feminine singular absolute "beginning"
```

### S29 – Tool `get_hebrew_word_analysis`

Parameters: `reference` (OT only, max 1 chapter). Flow: parse → versification → Hebrew words (S14) → morphemes (S27)
→ formatter. NT reference → `"Hebrew word analysis is only available for Old Testament passages."`.

### S30 – `get_interlinear` option `morphemes`

Add parameter `morphemes` (bool, default false, OT only). When true, load morphemes for the Hebrew `StepWordId`s and
pass them to the formatter, which prints them indented under each original word. Existing tests stay green; add tests
for the new option.

### S31 – `IStrongsLexiconRepository`

Read model `LexiconEntry` (`LanguageId`, `Number`, `Extra`, `DisambiguatedExtra`, `OriginalWord`, `Transliteration`,
`Morphology`, `Gloss`, `Lexicon`). Method `GetEntriesAsync(StrongsNumber strongs)`:

```sql
SELECT ssl."LanguageId"
     , ssl."Number"
     , ssl."Extra"
     , ssl."DisambiguatedExtra"
     , ssl."OriginalWord"
     , ssl."Transliteration"
     , ssl."Morphology"
     , ssl."Gloss"
     , ssl."Lexicon"
FROM "unshackled-word"."StepStrongsLexicon" ssl
WHERE 1=1
    AND ssl."LanguageId" = ANY(@LanguageIds)
    AND ssl."Number" = @Number
    AND (@Extra::varchar IS NULL OR ssl."Extra" = @Extra)
ORDER BY ssl."Extra"
```

`@LanguageIds` = `[2]` for Greek, `[0, 1]` for Hebrew (Aramaic words also use `H` numbers).

### S32 – Tool `lookup_strongs` + `LexiconFormatter`

Parameters: `strongs` (e.g. `G26`, `H1254A`), `full` (default false). Without `full`, `Lexicon` is cut to 800
characters with a note. Several entries (different `Extra`) → list all, each with its gloss. Tests: parse error,
no entry, one entry, multiple entries, truncation.

### S33 – `IWordStudyRepository.GetElberfelderRenderingsAsync`

Read model `Rendering` (`PlainWord`, `Occurrences`, `FirstHebRefId`). Greek:

```sql
SELECT ew."PlainWord"
     , COUNT(*) "Occurrences"
     , MIN(ew."HebRefId") "FirstHebRefId"
FROM "unshackled-word"."Elb1871GreekMapping" egm
    INNER JOIN "unshackled-word"."Elb1871Words" ew ON egm."ElbWordId" = ew."Id"
    INNER JOIN "unshackled-word"."StepGreekWords" sgw ON egm."StepWordId" = sgw."Id"
WHERE 1=1
    AND sgw."DisambiguatedStrongs" = @Strongs
GROUP BY ew."PlainWord"
ORDER BY COUNT(*) DESC
```

Hebrew: `Elb1871HebrewMapping` + `StepHebrewWords`, filter
`shw."RootDisambiguatedStrongsInstance" LIKE '%' || @Strongs || '%'` (the column can hold compound values; see
open questions). `@Strongs` = `StrongsNumber.ToString()`.

### S34 – Tool `get_elberfelder_renderings`

Parameter `strongs`. Output: `How the Elberfelder 1871 renders G0026 (ἀγάπη):` followed by
`Liebe – 106× (first: Mt 24,12)`. Tests with substituted repository.

### S35 – `IWordStudyRepository.FindOccurrencesAsync`

Parameters `strongs`, `startRefId?`, `endRefId?`, `pageSize`, `offset`. Returns `Occurrence` (`HebRefId`,
`VerseText`, `PlainWord`) and total count (two queries, same `FROM`/`WHERE`, like
`Elberfelder/Fts/GetElbWordOccurrence`). Use `LIMIT @PageSize OFFSET @Offset`.

### S36 – Tool `find_occurrences`

Parameters `strongs`, optional `withinReference` (e.g. `Röm-Phlm` or `Röm`; parsed into a ref range), `pageSize`
(default 20, max 100), `offset`. Output lists verses with the rendering word marked `**Liebe**`; footer with paging info.

### S37 – `ISearchRepository.SearchElberfelderAsync`

```sql
SELECT ev."HebRefId"
     , ev."VerseText"
     , ts_rank(ev."SearchVector", websearch_to_tsquery('german', @SearchTerm)) "Rank"
FROM "unshackled-word"."Elb1871Verses" ev
WHERE 1=1
    AND ev."SearchVector" @@ websearch_to_tsquery('german', @SearchTerm)
ORDER BY "Rank" DESC, ev."HebRefId"
LIMIT @PageSize OFFSET @Offset
```

Plus count query. Option `orderBy` (`rank` | `canonical`).

### S38 – Tool `search_elberfelder`

Parameters `query`, `orderBy`, `pageSize` (default 20, max 100), `offset`. Description explains websearch syntax
(`"exakte Phrase"`, `-ausschließen`, `oder`).

### S39 – `ISearchRepository.SearchOriginalAsync`

```sql
SELECT sbv."HebRefId"
     , sbv."VerseText"
     , ts_rank(sbv."SearchVector", websearch_to_tsquery('simple', @SearchTerm)) "Rank"
FROM "unshackled-word"."StepBibleVerses" sbv
WHERE 1=1
    AND sbv."SearchVector" @@ websearch_to_tsquery('simple', @SearchTerm)
ORDER BY "Rank" DESC, sbv."HebRefId"
LIMIT @PageSize OFFSET @Offset
```

Strip diacritics from the search term before querying (Greek and Hebrew, `string.Normalize(FormD)` + remove
non-spacing marks) because the vector is built from `NonDiacriticText` / `LemmaText`.

### S40 – Tool `search_original`

Parameters `query`, `pageSize`, `offset`. Output: German reference + original verse text.

### S41 – `IPeopleAndPlacesRepository`

`LxxRefId` in the name lexicons is only the first occurrence, so search via the Strong's numbers of the passage:

```sql
SELECT 'Person' "Type"
     , spl."Name"
     , spl."Strongs"
     , spl."Briefest"
     , spl."Brief"
FROM "unshackled-word"."StepPersonLexicon" spl
WHERE 1=1
    AND spl."Strongs" IN (
        SELECT shw."DisambiguatedStrongs"
        FROM "unshackled-word"."StepHebrewWords" shw
        WHERE shw."LxxRefId" = ANY(@LxxRefIds)
        UNION
        SELECT sgw."DisambiguatedStrongs"
        FROM "unshackled-word"."StepGreekWords" sgw
        WHERE sgw."LxxRefId" = ANY(@LxxRefIds)
    )
```

`UNION ALL` the same for `StepPlaceLexicon` (`'Place'`) and `StepOtherLexicon` (`'Other'`). Verify the Strong's
format of `StepPersonLexicon."Strongs"` against `DisambiguatedStrongs` first (see open questions); adjust the
comparison if needed.

### S42 – Tool `get_people_and_places`

Parameter `reference` (max 1 chapter). Output grouped by type: `Name (Strong's) – Brief`.

### S43 – `ITextualVariantRepository`

Two methods: `GetApparatusAsync(int[] lxxRefIds)` from `SblApparatus` (`LxxRefId`, `Text`) and
`GetEditionDifferencesAsync(int[] lxxRefIds)` from `StepGreekWords` where
`NOT (sgw."IsInNestleAland" AND sgw."IsInTextusReceptus")` (`LxxRefId`, `PositionInVerse`, `Greek`, `Strongs`,
`IsInNestleAland`, `IsInTextusReceptus`, `IsInOther`, `Editions`).

### S44 – Tool `get_textual_variants`

Parameter `reference` (NT only, max 1 chapter). Output per verse: SBL apparatus lines, then words found only in some
editions (`NA only`, `TR only`, `other`).

### S45 – MCP resource templates

`Resources/BibleResources.cs` with `[McpServerResourceType]`:
- `bible://elb1871/{book}/{chapter}` → `PassageFormatter` output (reuse S10/S11),
- `bible://strongs/{number}` → `LexiconFormatter` output (reuse S31/S32).
Register with `.WithResourcesFromAssembly()`. Tests call the methods directly.

### S46 – MCP prompts

`Prompts/StudyPrompts.cs` with `[McpServerPromptType]`, registered with `.WithPromptsFromAssembly()`:
- `study_passage(reference)` – instructs the model to call `get_passage`, `get_original_text`,
  `get_cross_references`, pick 3–5 key words and call `lookup_strongs` / `get_elberfelder_renderings` for them,
  then summarise.
- `word_study(strongs)` – `lookup_strongs` → `get_elberfelder_renderings` → `find_occurrences`.
Tests: prompt text contains the tool names and the argument.

### S47 – Dockerfile + `compose.yml`

- `src/UnshackledWord.Tooling.McpServer/Dockerfile` – copy of the WebApi Dockerfile with project names replaced.
- `compose.yml` service `unshackledword.tooling.mcpserver`, `depends_on` database + seed-db, port `7655:8080`,
  env `UNSHACKLEDWORD_ENVIRONMENT`, `UNSHACKLEDWORD_CONNECTIONSTRINGS__POSTGRESCONNECTION`.

### S48 – README

New section "MCP server": what it does, `dotnet run` / compose, connect with
`claude mcp add --transport http unshackled-word http://localhost:7655/mcp`, Claude Desktop config, MCP Inspector,
list of tools. Tick the roadmap item in the README "What does this repo provide?" list.

---

## Risks

| Risk | Impact | Mitigation |
|---|---|---|
| **No integration tests** – SQL is only checked by string assertions; typos in column names or wrong joins surface at runtime | Tools fail against the real DB | Run every new tool once against a seeded local DB via MCP Inspector before ticking the step; add the planned test DB later and back-fill tests |
| **Versification mistakes** (HebRefId vs LxxRefId) | Wrong original text / cross-references for Psalms, Joel, Malachi … | Always go through `IVersificationRepository`; manually check Ps 51,1-2 (heading), Joel 3,1 / 2,28, Mal 3,19 / 4,1 |
| **`BibleVerseCountingMapping` not complete** for every verse | Verses silently missing in original text / TSK | Compare row count with `Elb1871Verses`; fall back to `Elb1871Verses.LxxRefId` if a verse has no mapping row |
| **Strong's formats differ between tables** (`G0026` vs `G26` vs compound Hebrew `H9003/{H7225G}`) | Lookups return nothing | `StrongsNumber` normalises; verify formats with a few SQL samples before S31/S33/S41 |
| **AI-generated alignments are imperfect** | Interlinear shows wrong pairs; model draws wrong conclusions | Tool descriptions state that the alignment is AI-generated and may contain errors |
| **Token-heavy output** (whole chapters with morphology, long lexicon articles) | Slow, expensive, context overflow in the client | Hard limits per tool, truncation notes, `columns` / `full` options |
| **MCP C# SDK API changes** (2.x is young; attribute or option names may change) | Code from this roadmap doesn't compile | Pin the version in `Directory.Packages.props`; check the package README in S07 |
| **No auth on HTTP** | Anyone reaching the port can query the DB | Bind to localhost / docker network only; read-only DB user; separate auth roadmap before public hosting |
| **Unbounded user input in FTS** (`websearch_to_tsquery`) | Slow queries | Paging limits, statement timeout on the connection for the MCP server |
| **Changing shared Domain code** (S01, S02) | Side effects in SeedDb / WebApi / BibleTagger | Keep public signatures; run the full test suite; S02 only adds members |
| **German abbreviation collisions** (`Jos`, `Jo`, `Phil`, `Ph`) | Wrong book chosen | Explicit tests for ambiguous abbreviations in S02/S03 |
| **Copyright of the data** | Project principle is licence-free data | Only expose data already in the DB (open-licence sources); don't add third-party texts |

## Open questions / to verify during implementation

- Is `BibleVerseCountingMapping` complete for all verses of both testaments? (S09)
- Exact contents of `StepHebrewWords."RootDisambiguatedStrongsInstance"` and `Elb1871GreekMapping."StrongsNumber"`
  (S06, S33). Run `SELECT DISTINCT … LIMIT 50` against the seeded DB.
- Strong's format in `StepPersonLexicon."Strongs"` / `StepPlaceLexicon."Strongs"` vs `DisambiguatedStrongs` (S41).
- Does `StepBibleVerses."HebRefId"` exist and is it filled for the NT as well? (S39)
- Is the TSK `Scope` text English (KJV keywords)? Affects whether the formatter should label it as such (S19).
