# Strong's stats header – backend spec

Status: design, ready to implement. UI: `docs/drafts/strongs-stats-design.md`, `docs/drafts/strongs-stats.html`.
All SQL below ran read-only against the dev database (`postgres001`, PG 18.6) with literal parameters, and the results were checked (G1577: 117 occurrences; »Versammlung«: H6951 105 of 123, G1577 77 of 117, as on csv-bible).

## Decisions baked in

| Topic | Decision |
|---|---|
| German matching ("match the lemma") | German key = `LOWER(COALESCE(NULLIF(ew."Lemma", ''), ew."PlainWord"))`. **`Elb1871Words.Lemma` is NULL in all 722,753 rows today**, so for now the key is the lower-cased form (»Versammlung« and »Versammlungen« stay separate). Once `Elb1871Lemmatizer` fills `Lemma`, renderings, word stats and the rendering filter group by lemma with no code change. |
| Strong's matching | One list of **match keys** per request, used by the summary *and* the concordance, so counts always agree. Number with a letter (`G3004G`, `H0430G`, `H1254A_B` → `H1254A`) matches exactly that key. Number without a letter (`G3004`, `H0430`) matches every lexicon variant of the number (`G3004G`+`G3004H`, `H0430G/H/I`). Today `G3004` returns 0 hits because the data only has the suffixed forms. |
| Donut 1 | All Elberfelder renderings of the match keys, counted **per German word key** (as the existing word-detail renderings do, but every one, not the top 5). `count` = number of original-word occurrences rendered with that German word. A single original word can map to several German words, so the counts can add up to more than `mappedCount`. Added words (`IsAddedWord`) are excluded. |
| German-word donuts | Per testament (`ot` uses `Elb1871HebrewMapping`, `nt` uses `Elb1871GreekMapping`, `all` uses both). They list the Strong's numbers behind the word, with `count` and `totalCount`. |
| Concordance filter | `strongsOccurrences` gets optional `book` and `rendering` (a German key from donut 1). |
| Book numbering | Elberfelder book of the verse, `COALESCE(Elb1871Verses.HebRefId, word.LxxRefId) / 1000000`, the same as the concordance uses today. OT = 1–39, NT = 40–66. |
| Sections | 8, static in the frontend (`lib/bible/books.ts`). The backend returns only `bibleBookId`. |
| "Gebildet aus" | Not returned (`composedOf` is never set and is left out of the JSON because nulls are omitted). |

## Routes (contract)

All routes live under the `elberfelder` group (prefix `api`, so `api/elberfelder/...`), with `AllowAnonymous()` and `Cache-Control: public, max-age=3600`. JSON is camelCase and leaves out nulls.

### 1. `GET api/elberfelder/strongsSummary/strongs/{strongs}`

- `400`: the format is invalid (same validator rule as `strongsOccurrences`).
- `404`: no lexicon entry **and** no occurrence for any match key.
- `200` otherwise. That includes known numbers with 0 occurrences or no mapping.

```ts
interface GetStrongsSummaryResponse {
  strongs: string;               // normalized request, "G1577" / "G3004" / "H0430G"
  language: "greek" | "hebrew" | "aramaic";
  lemma?: string;                // StepStrongsLexicon.OriginalWord of the first variant
  transliteration?: string;
  gloss?: string;                // English; distinct variant glosses joined with "; "
  occurrenceCount: number;       // sum of byBook.count
  verseCount: number;            // sum of byBook.verseCount (a verse never spans books)
  mappedCount: number;           // occurrences with ≥1 non-added Elberfelder word
  unmappedCount: number;         // occurrenceCount - mappedCount (donut slice "nicht zugeordnet", not clickable)
  byBook: { bibleBookId: number; count: number; verseCount: number }[];  // only books > 0, ascending
  renderings: { word: string; key: string; count: number }[];           // all, most used first; key = filter value
  variants?: {                   // only when more than one match key (number given without letter)
    strongs: string; lemma: string; transliteration?: string; gloss?: string; count: number;
  }[];
  // composedOf: not returned yet
}
```

Example (G1577): `{"strongs":"G1577","language":"greek","lemma":"ἐκκλησία","transliteration":"ekklēsia","gloss":"assembly","occurrenceCount":117,"verseCount":…,"mappedCount":113,"unmappedCount":4,"byBook":[…,{"bibleBookId":44,"count":23,"verseCount":23},…],"renderings":[{"word":"Versammlung","key":"versammlung","count":77},{"word":"Versammlungen","key":"versammlungen","count":36},{"word":"die","key":"die","count":1}]}`

