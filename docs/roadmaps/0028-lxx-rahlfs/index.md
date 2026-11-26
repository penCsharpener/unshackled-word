# Roadmap 0028: Septuagint (Swete text in Rahlfs numbering)

Folder roadmap (see the `roadmap-folder` skill): open only the file you need.

| File | Content |
|---|---|
| [01-goal.md](01-goal.md) | What is missing, end state, out of scope |
| [02-decisions.md](02-decisions.md) | Maintainer decisions from the interview, plan decisions, code findings |
| [03-versification.md](03-versification.md) | Swete → Rahlfs → STEP/Elberfelder numbering through TVTMS |
| [04-model.md](04-model.md) | Tables `LxxVerses`, `LxxWords`, `LxxWordTags`, `LxxHebrewLinks`, audit, seed files |
| [05-tagging.md](05-tagging.md) | Deterministic pre-pass, AI mode `lxx-tag-v1`, review and confirm |
| [06-ui.md](06-ui.md) | Side panel mode "LXX", word details, Strong's page, review page `/edit/lxx` |
| [07-tasks.md](07-tasks.md) | Task table (backend and frontend woven) |
| [08-risks.md](08-risks.md) | Risks worth naming before starting |

## Status (2026-11-25)

Interview held 2026-11-25 (see [02-decisions.md](02-decisions.md)). 0028.1 done (version 0.13.0); 0028.2 done (0.13.1); 0028.3 done; the rest is open.

**0028.1 (download, parsers, `lxx-check`) done, with these findings and deviations:**

- **Source:** eliranwong/LXX-Swete-1930 pinned at `1d3efc3c63bd384a4f3f07ed37eef54b0d45ac33` (`AppSettings:DatabaseSeeding:Swete`), downloaded to `temp/SeedData/Swete/` with a `commit.txt` marker; a changed commit downloads again.
- **Format:** both files are tab separated, with no header. The word file has 609,522 lines of `id<TAB>word` with consecutive ids and punctuation attached. The verse file has 30,533 lines of `id<TAB>Book.Chapter:Verse`, where the id is the verse's first word. There are no subverse refs. Headings (`ΕΝ ΑΡΧΗ`) and editorial marks (~251 `[N]`, ~210 `⸂ ⸆ ⸃`) appear as words, so 0028.3 must filter them.
- **Protocanonical books:** the 39 books plus `Dat` hold 23,617 verses and 487,224 words, Daniel counted twice (per-book totals are printed by `lxx-check`). Swete's book codes differ from TVTMS for Sol/Eze/Joe/Nah; `LxxBookCodes` maps them.
- **Special books:**
  - Joshua and Judges are one stream each (codex B).
  - Daniel is `Dan` (OG) and `Dat` (Theodotion), 424 verses each.
  - The additions in **Esther (A–F)** and **3 Kgdms 2:35/2:46** are not separate verses. They sit *inline* in the neighbouring verse with `[N]`/`⸂…⸃` marks, so the subverse model in [03-versification.md](03-versification.md) needs splitting logic or a decision in 0028.2.
  - Psalms (1–151, title = verse 1) match TVTMS Greek exactly.
- **Comparison with TVTMS:** 80 chapters differ (74 distinct book-chapters), and 324 Swete refs are missing in the TVTMS Greek tradition. Most are in Proverbs (77), 3 Kgdms (64), Daniel (43 per form) and Joel (21). Not all are Swete exceptions:
  - In Gen 2 and Deu 28, Swete follows TVTMS `Greek2`.
  - In Mal 4, the TVTMS cell has a typo (`Mal.4:1-35`).
  - TVTMS comments out Dan 3:52–90.
  - In 1Ki 14, TVTMS moves Greek 14:1–20 into the 12:24 long verse.
- **Deviations:**
  - New `IOfflineSeedVerb`, so `lxx-check` runs without a database.
  - `VerseTables` is now public, for the English verse counts.
  - `TvtmsGreekParser` lives in `Application/Features/Lxx`.
  - The verb downloads STEP itself when TVTMS is missing.
  - The stub `RalfsLxxParserStrategy` and its setting are removed.
- **Tests:** new parser tests pass (8 + 3). Two AgentMapping prompt tests (`AgentPromptProviderTests`, `AgentPromptExampleTests` greek-v2) fail; this change touches neither the prompt files nor that code.

**0028.2 (exception list, `LxxVersification`, `lxx-check --show`) done, with these findings and deviations:**

