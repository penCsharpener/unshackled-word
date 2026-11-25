# 0025 · Model

## Shape

```
StepGreekWords (unchanged, 142k)          GreekEditions (~22 rows)
  Id ───────────────┐                       Id, Code, Name, Year, Kind, IsMain, Sort
  Editions (raw)    │                          ▲
                    ▼                          │
          StepGreekWordEditions (~1.1M rows) ──┘
            StepWordId, EditionId, Shift, ShiftRaw
```

## DDL sketch

```sql
CREATE TABLE "unshackled-word"."GreekEditions" (
    "Id"     SMALLSERIAL PRIMARY KEY,
    "Code"   VARCHAR(20)  NOT NULL UNIQUE,  -- token as in STEP: 'NA28', 'TR', 'P66*'
    "Name"   VARCHAR(200) NOT NULL,         -- 'Nestle-Aland 28. Auflage'
    "Year"   SMALLINT,                      -- 2012
    "Kind"   VARCHAR(20)  NOT NULL,         -- 'edition' | 'translation' | 'manuscript' | 'version' | 'unknown'
    "IsMain" BOOLEAN      NOT NULL DEFAULT FALSE,
    "Sort"   SMALLINT     NOT NULL
);

CREATE TABLE "unshackled-word"."StepGreekWordEditions" (
    "StepWordId" INT      NOT NULL REFERENCES "unshackled-word"."StepGreekWords" ("Id") ON DELETE CASCADE,
    "EditionId"  SMALLINT NOT NULL REFERENCES "unshackled-word"."GreekEditions" ("Id"),
    "Shift"      SMALLINT,                  -- +n for »n, -n for «n, NULL = same order
    "ShiftRaw"   VARCHAR(20),               -- original suffix when it is not a plain number ('«14.24', '»1»1')
    PRIMARY KEY ("StepWordId", "EditionId")
);
CREATE INDEX ON "unshackled-word"."StepGreekWordEditions" ("EditionId");
```

`Name` is German (the UI is German first; English names come from the frontend i18n keyed by `Code`).

## Catalog (initial rows)

| Code | Kind | IsMain | Year | Name (de) |
|---|---|---|---|---|
| NA28 | edition | ✓ | 2012 | Nestle-Aland, 28. Auflage |
| NA27 | edition | ✓ | 1993 | Nestle-Aland, 27. Auflage |
| Tyn | edition | ✓ | 2017 | Tyndale House GNT |
| SBL | edition | ✓ | 2010 | SBLGNT (Holmes) |
| WH | edition | ✓ | 1881 | Westcott-Hort |
| Treg | edition | ✓ | 1879 | Tregelles |
| TR | edition | ✓ | 1894 | Textus Receptus (Scrivener) |
| Byz | edition | ✓ | 2005 | Byzantinischer Text (Robinson-Pierpont) |
| KJV, NIV | translation | | | Grundtext der King James / NIV |
| 01, 02, 03, 04, 05, 06, 032, P66, P66* | manuscript | | | Sinaiticus, Alexandrinus, Vaticanus, Ephraemi, Bezae, Claromontanus, Washingtonianus, P66, P66 (Erstschreiber) |
| Coptic, Latin, Syriac | version | | | Koptisch, Lateinisch, Syrisch |

## Token grammar

`Editions` = tokens joined by `+`. Token = `Code` + optional suffix. Suffix `»n` → `Shift = +n`, `«n` → `Shift = -n`, `»0` → `Shift = 0`. Anything else after the code (`«14.24`, `»1»1`) → `Shift = NULL`, `ShiftRaw` = suffix. `Code` = token up to the first `»`/`«`.

## Refresh function

`RefreshStepGreekWordEditions()` (plpgsql, idempotent):

1. Insert unknown codes into `GreekEditions` (`Kind = 'unknown'`, `Sort = 999`).
2. `TRUNCATE StepGreekWordEditions`, then one `INSERT … SELECT` over `unnest(string_to_array("Editions", '+'))` with the regex split above.
3. Return the number of rows written (SeedDb logs it).

Expected on the dev DB: about 1,103,000 rows (sum of token counts), 0 unknown codes.

## Derived per word (read side)

- `InAllMain` = the word has a row for every `IsMain` edition **and** none of them has a shift. Computed in SQL (`COUNT(*) FILTER (WHERE ge."IsMain") = (SELECT COUNT(*) FROM GreekEditions WHERE IsMain)`), not stored.
- `MissingMain` = main editions without a row for the word.
