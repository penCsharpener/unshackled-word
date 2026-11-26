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
| Psalm title | Ps 3:1 | Ps 3:1 | Ps 3:0 (title, as in STEP) | Ps 3:1 |
| 3 Kgdms long verse | 3 Kgdms 2:35 `[1]` | 1Ki 2:35.1 | — | — (addition after 2:35) |
| Esther addition A | Est 1:1 `[1]` | Est 1:1.1 | — | — (addition after 1:1) |
| Joshua 8:30-35 in the Greek | Jos 9:3 | Jos 9:2.1 | Jos 8:30 | Jos 8:30 |

`lxx-check --show <ref>` prints this chain for any Swete or Rahlfs reference.

## Rules

- **"Rahlfs" means the TVTMS Greek column.** It is the key space we store. Where TVTMS's Greek column has its own quirks (Jer 30 puts Ammon after Damascus, Prov keeps Hebrew numbers in the Greek order), we follow TVTMS, because TVTMS is the bridge.
- **Rahlfs book names map to the existing book ids.** 1–4 Kingdoms = 1 Sam–2 Kgs, 1–2 Paralipomenon = 1–2 Chr, 2 Esdras = Ezra + Nehemiah (Rahlfs 2 Esd 11 = Neh 1). Daniel OG and Th are one book with `TextForm` (`Dan` = `og`, `Dat` = `th`).
- **Encoding:** `RahlfsRefId = book·10⁸ + chapter·10⁵ + verse·10² + subverse` (int8). Subverses go up to 30 (Est 4:17.30), so one digit is not enough. A subverse sorts right after its main verse. `LxxRefId`/`HebRefId` use the existing `RefId` (`book·10⁶ + chapter·10³ + verse`, `BibleReference.RefId`).
- **Psalm titles:** Greek and Hebrew count the title as verse 1. English has no title verse, so `LxxRefId` of a title is verse 0 (`Psa.3:0` = 19003000), the same as STEP's Hebrew words (`Psa.3.0(3.1)`). Two-verse Hebrew titles (Ps 51:1-2) give two Greek verses with the same `LxxRefId` and `MapKind` `split`.
- **Reading order is Swete's order** (`Order` in `LxxVersification`), not `RahlfsRefId`: Esther addition A (1:1.1-17) comes before 1:1, and Prov 30:1-14 before 24:23. 0028.3/0028.4 must keep that order.

### Units: verses, marker segments, word ranges

- Every Swete verse is one unit.
- **Marker rule.** Swete prints its a, b, c … pluses as numbers in brackets (`[1]`, `⸂[1]`). In the 39 books they appear in Jos, 1Sa, 1Ki, 2Ki, 2Ch, Prov, Job, Est and Dan, and give 232 marker segments. A verse with markers is cut at them: the words before the first marker keep the verse, and the k-th marker segment becomes subverse k. The order counts, not the printed number: Swete skips `[10]` (j) in 1Ki 2:35 and 12:24, so `[11]` is subverse 10. Est 1:1 `[18]` is the canonical 1:1, and Est 5:1-2 follow TVTMS's D numbering. Both are exception rows. 2Sa 23:30 `[31]` only marks where Hebrew 23:31 begins, so a whole-verse row keeps that verse in one piece.
- The marker tokens stay in the unit's words, and `LxxVersification.IsMarkerToken` finds them. 0028.3 must keep them out of the word stream, together with the editorial marks `⸂ ⸆ ⸃` and the heading words.
- **Empty verses.** Swete keeps the number of a verse its Greek lacks as one blank word (240 in the 39 books: 1Ki 14:1-20, Jer 46:4-13, 1Sa 17:12-31 …). They become `MapKind` `absent` with no references and are not imported.

### `assets/seed/lxx/swete-to-rahlfs.csv`

CC0, `;`-separated, header `SweteRef;RahlfsRef;TextForm;Note`. Lines starting with `#` are comments. Every row has a `Note`.

| Cell | Syntax |
|---|---|
| `SweteRef` | Swete book code (`Joe`, `Sol`, `Eze`, `Nah`, `Dat` …): `Book.C:V`, `Book.C:V-W`, `Book.C:V[N]` / `[N-M]` (marker segments by printed number), `Book.C:V#A-B` / `#A-` (word positions 1…n of the verse). Empty = **gap row**: the Rahlfs verses have no Swete text. |
| `RahlfsRef` | TVTMS syntax and book codes (`Jol`, `Sng`, `Ezk`, `Nam`, `Dan`): `Book.C:V`, `Book.C:V-W`, `Book.C:V.S`, `Book.C:V.S-T`, inside one chapter. Empty = the Swete text has no Rahlfs counterpart (`absent`). |
| `TextForm` | Empty, or `og`/`th` for Daniel. It must agree with `Dan`/`Dat`. |