### 2. `GET api/elberfelder/wordStats?word=Versammlung&testament=ot`

- `word` is required, 1–50 characters, `^[\p{L}'-]+$` (a single German word). The server lower-cases it.
- `testament` is `all` | `ot` | `nt` (default `all`), case-insensitive. Anything else returns `400`.
- An unknown word returns `200` with zeros and empty arrays.

```ts
interface GetWordStatsResponse {
  word: string;                  // most frequent spelling found (MODE), else the request as given
  testament: "all" | "ot" | "nt";
  occurrenceCount: number;       // Elberfelder words with this key in the testament
  verseCount: number;
  unmappedCount: number;         // of those, words without a non-added mapping (italic/added words, untagged)
  byBook: { bibleBookId: number; count: number; verseCount: number }[];
  originals: {                   // most used first
    strongs: string;             // "H6951", "G1577"
    language: "greek" | "hebrew" | "aramaic";
    lemma?: string; transliteration?: string; gloss?: string;
    count: number;               // occurrences of this Strong's rendered with the word (in the testament)
    totalCount: number;          // all occurrences of the Strong's ("105 von 123 Mal")
  }[];
}
```

`originals.language` is `greek` for G numbers. For H numbers it is `aramaic` when the lexicon row has `LanguageId = 1`, otherwise `hebrew`. That is close enough for a badge; the summary uses the more exact grammar-based rule.

### 3. Changed: `GET api/elberfelder/strongsOccurrences/strongs/{strongs}?page&pageSize&book&rendering`

- New optional parameters: `book` (int 1–66) and `rendering` (string, 1–50 characters, compared lower-cased to the German key). Both together are combined with AND.
- The response shape is unchanged. `totalItems` and `totalPages` respect the filters. It now uses the same match keys as the summary, so `G3004` finally returns hits.

## Match-key resolution (service, C#)

```
StrongsNumber.TryNormalize(input, out normalized, out hebrew)            // existing; drops "_A"
parsed  = StrongsNumber.Parse(normalized).Value
entries = IStrongsLexiconRepository.GetEntriesAsync(parsed)               // existing; Extra==null → all variants
keys    = entries.Select(e => new StrongsNumber(parsed.Language, e.Number, e.Extra).ToString())
                 .Append(normalized).Distinct().Order().ToArray()          // request itself always included
```

Lexicon rows are unique per (language, number, extra), and Hebrew and Aramaic never share a number+extra (checked), so `ORDER BY "Extra"` from the existing repo is enough. Put this in a small static `StrongsMatchKeys.Resolve(normalized, parsed, entries)` in `Application/Features/StrongsStats` so it can be unit-tested and reused by `StrongsOccurrenceService`.

Rules for the header fields:
- `lemma`/`transliteration` come from `entries[0]`. `gloss` is the distinct `entries.Gloss` joined with `"; "`. With no entries, all three are omitted.
- `language`: Greek → `greek`. Hebrew → `aramaic` if `sum(AramaicCount) * 2 > occurrenceCount`; if `occurrenceCount == 0`, `aramaic` if `entries[0].LanguageId == 1`; otherwise `hebrew`. The lexicon alone is unreliable here (H0426 אֱלָהּ is LanguageId 0 but all 95 words are Aramaic).
- `variants`: when `keys.Length > 1`, one row per key, joined with its lexicon entry and its count from the key-counts query (keys with 0 hits are kept, with `count: 0`).

## SQL

Style follows the existing repositories (`"unshackled-word"` schema, `WHERE 1=1`, leading commas, raw string literals indented to the `"""`). No SQL-formatting skill file exists in `.claude/skills`, so match `WordDetailRepository.cs`. Parameters: `Keys` is a `string[]` (Npgsql sends `text[]`). `Book` is an `int?` and `Rendering` a `string?`, both always cast in SQL (`@Book::int`, `@Rendering::text`) the same way `StrongsLexiconRepository` does `@Extra::varchar`.

The verse map CTE replaces the per-row `LATERAL … LIMIT 1` of today's concordance with the same semantics (first Elberfelder verse by `Id` for the original verse), so it can be filtered by book before paging. It is used as-is in S1, S2, O1 and O2:

