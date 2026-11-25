# 0028 · Versification

## Three numberings, one chain

```
Swete ref ──(swete-to-rahlfs.csv, exceptions only)──▶ Rahlfs ref ──(TVTMS Greek column)──▶ English ref = LxxRefId (STEP)
                                                                     └──────────────────▶ Hebrew ref  = HebRefId (Elb1871)
```

| Example | Swete | Rahlfs (stored) | English / `LxxRefId` | Hebrew / `HebRefId` |
|---|---|---|---|---|
| Oracles against the nations | Jer 32:1 | Jer 32:1 | Jer 25:15 | Jer 25:15 |
| Psalm 10 | Ps 9:22 | Ps 9:22 | Ps 10:1 | Ps 10:1 |
| Psalm title | Ps 3:1 | Ps 3:1 | Ps 3 title (none) | Ps 3:1 |
| 3 Kgdms long verse | 3 Kgdms 2:35a | 1Ki 2:35.1 | — | — (addition) |
| Esther addition A | Est 1:1a | Est 1:1.1 | — | — (addition) |

## Rules

- **Rahlfs book names map to the existing book ids.** 1–4 Kingdoms = 1 Sam–2 Kgs, 1–2 Paralipomenon = 1–2 Chr, 2 Esdras = Ezra + Nehemiah (Rahlfs 2 Esd 11 = Neh 1). Daniel OG and Th are one book with `TextForm`.
- **Encoding:** `RahlfsRefId = book·10⁷ + chapter·10⁴ + verse·10 + subverse`, so a ref with a subverse sorts right after its main verse. The existing `RefId` format (`BibleReference.FromRefId`) has no subverse, which is why this needs its own id.
- **TVTMS rows that are `OneToOne` or a renumbering** give `LxxRefId` and `HebRefId` per verse. For rows that are `Absent`, a merge or a split, the verse maps to the first verse of the range and gets `MapKind` (`one`, `merged`, `split`, `addition`, `absent`). Additions get `HebRefId` = NULL, and the reader shows them after the verse they follow (the `AfterHebRefId` column).
- **Elberfelder verses come from `HebRefId`,** resolved through `Elb1871Verses` the same way 0027 resolves hits, never through `BibleVerseCountingMapping`.
- **0028.1 measures the exceptions.** It compares the verse count of every Swete chapter with the TVTMS Greek column. Each chapter that differs becomes a row in `swete-to-rahlfs.csv` or a known gap. The expected size is a few hundred lines in Exodus 35–40, 3 Kingdoms, Proverbs 24–31, Jeremiah and Esther; that estimate is unmeasured.