- With the same count on both sides, units are assigned pairwise.
- With one `RahlfsRef`, the units **join** in Swete order. Rows that name the same `RahlfsRef` also join (Nah 1:15 + 2:1 = Nam 2:1).
- Word-range rows must cover the whole verse.
- Giving a default (unlisted) reference twice is an error, so a shifted range must be listed in full.

Examples:

```
Jos.9:3-8;Jos.9:2.1-6;;Heb 8:30-35 follow 9:2 in the Greek (Rahlfs 9:2a-f); Swete numbers them 9:3-8
Gen.3:1#1-15;Gen.2:25;;Swete prints 2:25 as the start of 3:1 (TVTMS Greek2 numbering); split back to the Greek column
Exo.36:23-24;Exo.36:23;;Swete follows Greek2 (NETS) in Exo 36: Greek 36:23 is Swete 36:23-24; joined
;Est.9:30;;the Greek lacks Heb 9:30
```

### TVTMS: mapping and fixes

- **Lookup.** A TVTMS row gives `LxxRefId` (English) and `HebRefId` (Hebrew) per Greek verse. Same count on both sides gives `one`. More Greek verses than English verses gives `split`, and fewer gives `merged`; either way the verse takes the first English/Hebrew verse. The main verse of a LongVerse (`1Ki.2:35.0-14`) is `one`.
- **Identity.** A Greek verse that no row names, and whose English number no row covers, is the same in every tradition.
- **Additions** get `HebRefId` = `LxxRefId` = NULL and `AfterHebRefId` set. A verse is an addition when:
  - its row has no Hebrew (Esther A–F, Dan 3:24-90 = S3Y, Ps 151);
  - it is a subverse whose only rows point back to its own main verse (1Ki 2:35.1-14); or
  - it is a subverse TVTMS does not have (Swete's other marker pluses, Prov 15:29.1).
- **Placement.** For a subverse, `AfterHebRefId` is the Hebrew verse of its main verse. Otherwise it is the last Hebrew verse before it in Swete's order.
- **Fixes in code, not in the CSV** (`TvtmsGreekFixes`):
  - **Mal 4:** drop the stray row `Mal.4:1-35`. Eng 4:4/4:5/4:6 become Greek 4:6/4:4/4:5, because the Greek ends with Elijah, the turning of hearts, then Moses.
  - **Dan 3:52-90:** TVTMS comments these out. They are added as S3Y additions.
  - **Dan 3:98-100:** TVTMS marks them `Absent`. They become Eng 4:1-3 / Heb 3:31-33.
  - **Exo 38:19:** missing from the Greek column. It is mapped to Eng/Heb 36:13.
  - **Deu 28:69:** an artefact of the English count table (69 instead of 68), so it is dropped from the check.
  - **1Ki 14:1-20:** TVTMS puts these in the 12:24 long verse. No fix is needed: Swete prints 12:24a-z with markers and leaves 14:1-20 empty.
- **Elberfelder verses come from `HebRefId`,** resolved through `Elb1871Verses` the same way 0027 resolves hits, never through `BibleVerseCountingMapping`.

### The check

`lxx-check` compares each Rahlfs chapter (and Daniel text form) with the TVTMS Greek verses. It writes `temp/lxx/swete-vs-tvtms.csv` with a status per chapter:

| Status | Meaning |
|---|---|
| `ok` | The verses match. |
| `explained (exception)` | Exception rows touch the chapter. |
| `explained (tvtms-fix)` | A TVTMS fix touches the chapter. |
| `gap` | The only differences are empty Swete verses or gap rows. |
| `unexplained` | Anything else. Exit code 2. |

The reference rows cover:

- `rahlfs-not-in-tvtms`: empty verses, and marker or exception subverses;
- `tvtms-not-in-swete`: gap rows;
- `swete-absent`.

`temp/lxx/swete-to-rahlfs-mapping.csv` lists every mapped unit, for review.

The check counts verses only. A shift inside a chapter whose verse count matches is not detected. Prov 16 is an example: Swete 16:5 is partly Heb 16:2-4. Those mappings are as exact as TVTMS's Greek column.