```sql
verse_map AS (
    SELECT DISTINCT ON (v."LxxRefId") v."LxxRefId"
         , v."HebRefId"
    FROM "unshackled-word"."Elb1871Verses" v
    ORDER BY v."LxxRefId", v."Id"
)
```

### S1 – by book (Greek / Hebrew)

```sql
WITH verse_map AS (
    SELECT DISTINCT ON (v."LxxRefId") v."LxxRefId"
         , v."HebRefId"
    FROM "unshackled-word"."Elb1871Verses" v
    ORDER BY v."LxxRefId", v."Id"
), hits AS (
    SELECT COALESCE(vm."HebRefId", sgw."LxxRefId") "ElbRefId"
    FROM "unshackled-word"."StepGreekWords" sgw
        LEFT JOIN verse_map vm ON vm."LxxRefId" = sgw."LxxRefId"
    WHERE 1=1
        AND sgw."DisambiguatedStrongs" = ANY(@Keys)
)
SELECT (hits."ElbRefId" / 1000000)::int "BibleBookId"
     , COUNT(*)::int "Count"
     , COUNT(DISTINCT hits."ElbRefId")::int "VerseCount"
     , 0 "AramaicCount"
FROM hits
GROUP BY 1
ORDER BY 1
```

```sql
WITH verse_map AS (
    SELECT DISTINCT ON (v."LxxRefId") v."LxxRefId"
         , v."HebRefId"
    FROM "unshackled-word"."Elb1871Verses" v
    ORDER BY v."LxxRefId", v."Id"
), hits AS (
    SELECT COALESCE(vm."HebRefId", shw."LxxRefId") "ElbRefId"
         , shw."Grammar"
    FROM "unshackled-word"."StepHebrewWords" shw
        LEFT JOIN verse_map vm ON vm."LxxRefId" = shw."LxxRefId"
    WHERE 1=1
        AND split_part(shw."RootDisambiguatedStrongsInstance", '_', 1) = ANY(@Keys)
)
SELECT (hits."ElbRefId" / 1000000)::int "BibleBookId"
     , COUNT(*)::int "Count"
     , COUNT(DISTINCT hits."ElbRefId")::int "VerseCount"
     , (COUNT(*) FILTER (WHERE hits."Grammar" LIKE 'A%'))::int "AramaicCount"
FROM hits
GROUP BY 1
ORDER BY 1
```

Row: `StrongsBookCountRow { int BibleBookId; int Count; int VerseCount; int AramaicCount }`.

### S2 – count per key (only run when `keys.Length > 1`)

```sql
SELECT sgw."DisambiguatedStrongs" "Strongs"
     , COUNT(*)::int "Count"
FROM "unshackled-word"."StepGreekWords" sgw
WHERE 1=1
    AND sgw."DisambiguatedStrongs" = ANY(@Keys)
GROUP BY sgw."DisambiguatedStrongs"
```

```sql
SELECT split_part(shw."RootDisambiguatedStrongsInstance", '_', 1) "Strongs"
     , COUNT(*)::int "Count"
FROM "unshackled-word"."StepHebrewWords" shw
WHERE 1=1
    AND split_part(shw."RootDisambiguatedStrongsInstance", '_', 1) = ANY(@Keys)
GROUP BY 1
```

Row: `StrongsKeyCountRow { string Strongs; int Count }`.

### S3 – all renderings

```sql
SELECT MODE() WITHIN GROUP (ORDER BY COALESCE(NULLIF(ew."Lemma", ''), ew."PlainWord")) "Word"
     , LOWER(COALESCE(NULLIF(ew."Lemma", ''), ew."PlainWord")) "Key"
     , COUNT(DISTINCT sgw."Id")::int "Count"
FROM "unshackled-word"."StepGreekWords" sgw
    JOIN "unshackled-word"."Elb1871GreekMapping" egm ON egm."StepWordId" = sgw."Id"
    JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = egm."ElbWordId"
WHERE 1=1
    AND sgw."DisambiguatedStrongs" = ANY(@Keys)
    AND NOT egm."IsAddedWord"
    AND ew."PlainWord" <> ''
GROUP BY LOWER(COALESCE(NULLIF(ew."Lemma", ''), ew."PlainWord"))
ORDER BY COUNT(DISTINCT sgw."Id") DESC, LOWER(COALESCE(NULLIF(ew."Lemma", ''), ew."PlainWord"))
```