- **Exception list:** `assets/seed/lxx/swete-to-rahlfs.csv` has 133 rows (syntax in [03-versification.md](03-versification.md#assetsseedlxxswete-to-rahlfscsv)).
  - 54 rows cover Swete's English/Vulgate chapter divisions where Rahlfs follows the Hebrew: 1Sa 20/21, 2Sa 18/19, 2Ki 11/12, 1Ch 5/6, 2Ch 1/2 and 13/14, Neh 3/4, Ecc 6/7, Song 5-7, Hos, Joel, Mic, Nah, Hag, Zech, Isa 8/9 and 63/64, Jer 2:36 and 8/9, Ezek 20/21, Dan 5/6 (both forms).
  - 45 rows cover Greek order and pluses: Gen 5:32/6:1, Jos 9:2a-f, 2Sa 23:30, 3 Kgdms 4-6 and 9-10, Prov 15-20, 24 and 29 (Hebrew 30-31 inside), Est 1:1, 5:1-2 and 9.
  - 24 rows cover places where Swete follows TVTMS Greek2/Latin: Gen 2:25, Exo 36 and 39, Job 16, 39-41, Neh 4:4-5, Jer 30:1-5.
  - 21 rows are word-range splits (10 verses). 9 are gap rows (16 Rahlfs verses without Swete text), and 1 is an absent row (Prov 24:23).
- **Inline additions:** the marker rule gives 232 subverses (Esther A–F, 3 Kgdms 2:35a-o/2:46a-l/12:24a-z/16:28a-h, Prov, Job, Dan OG 4). Of the 367 additions, the rest are:
  - Dan 3:24-90 in both forms, as S3Y;
  - Ps 151;
  - Prov 15:29.1.
- **TVTMS fixes in code:** Mal 4 (the typo and the 4:4-6 order), Dan 3:52-90 (commented out), Dan 3:98-100 (`Absent`), Exo 38:19 (missing). Deu 28:69 is an artefact of `VerseTables`: the English count for Deu 28 is 69, but should be 68. It is filtered in the check, and `BibleBook` is unchanged.
- **Gen 2 and Deu 28:** only Gen 2 is a Greek2 case, fixed by a word split. Deu 28 was the count artefact.
- **1Ki 14:1-20:** no TVTMS error. Swete leaves them empty, like 239 other verse numbers (240 empty Swete verses in all).
- **Check:** `lxx-check` maps 23,860 units:
  - by MapKind: one 23,203, split 48, merged 2, addition 367, absent 240 (all empty);
  - chapters: ok 869, explained (exception) 63, explained (tvtms-fix) 5, gap 6, **unexplained 0**;
  - references: explained (exception) 81, gap 69, unexplained 0.
  
  The full mapping is in `temp/lxx/swete-to-rahlfs-mapping.csv`.
- **Deviations:**
  - The `RahlfsRefId` encoding is book·10⁸ + chapter·10⁵ + verse·10² + subverse, because Est 4:17 has 30 subverses.
  - "Rahlfs" is defined as the TVTMS Greek column.
  - `SweteVerse`/`SweteWord` moved to `Application/Features/Lxx`, so that `LxxVersification` stays pure.
  - The check finds count differences only. Shifts inside a chapter with an equal count, for example Prov 16, map as TVTMS does.
- **Notes for 0028.3:**
  - Import in `Order` (Swete order), not in `RahlfsRefId` order.
  - Skip `IsEmpty` units.
  - Drop marker tokens (`LxxVersification.IsMarkerToken`), the `⸂ ⸆ ⸃` marks and the heading words from the word stream.
- **Tests:** 12 new `LxxVersificationTests` pass (20 with the parser tests). The two AgentMapping prompt tests from 0028.1 still fail and are untouched.

**0028.3 (migration 0050, `LxxTextImporter`) done, with these findings and deviations:**

- **Schema:** `LxxVerses`/`LxxWords` as in [04-model.md](04-model.md), which now matches the migration. Changes against the plan:
  - `SortOrder` (int4, unique) holds Swete's reading order, because `RahlfsRefId` order differs (Esther addition A sorts after 1:1).
  - `RahlfsRefId` is nullable, for `absent` units only (CHECK). `SweteRef` is `varchar(60)`; the longest joined ref has 26 characters (`Job.41:1#6- + Job.41:2#1-6`).
  - There are CHECKs on `MapKind`, `TextForm`, the book range 1..39, and on additions (no `LxxRefId`/`HebRefId`).
  - FK to `BibleBooks`, and `LxxWords` → `LxxVerses` with `ON DELETE CASCADE`.
  - Indexes: `HebRefId` and `AfterHebRefId` (partial), and `(BibleBookId, Chapter)`. The unique `(LxxVerseId, Position)` serves reads by verse.
- **Import (dev DB):**
  - 23,620 verses: one 23,203, split 48, merged 2, addition 367, absent 0. The 240 empty Swete verses are skipped.
  - 486,313 words. That is the 0028.1 total of 487,224 tokens minus the 240 blanks and the 671 tokens dropped by the filter.
  - The SeedDb pipeline imports in 11.2 s (download check, parse, map and insert in one transaction); the whole process run takes 13.4 s.
  - The second run logs "LXX text up to date, no import" in 1.6 s and changes no row; the identity max values are unchanged.
- **Per book (verses/words):** Gen 1532/32,635, Exo 1169/24,647, Lev 859/19,042, Num 1288/24,973, Deu 955/22,501, Jos 659/14,815, Jdg 618/15,570, Rut 85/2066, 1Sa 773/20,044, 2Sa 695/17,906, 1Ki 819/20,436, 2Ki 722/18,618, 1Ch 928/15,967, 2Ch 832/21,059, Ezr 278/5332, Neh 377/7336, Est 267/5830, Job 1082/13,509, Psa 2527/34,615, Pro 931/11,091, Ecc 222/4518, Sng 117/1994, Isa 1288/26,953, Jer 1296/28,775, Lam 150/2375, Ezk 1265/29,364, Dan og 415/10,952, Dan th 422/10,272, Hos 197/3915, Jol 73/1573, Amo 146/3200, Oba 21/456, Jon 48/1080, Mic 105/2342, Nam 47/931, Hab 56/1090, Zep 52/1223, Hag 38/948, Zec 211/4952, Mal 55/1408.
  - Additions: Est 105, Dan og 74, Dan th 65, Pro 39, 1Ki 36, Job 13, 2Ch 12, Jos 10, Psa 7, 2Ki 4, 1Sa 2.
- **Spot checks:**
  - Jer 32:1 has `HebRefId` 24025015.
  - Ps 9:22 has `HebRefId` 19010001.
  - Est 1:1.1 (`SortOrder` 17629) comes before Est 1:1 (`Est.1:1[18]`, `SortOrder` 17646).
- **Word filter (`LxxWordFilter`):** it dropped 671 tokens:
  - 251 marker tokens;
  - 330 editorial-mark tokens (`⸂⸆⸃`, `⸆`, `⸂⸆⸆⸃`). The marks attached to words (`⸂αἰρόμενα`, `χρονιεῖ,⸃`) are stripped, and the word is kept;
  - 88 acrostic letter names in Lam 1-4 (`Ἄλεφ.`, `Βήθ.` … `Θαύ.`, including the mid-verse ones in Lam 1:1 and 3:45);
  - 2 standalone `·` (Gen 36:40, Jer 5:6), appended to the previous word.
- **No heading words:** Swete's word file has no book titles or section headings. The capitals at the start of a book (`ΕΝ ΑΡΧΗ` Gen 1:1, `ΚΑΙ` Lev/Num/Jos …, `ΛΟΓΟΣ` Hos, `ΜΑΚΑΡΙΟΣ` Ps 1:1, `ΕΤΟΥΣ` Est addition A) are the first words of the text, printed in unaccented capitals. They are kept as words.
  - For tagging (0028.5), their form lacks accents.
- **`GreekNoDiacritics`** uses `RemoveGreekDiacritics`, as for STEP. Swete's square brackets for supplied text (74 words: Gen 1:20-2:3, 1Ki 22:29, 2Ch 34:8, e.g. `[τοῖ]ς`) are also removed there; `Greek` keeps them. The elision mark `᾽` stays (`επ᾽`), as it does in the STEP words.
- **Deviations:**
  - `LxxSourceLoader` (Swete + TVTMS + exception list + `LxxVersification.Map`) is shared by `lxx-check` and the importer.
  - The importer locks `LxxVerses` and checks the gate again inside its transaction.
  - An exception list with errors stops the import (logged); the seed goes on.
  - The verified run used only the LXX runner through the git-ignored `SeedingService.local.cs`. That change has been reverted.
- **Tests:**
  - 11 new `LxxTextBuilderTests` (filter and row mapping) pass, 31 Lxx tests in all.
  - The 2 new `LxxTextDatabaseTests` pass against the dev DB: the migration runs twice, the insert and the gate work, and the transaction rolls back.
  - The two known AgentMapping prompt tests still fail.

Numbering (2026-11-25): this roadmap goes next and takes migrations `0050`/`0051` and version 0.13.0. 0026 (morpheme alignment) is deferred indefinitely and will take whatever numbers are free when it is picked up.