```sql
SELECT MODE() WITHIN GROUP (ORDER BY COALESCE(NULLIF(ew."Lemma", ''), ew."PlainWord")) "Word"
     , LOWER(COALESCE(NULLIF(ew."Lemma", ''), ew."PlainWord")) "Key"
     , COUNT(DISTINCT shw."Id")::int "Count"
FROM "unshackled-word"."StepHebrewWords" shw
    JOIN "unshackled-word"."Elb1871HebrewMapping" ehm ON ehm."StepWordId" = shw."Id"
    JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = ehm."ElbWordId"
WHERE 1=1
    AND split_part(shw."RootDisambiguatedStrongsInstance", '_', 1) = ANY(@Keys)
    AND NOT ehm."IsAddedWord"
    AND ew."PlainWord" <> ''
GROUP BY LOWER(COALESCE(NULLIF(ew."Lemma", ''), ew."PlainWord"))
ORDER BY COUNT(DISTINCT shw."Id") DESC, LOWER(COALESCE(NULLIF(ew."Lemma", ''), ew."PlainWord"))
```

Row: `StrongsRenderingRow { string Word; string Key; int Count }`. Return all rows; the frontend folds the tail into "andere".

### S4 – mapped count (scalar)

```sql
SELECT COUNT(DISTINCT egm."StepWordId")::int
FROM "unshackled-word"."StepGreekWords" sgw
    JOIN "unshackled-word"."Elb1871GreekMapping" egm ON egm."StepWordId" = sgw."Id"
WHERE 1=1
    AND sgw."DisambiguatedStrongs" = ANY(@Keys)
    AND NOT egm."IsAddedWord"
```

```sql
SELECT COUNT(DISTINCT ehm."StepWordId")::int
FROM "unshackled-word"."StepHebrewWords" shw
    JOIN "unshackled-word"."Elb1871HebrewMapping" ehm ON ehm."StepWordId" = shw."Id"
WHERE 1=1
    AND split_part(shw."RootDisambiguatedStrongsInstance", '_', 1) = ANY(@Keys)
    AND NOT ehm."IsAddedWord"
```

### W1 – word stats by book

`@FirstBook`/`@LastBook`: `all` = 1/66, `ot` = 1/39, `nt` = 40/66. `@Word` is passed as given; SQL lower-cases it.

```sql
SELECT ew."BibleBookId"
     , COUNT(*)::int "Count"
     , COUNT(DISTINCT ew."HebRefId")::int "VerseCount"
     , (COUNT(*) FILTER (WHERE NOT EXISTS (SELECT 1
                                            FROM "unshackled-word"."Elb1871GreekMapping" egm
                                            WHERE 1=1
                                                AND egm."ElbWordId" = ew."Id"
                                                AND NOT egm."IsAddedWord")
                           AND NOT EXISTS (SELECT 1
                                           FROM "unshackled-word"."Elb1871HebrewMapping" ehm
                                           WHERE 1=1
                                               AND ehm."ElbWordId" = ew."Id"
                                               AND NOT ehm."IsAddedWord")))::int "UnmappedCount"
     , MODE() WITHIN GROUP (ORDER BY COALESCE(NULLIF(ew."Lemma", ''), ew."PlainWord")) "Word"
FROM "unshackled-word"."Elb1871Words" ew
WHERE 1=1
    AND LOWER(COALESCE(NULLIF(ew."Lemma", ''), ew."PlainWord")) = LOWER(@Word)
    AND ew."BibleBookId" BETWEEN @FirstBook AND @LastBook
GROUP BY ew."BibleBookId"
ORDER BY ew."BibleBookId"
```

Row: `WordBookCountRow { int BibleBookId; int Count; int VerseCount; int UnmappedCount; string Word }`. Response `word` = the `Word` of the book row with the highest `Count`, or the request when there are no rows.

### W2 – Strong's numbers behind the word

```sql
WITH words AS (
    SELECT ew."Id"
    FROM "unshackled-word"."Elb1871Words" ew
    WHERE 1=1
        AND LOWER(COALESCE(NULLIF(ew."Lemma", ''), ew."PlainWord")) = LOWER(@Word)
        AND ew."BibleBookId" BETWEEN @FirstBook AND @LastBook
), originals AS (
    SELECT sgw."DisambiguatedStrongs" "Strongs"
         , 2 "LanguageId"
         , COUNT(DISTINCT sgw."Id")::int "Count"
    FROM words
        JOIN "unshackled-word"."Elb1871GreekMapping" egm ON egm."ElbWordId" = words."Id"
        JOIN "unshackled-word"."StepGreekWords" sgw ON sgw."Id" = egm."StepWordId"
    WHERE 1=1
        AND NOT egm."IsAddedWord"
    GROUP BY sgw."DisambiguatedStrongs"
    UNION ALL
    SELECT split_part(shw."RootDisambiguatedStrongsInstance", '_', 1) "Strongs"
         , 0 "LanguageId"
         , COUNT(DISTINCT shw."Id")::int "Count"
    FROM words
        JOIN "unshackled-word"."Elb1871HebrewMapping" ehm ON ehm."ElbWordId" = words."Id"
        JOIN "unshackled-word"."StepHebrewWords" shw ON shw."Id" = ehm."StepWordId"
    WHERE 1=1
        AND NOT ehm."IsAddedWord"
        AND shw."RootDisambiguatedStrongsInstance" IS NOT NULL
    GROUP BY 1
)
SELECT o."Strongs"
     , o."Count"
     , CASE
           WHEN o."LanguageId" = 2 THEN (SELECT COUNT(*)::int
                                         FROM "unshackled-word"."StepGreekWords" t
                                         WHERE 1=1
                                             AND t."DisambiguatedStrongs" = o."Strongs")
           ELSE (SELECT COUNT(*)::int
                 FROM "unshackled-word"."StepHebrewWords" t
                 WHERE 1=1
                     AND split_part(t."RootDisambiguatedStrongsInstance", '_', 1) = o."Strongs")
       END "TotalCount"
     , lex."LanguageId" "LexiconLanguageId"
     , lex."OriginalWord" "Lemma"
     , lex."Transliteration"
     , lex."Gloss"
FROM originals o
    LEFT JOIN LATERAL (
        SELECT ssl."LanguageId"
             , ssl."OriginalWord"
             , ssl."Transliteration"
             , ssl."Gloss"
        FROM "unshackled-word"."StepStrongsLexicon" ssl
        WHERE 1=1
            AND ssl."LanguageId" = ANY(CASE WHEN o."LanguageId" = 2 THEN ARRAY[2] ELSE ARRAY[0, 1] END)
            AND ssl."Number" = substring(o."Strongs" from '^[GH](\d+)')::int
            AND COALESCE(ssl."Extra", '') = substring(o."Strongs" from '^[GH]\d+([A-Za-z]*)$')
        ORDER BY ssl."LanguageId"
        LIMIT 1
    ) lex ON TRUE
ORDER BY o."Count" DESC, o."Strongs"
```

Row: `WordOriginalRow { string Strongs; int Count; int TotalCount; int? LexiconLanguageId; string? Lemma; string? Transliteration; string? Gloss }`. Map language: `G…` → greek, `LexiconLanguageId == 1` → aramaic, else hebrew. The UNION branches return nothing for the testament with no matching words (`ot` gives no Greek rows), so one statement serves all three testaments.

### O1/O2 – concordance with filters (replace `WordDetailRepository.GetOccurrenceCountAsync` use in `StrongsOccurrenceService` and `GetOccurrencesAsync`)

Greek count:

```sql
WITH verse_map AS (
    SELECT DISTINCT ON (v."LxxRefId") v."LxxRefId"
         , v."HebRefId"
    FROM "unshackled-word"."Elb1871Verses" v
    ORDER BY v."LxxRefId", v."Id"
)
SELECT COUNT(*)::int
FROM "unshackled-word"."StepGreekWords" sgw
    LEFT JOIN verse_map vm ON vm."LxxRefId" = sgw."LxxRefId"
WHERE 1=1
    AND sgw."DisambiguatedStrongs" = ANY(@Keys)
    AND (@Book::int IS NULL OR COALESCE(vm."HebRefId", sgw."LxxRefId") / 1000000 = @Book::int)
    AND (@Rendering::text IS NULL OR EXISTS (SELECT 1
                                             FROM "unshackled-word"."Elb1871GreekMapping" egm
                                                 JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = egm."ElbWordId"
                                             WHERE 1=1
                                                 AND egm."StepWordId" = sgw."Id"
                                                 AND NOT egm."IsAddedWord"
                                                 AND LOWER(COALESCE(NULLIF(ew."Lemma", ''), ew."PlainWord")) = LOWER(@Rendering::text)))
```

Greek page:

```sql
WITH verse_map AS (
    SELECT DISTINCT ON (v."LxxRefId") v."LxxRefId"
         , v."HebRefId"
    FROM "unshackled-word"."Elb1871Verses" v
    ORDER BY v."LxxRefId", v."Id"
), page AS (
    SELECT sgw."Id"
         , sgw."LxxRefId"
         , sgw."PositionInVerse"
         , sgw."Greek" "Original"
         , COALESCE(vm."HebRefId", sgw."LxxRefId") "ElbRefId"
    FROM "unshackled-word"."StepGreekWords" sgw
        LEFT JOIN verse_map vm ON vm."LxxRefId" = sgw."LxxRefId"
    WHERE 1=1
        AND sgw."DisambiguatedStrongs" = ANY(@Keys)
        AND (@Book::int IS NULL OR COALESCE(vm."HebRefId", sgw."LxxRefId") / 1000000 = @Book::int)
        AND (@Rendering::text IS NULL OR EXISTS (SELECT 1
                                                 FROM "unshackled-word"."Elb1871GreekMapping" egm
                                                     JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = egm."ElbWordId"
                                                 WHERE 1=1
                                                     AND egm."StepWordId" = sgw."Id"
                                                     AND NOT egm."IsAddedWord"
                                                     AND LOWER(COALESCE(NULLIF(ew."Lemma", ''), ew."PlainWord")) = LOWER(@Rendering::text)))
    ORDER BY sgw."LxxRefId", sgw."PositionInVerse", sgw."Id"
    LIMIT @Take OFFSET @Skip
), elb AS (
    SELECT egm."StepWordId"
         , array_agg(ew."PositionInVerse" ORDER BY ew."PositionInVerse") "MatchedPositions"
    FROM "unshackled-word"."Elb1871GreekMapping" egm
        JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = egm."ElbWordId"
    WHERE 1=1
        AND egm."StepWordId" IN (SELECT "Id" FROM page)
        AND NOT egm."IsAddedWord"
    GROUP BY egm."StepWordId"
)
SELECT (page."ElbRefId" / 1000000)::int "BibleBookId"
     , (page."ElbRefId" / 1000 % 1000)::int "Chapter"
     , (page."ElbRefId" % 1000)::int "Verse"
     , page."Original"
     , elb."MatchedPositions"
     , vw."VersePositions"
     , vw."VerseWords"
FROM page
    LEFT JOIN elb ON elb."StepWordId" = page."Id"
    LEFT JOIN LATERAL (
        SELECT array_agg(w."PositionInVerse" ORDER BY w."PositionInVerse") "VersePositions"
             , array_agg(w."WordInContext" ORDER BY w."PositionInVerse") "VerseWords"
        FROM "unshackled-word"."Elb1871Words" w
        WHERE 1=1
            AND w."HebRefId" = page."ElbRefId"
    ) vw ON TRUE
ORDER BY page."LxxRefId", page."PositionInVerse", page."Id"
```

Hebrew count and page: the same two statements with exactly these substitutions (all tested):
`"StepGreekWords" sgw` → `"StepHebrewWords" shw`; every `sgw.` → `shw.`; `"Elb1871GreekMapping" egm` → `"Elb1871HebrewMapping" ehm`; every `egm.` → `ehm.`; `sgw."Greek" "Original"` → `shw."Hebrew" "Original"`; `sgw."DisambiguatedStrongs" = ANY(@Keys)` → `split_part(shw."RootDisambiguatedStrongsInstance", '_', 1) = ANY(@Keys)`. Write them out in full as separate `const string`s; do not build SQL from string fragments.

Check (G1577): unfiltered 117; `book=44` 23 (= S1 Acts row); `book=44&rendering=Versammlung` 20.

## Files

Add:

| Layer | File | Content |
|---|---|---|
| Domain.WebApi | `src/UnshackledWord.Domain.WebApi/Elberfelder/GetStrongsSummary/GetStrongsSummaryRequest.cs` | `Strongs` (route) |
| | `.../GetStrongsSummary/GetStrongsSummaryResponse.cs` | `GetStrongsSummaryResponse`, `StrongsBookCountResponse`, `StrongsRenderingResponse`, `StrongsVariantResponse` (shape above; optional fields nullable) |
| | `.../GetWordStats/GetWordStatsRequest.cs` | `Word`, `Testament = "all"`, constants `MaxWordLength = 50` |
| | `.../GetWordStats/GetWordStatsResponse.cs` | `GetWordStatsResponse`, `WordOriginalResponse` (reuse `StrongsBookCountResponse` for byBook) |
| Domain | `src/UnshackledWord.Domain/Models/Study/StrongsStatsRows.cs` | `StrongsBookCountRow`, `StrongsKeyCountRow`, `StrongsRenderingRow`, `WordBookCountRow`, `WordOriginalRow`, `StrongsOccurrenceFilter(int? Book, string? Rendering)` |
| Application | `src/UnshackledWord.Application/Repositories/IStrongsStatsRepository.cs` | `GetBookCountsAsync(keys, hebrew)`, `GetKeyCountsAsync(keys, hebrew)`, `GetRenderingsAsync(keys, hebrew)`, `GetMappedCountAsync(keys, hebrew)`, `GetWordBookCountsAsync(word, firstBook, lastBook)`, `GetWordOriginalsAsync(word, firstBook, lastBook)`, `CountOccurrencesAsync(keys, hebrew, filter)`, `GetOccurrencesAsync(keys, hebrew, filter, skip, take)`; all take `CancellationToken token = default`, keys are `IReadOnlyList<string>` and are passed to Dapper as `string[]` |
| | `src/UnshackledWord.Application/Features/StrongsStats/StrongsMatchKeys.cs` | static `Resolve` (above) |
| | `.../StrongsStats/IStrongsSummaryService.cs` + `StrongsSummaryService.cs` | `Task<StrongsSummaryResult>`: NotFound / Invalid / Ok(response) (or `null` = invalid and a `NotFound` flag; follow how the endpoint maps it) |
| | `.../StrongsStats/IWordStatsService.cs` + `WordStatsService.cs` | testament → book range, row → response mapping |
| Infrastructure | `src/UnshackledWord.Infrastructure/Repositories/StrongsStatsRepository.cs` | SQL above, pattern of `WordDetailRepository` (`IDbReader`) |
| | register in `Infrastructure/Repositories/ServiceCollectionExtensions.cs` | `IStrongsStatsRepository`, `IStrongsSummaryService`, `IWordStatsService` (scoped) |
| WebApi | `src/UnshackledWord.Tooling.WebApi/Endpoints/Elberfelder/GetStrongsSummary/Endpoint.cs` | route `strongsSummary/strongs/{Strongs}`, `Group<RouteGroupConfig>()`, validator copied from GetStrongsOccurrences, 400/404/200 + cache header |
| | `.../Endpoints/Elberfelder/GetWordStats/Endpoint.cs` | route `wordStats`, validator (word regex/length, testament in all/ot/nt) |
| Migration | `src/UnshackledWord.Tooling.Postgres.Migration/scripts/0007_StrongsStatsIndexes.sql` | below (scripts are embedded resources by glob, nothing else to register) |
| Bruno | `api/unshackled-word/…` | one request per new route (optional) |

Change:
- `GetStrongsOccurrencesRequest`: add `int? Book`, `string? Rendering`. The validator adds `Book` in 1–66 when set and `Rendering` length 1–50 when set.
- `StrongsOccurrenceService`: resolve keys (inject `IStrongsLexiconRepository`), use `IStrongsStatsRepository.CountOccurrencesAsync/GetOccurrencesAsync` and pass the filter. `IStrongsOccurrenceService.GetAsync` gains `StrongsOccurrenceFilter filter`.
- `IWordDetailRepository.GetOccurrencesAsync` and its SQL become unused, so remove them and their test. **Keep** `GetOccurrenceCountAsync` (the word-detail popover uses it; its single-key semantics fit there).

Tests (xUnit + Shouldly + NSubstitute, `FakeDbReader` from TestTooling, like the existing ones):
- `tests/UnshackledWord.Application.Tests/Features/StrongsMatchKeysTests.cs`: `G3004` with entries G/H gives `[G3004, G3004G, G3004H]`; `G1577` with an entry without Extra gives `[G1577]`; no entries gives `[normalized]`; `H1254A_B` gives `[H1254A]`.
- `.../Features/StrongsSummaryServiceTests.cs`: totals are summed from byBook; `unmappedCount = occurrenceCount - mapped`; aramaic majority rule, including the 0-occurrence lexicon fallback; 404 when there are no entries and no rows; `variants` only with >1 key (keys with 0 hits included); gloss join; S2 not called for a single key.
- `.../Features/WordStatsServiceTests.cs`: testament → (1,39)/(40,66)/(1,66); invalid testament is rejected by the validator; unknown word returns zeros; `word` from the max-count row; language mapping of originals.
- `.../Features/StrongsOccurrenceServiceTests.cs`: update for keys + filter, so `book`/`rendering` reach the repository.
- Validator tests (pattern of `SaveVerseMappingValidatorTests.cs`) for both new requests and the new occurrence params.
- `tests/UnshackledWord.Infrastructure.Tests/StrongsStatsRepositoryTests.cs`: for each method the right tables appear in the SQL (Greek vs Hebrew via the `hebrew` flag), parameter names `Keys` (a `string[]`), `Book`, `Rendering`, `Skip`, `Take`, `Word`, `FirstBook`, `LastBook`.

## Migration `0007_StrongsStatsIndexes.sql`

```sql
-- Strong's stats and German word stats: German word key (lemma, falling back to the plain form) per book.
-- The expression must stay identical to the one used in StrongsStatsRepository, or the index is not used.
CREATE INDEX IF NOT EXISTS "IX_Elb1871Words_GermanKey_Book"
    ON "unshackled-word"."Elb1871Words" ((LOWER(COALESCE(NULLIF("Lemma", ''), "PlainWord"))), "BibleBookId");
```

Everything else is already covered: 0006 (`DisambiguatedStrongs`, Hebrew root expression, mapping `StepWordId`), 0002 (mapping `ElbWordId`) and 0001 (`Elb1871Verses.LxxRefId`). `lower`, `split_part` and `nullif` are immutable (checked in `pg_proc`). **Note:** the dev DB `postgres001` does not have the 0006 indexes (no `IX_StepGreekWords_DisambiguatedStrongs`, no mapping `StepWordId` index). That is why W2 took about 1.6 s and the rendering filter about 0.6 s there; unfiltered S1–S4 took 30–80 ms even on that DB (G3588 ὁ, 20k hits, S1 78 ms). Run the migration before you judge performance.

## Edge cases

| Case | Behaviour |
|---|---|
| Strong's known, 0 words (STEP lexicon-only number) | 200, zeros, empty arrays, header from the lexicon. |
| Words but no Elberfelder mapping | `renderings: []`, `mappedCount: 0`, `unmappedCount = occurrenceCount`; the concordance shows full verses (existing behaviour). |
| Mapped only as added words | Counted as unmapped (added words are excluded everywhere, like the word detail). |
| Greek number, OT | No data (LXX is not mapped); byBook holds only NT books. The OT word donut still works via Hebrew. |
| Aramaic | Same H numbers and tables; `language` from the grammar majority (`Grammar LIKE 'A%'`), because the lexicon `LanguageId` is wrong for e.g. H0426. 13 roots occur in both languages and get the majority. |
| Lemma with several Strong's numbers (λέγω = G3004G/G3004H, בָּרָא = H1254A/B) | `/strongs/G3004` aggregates all variants and lists them in `variants`; `/strongs/G3004G` is exact. |
| One Strong's with several lemmas (G1473 ἐγώ/ἐμός/…) | Counted together (same key); header lemma from the lexicon. |
| Odd data keys (`G20447`, `H5838x`, `"H0310A, H3651C"`, 14 Hebrew rows with NULL root) | Not matched; a handful of words, ignored. |
| Rendering filter with an unknown key, or a book with 0 hits | 200, `totalItems: 0`. |
| Unmapped slice | Not filterable (no magic value); the UI renders it non-interactive. |
| Original word mapped to 2+ German words | Counts once per German word key; donut shares are of renderings, not occurrences. |

## Open for the user

1. **German lemma data:** `Elb1871Words.Lemma` is empty, so "match the lemma" degrades to "match the form" until the Elb1871Lemmatizer output is seeded. Should that be a separate task before or after this feature?
2. **Unsuffixed Strong's = all variants** (`G3004` → G3004G+H; `H0430` also merges the place name "Gibeath-elohim" H0430I). Accept, or keep exact-only and make the frontend always link the suffixed number?
3. **Rendering counting:** per German word (chosen: clean single-word slices that feed the word donuts and the filter) vs. per occurrence phrase ("meines Gottes", "unser Gott" as separate slices, which add up exactly to the occurrences).
