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
| [06a-review-page.md](06a-review-page.md) | Frontend design of the review page `/edit/lxx` (0028.7f): layout, states, requests, query keys, i18n |
| [07-tasks.md](07-tasks.md) | Task table (backend and frontend woven) |
| [08-risks.md](08-risks.md) | Risks worth naming before starting |

## Status (2026-11-25)

Interview held 2026-11-25 (see [02-decisions.md](02-decisions.md)). All rows 0028.1 to 0028.11 are done (versions 0.13.0 to 0.13.16, one patch bump per step; 0028.7f was committed in three parts). Open: the end-of-roadmap review, and the first real (paid) `lxx-tag` run, which needs the maintainer's approval.

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

**0028.4 (read endpoint `GET api/lxx/chapter/{bibleBookId}/{chapter}`) done, with these findings and deviations:**

- **Shape:** `{ bibleBookId, chapter, verses: [{ hebRefId, verse, units: [{ lxxVerseId, bibleBookId, chapter, verse, subverse, ref, textForm, mapKind, isAddition, differsFromHebrew, words: [{ id, position, greek }] }] }] }`. One entry per Elb1871 verse (from `Elb1871Verses`), empty `units` when the LXX has no text. `ref` is `Jer.32:1` or `1Ki.2:35.1` (TVTMS book code); the client formats it.
- **Reading order rule:** per Elb verse the units are those with `HebRefId` = verse plus additions with `AfterHebRefId` = verse, all in `SortOrder` (Swete's order). Additions therefore come before or after the main unit as Swete prints them; there is no "additions last" rule. Est 1:1 returns addition A (`Est.1:1.1` to `1:1.17`) first, then `Est.1:1`, which matches 03-versification.md "Reading order". For additions the `subverse` is the sequence number, so the client must use `isAddition`, not the label, to tell `Est.1:1.1` (addition) from a subverse.
- **`differsFromHebrew`:** true for additions, any subverse, and a Rahlfs chapter or verse unequal to the Elb verse.
- **Daniel:** both text forms are returned, the client filters. No tags field yet: the extension point is `LxxWordResponse` (0028.5). Rate limit: the general anonymous limiter applies, there is no per-endpoint policy in this code base.
- **Code:** `Domain.WebApi/Lxx/GetLxxChapterContracts.cs`, `Application/Features/Lxx` (`ILxxReadRepository`, `LxxChapterReadService`), `Infrastructure/Lxx/LxxReadRepository` (one round trip, three result sets: Elb verse ids, units, words), endpoint `Endpoints/Lxx/GetLxxChapter`, Bruno `api/unshackled-word/Lxx/GetLxxChapter.bru`. Validation: book 1..39 and an existing chapter, else 400.
- **Checked against the dev DB (local Kestrel 5142):**
  - Jer 25:1-13 carry Jer 25:1-13 (`differsFromHebrew` false), 25:14 is empty (not in the LXX), 25:15-38 carry Jer 32:1-24 (true).
  - Ps 10 carries Psa 9:22-39 (18 verses, all true).
  - Est 1 as above. Mt 1 gives 400.
- **Timing (warm, local):** Ps 119 about 8 ms and 179 kB JSON, Jer 25 about 4 ms and 58 kB (uncompressed).
- **Tests:** 4 `LxxChapterReadServiceTests` (grouping and empty verses, split order, addition before and after the main unit, Daniel forms) and 8 `LxxChapterValidatorTests` pass.

**0028.4f (side panel mode "LXX") done, with these findings and deviations:**

- **Mode:** `SidePanelMode` gains `"lxx"`, the cycle is `tsk → original → lxx → similar → off`. In NT chapters the LXX step and its rail button are left out (`sidePanelModes(false)`). A stored `lxx` choice shows the Grundtext there (`effectiveSidePanelMode`), and the stored choice stays, so the next OT chapter shows the LXX again. Rail: heading "Septuaginta (Swete)", the muted line "Griechischer Text nach Swete, Verszählung nach Rahlfs", the icon is the text "LXX". `useLxxChapter` (`lib/bible/sidePanel.ts`) runs only while the mode is shown; the panels wait for the data, so no verse flashes "nicht vorhanden" while loading.
- **No new component:** the LXX list is a branch of `pages/chapter/VerseSide.tsx`, as the Grundtext is. The helpers are in `lib/bible/lxx.ts` (`unitsOfForm`, `lxxBlocks`, `lxxReference`, `lxxOriginalWords`, `useLxxForm`). `OriginalVerse` takes `language: "lxx"`: Greek font, `lang="grc"`, no edition filter; words without `strongs` (all until 0028.5) are plain text without link and hover card. `VerseSidePanel` takes `mode="lxx"`. Both are updated in `Components.md`.
- **Labels:** "Jer 32,1 LXX" (German) and "Jer 32:1 LXX" (English), from the book abbreviation of the bible structure, only when `differsFromHebrew`. A subverse that is no addition shows as ".k" like the source label (`1Kön 2,35.1 LXX`).
- **Deviation, additions:** consecutive additions form one muted frame (`border-border bg-surface-muted`) titled "Zusatz der LXX" (English "LXX addition"), with no reference and no subverse numbers. Est addition A (17 units) is one frame, not 17. The words of the frame run on as one text.
- **Deviation, position:** additions stand where Swete prints them (the API order), not always after their verse. Est 1:1 shows the frame first, then the verse's own Greek.
- **Daniel:** the Old Greek / Theodotion toggle sits in the rail (two secondary `Button`s with `aria-pressed`), only in Daniel and only in `lxx` mode. Theodotion is the default; the choice is in localStorage `uw.reader.lxxForm`. Units without a text form always show.
- **Checked in the browser** (local API on 5142 against the dev DB, Vite on 5174, 2100 px wide):
  - Jer 25 with `G` twice shows "Septuaginta (Swete)". 25:15 has "Jer 32,1 LXX", 25:14 "In der LXX nicht vorhanden".
  - Est 1:1 shows the addition frame before the verse's own Greek.
  - In Dan 3 the toggle swaps OG and Theodotion; after a reload OG is still on.
  - Mt 1 has no LXX button; the stored `lxx` shows the Grundtext, and `G` goes on to "Ähnliche Verse".
  - Dark and light mode were both checked.
- **Tests:** 6 new `lxx.test.ts`, 8 new `VerseSide.lxx.test.tsx` (labels de/en, plain words, additions frame, empty state, Daniel filter, rail heading, NT without LXX button, toggle persistence). `sidePanel.test.ts` got the new cycle (+2 tests).
- **Checks:** typecheck, lint (only the 8 known warnings) and `check:components` pass. Vitest in `node:24-slim` (the bun container failed to start its workers): 1171 passed, 8 failed, all of them the known ones in `CrossReferenceList`, `ChapterPage.tskProposal` and `NoteReviewPage`.

**0028.5 (migration 0051, `GreekMorphologyCode`, LXX word detail, tags in the chapter) done (backend; 0028.5f open), with these findings and deviations:**

- **Schema (`0051_LxxTags.sql`):** `LxxWordTags`, `LxxHebrewLinks`, `LxxTagAudit` as in [04-model.md](04-model.md), which now matches the migration. Changes against the sketch:
  - FKs to `Users` for every `UserId`, which is **nullable** (NULL = seed import; the seed files carry no user, and there is no system user in `Users`).
  - CHECKs: `Status` proposed|confirmed (tags and links), `Source` nt-form|ai|human, `Action` confirm|edit|reject|import, `Strongs` `^G[0-9]{4}[A-Z]?$` (the `StepGreekWords.DisambiguatedStrongs` format, e.g. `G1487M`), lemma and morphology not blank.
  - FKs to `LxxWords`/`LxxVerses` without `ON DELETE CASCADE`: deleting tagged text fails instead of dropping reviewed work.
  - `LxxHebrewLinks.StepHebrewWordId` has no FK, like the Elb1871 mappings (a STEP re-import renumbers it); the seed anchors the Hebrew side by `SourceKey` as planned. Index on `StepHebrewWordId` for reverse reads and the re-resolve.
  - Indexes: `LxxWordTags ("Strongs", "Status")` (0028.9), partial `("LxxWordId") WHERE "Status" = 'proposed'` (review page), `LxxTagAudit ("LxxVerseId", "At" DESC)`.
  - `AgentVerseProposals_Language_CK` allows `'lxx'` (dropped and re-added). **Also** the unique index `AgentVerseProposals_Open_UQ` was rebuilt with `"Language"` and `Flags.textForm`/`Flags.subverse`: with the old key (book, chapter, verse) an LXX proposal for Gen 1:1 would have collided with a Hebrew mapping proposal for Gen 1:1, and Daniel og with th. `AgentStoreSql.SupersedeOpen` still matches book/chapter/verse only; **0028.7 must add the language** there.
  - `MigrationsHealthCheck.ExpectedMigration` = 51. Ran on the dev DB via DbUp, then the script again by hand (idempotent).
- **Morphology: reused and extended, not duplicated.** `Domain/Models/Grammar/GreekMorphologyDecoder` (lenient, feeds `MorphologyResponse` keys for the NT word detail; the frontend translates the keys) already existed. It now also decodes reflexive (`F-3ASM`) and possessive pronouns (`S-1PASF`), strips a crasis part (`PREP + G1537`, `A-NUI + G3638`) and reads `ADV-C`; before, those gave a part of speech only (this improves the NT word box too). The new `Application/Features/Lxx/GreekMorphologyCode` is the strict validator on top: every segment must be understood (`V-XYZ`, `N-DS`, `N-DSF-Q`, `V-AAP-3S` are rejected with a reason), it takes its keys from the decoder and adds pronoun kind, deponent, second aorist, possessor number, STEP suffixes (`-C -S -N -I -T -K -L -LG -P -PG -LI -ATT -ARAM -HEB`, plus Robinson's `-M -AP -ABB`), the crasis Strong's and a German/English description ("Nomen Dativ Singular feminin" / "noun dative singular feminine").
  - **1161 distinct `StepGreekWords.Morphology` codes** in the dev DB (fixture `tests/UnshackledWord.Application.Tests/Features/Lxx/Fixtures/step-greek-morphology-codes.txt`): all parse, and all have complete keys for what they spell out.
  - **Label difference for 0028.5f:** the server description says "Nomen" (as the roadmap's acceptance text), the frontend's `bible.grammar.partOfSpeech.noun` says "Substantiv". 0028.5f either shows `parseGerman` or formats `morphology` like the NT words; pick one.
- **Word detail: sibling endpoint `GET api/lxx/word/{lxxWordId}`, not `lxx:{id}` in `GetWordDetail`.** `elberfelder/wordDetail/wordId/{WordId:int}` is an Elb1871 (German) word with its original readings, mapping provenance, co-mapped words and TSK; an LXX word is itself the original. A string id would break the int contract, and `revision/wordDetail` already sets the sibling pattern. The response reuses `MorphologyResponse` and `LexiconSummaryResponse`, so `WordInfoCard`/`WordDetailPopover` can render it. Shape: `{ id, lxxVerseId, position, greek, ref, bibleBookId, chapter, verse, subverse, textForm, isAddition, hebRefId, tag?: { lemma, strongs, gloss, morphology: {code, keys…}, parseGerman, parseEnglish, status, source (Admin/Maintainer only), lexicon } }`. Untagged → no `tag`. Unknown id → 404, id ≤ 0 → 400. The lexicon lookup (`FindEntryAsync`/`Build`) moved from `WordDetailService` into the shared `Application/Features/WordDetail/LexiconSummaries`.
- **Chapter tags:** `api/lxx/chapter` words carry an optional `tag: { lemma, strongs, gloss, morphology, status }`. Tags and the TBESG gloss (`StepStrongsLexicon`, `LATERAL` with exact disambiguation first) are joined into the existing words query: still one round trip with three result sets. Ps 119 (no tags) stays ~10 ms warm / 179 kB.
- **Visibility rule** (`LxxTagVisibility`): the repository returns tags of every status, the read services drop `proposed` unless `User.HasWriteRole()` (Admin/Maintainer, as the TSK and verse-mapping endpoints do). Both LXX endpoints now send `Cache-Control: private, no-cache` (role-dependent, a confirmation must show on the next request).
- **Checked against the dev DB (local Kestrel 5142):** hand-inserted (committed, then deleted) a confirmed tag on Gen 1:1 word 2 (ἀρχή, G0746, `N-DSF`, Source human, UserId 1 = admin) and a proposed one on word 3 (ποιέω, G4160, `V-AAI-3S`, ai).
  - Swete prints the opening words in capitals: word 2 is `ΑΡΧΗ`, not ἀρχῇ (see 0028.3). 0028.5f's acceptance text should say ΑΡΧΗ.
  - Anonymous `lxx/word/486320`: lemma ἀρχή, G0746, gloss "beginning", `parseGerman` "Nomen Dativ Singular feminin", TBESG definition. `lxx/word/486321` (proposed): no `tag`. `lxx/chapter/1/1`: only ΑΡΧΗ has a `tag`.
  - The maintainer path (proposed tags with status and source) is covered by unit tests only; a logged-in session was not used.
  - Afterwards `LxxWordTags`, `LxxHebrewLinks` and `LxxTagAudit` are empty again.
- **Tests:** 40 `GreekMorphologyCodeTests` cases (all 1161 STEP codes, complete keys, 18 descriptions, structure, 19 rejected codes), 14 `LxxTagVisibilityTests` (rule, chapter anonymous/maintainer, word untagged/confirmed/proposed/unparsable, service skips the lexicon for hidden tags), 5 new `GreekMorphologyDecoderTests` cases; 7 `LxxTagsDatabaseTests` against the dev DB (0051 runs twice, 5 refused rows, read SQL with tags and gloss) pass, skipped without `UNSHACKLEDWORD_TEST_POSTGRES`. Full suites: Domain 771, Infrastructure 178 (+153 skipped), WebApi 277 (+42 skipped) pass; Application 1349 pass, 2 fail (the known AgentMapping prompt tests).
- **Deviations:** no version bump and no commit (by instruction). Bruno `api/unshackled-word/Lxx/GetLxxWord.bru`. `LxxReadRepository`'s SQL moved into the public `LxxReadSql` so the DB tests can run it inside a rolled-back transaction.

**0028.5f (LXX words with hover card and detail in the panel) done, with these findings and deviations:**

- **No new component.** `OriginalVerse` (`language: "lxx"`) and `WordInfoCard` got props/fields: `info.notes`, `info.badge`, `onClose`; `VerseMappingOriginalWord.lxx` carries the Rahlfs label and tag status. `lxxOriginalWords(units, books, language)` maps the chapter `tag` to the same fields NT words have. Types `LxxWordTag`, `GetLxxWordResponse`, `bibleApi.lxxWord`, hook `useLxxWord` (stale time 60 s). `Components.md` updated.
- **Parse wording:** formatted from the `morphology` keys with `formatMorphology`, as NT words ("Substantiv · Dativ · Singular · feminin"); the server's `parseGerman` ("Nomen ...") is not shown. The Strong's link and label use `normalizeStrongs` like NT words, so `G0746` links to `/bible/strongs/G746`.
- **Deviation, detail surface:** `WordDetailPopover` is built for Elberfelder words (`GetWordDetailResponse`: readings, mapping, TSK), so the LXX detail is the margin card pinned on click: adds the Rahlfs reference and the TBESG definition from `GET api/lxx/word/{id}`, closes with "Schließen", Escape or a click on another word. A plain click on a tagged link opens it instead of navigating; Ctrl/Shift/Alt/middle click still follows the link; the card has the Strong's link.
- **Untagged words** are `Button variant="inline"` ("{word}: noch nicht bestimmt, Details öffnen") and open the card with "Noch nicht bestimmt" (en "Not yet tagged"). **Proposed** tags (maintainers): dotted underline (the edition mark class) and a neutral "Vorschlag" badge plus a hint line. The maintainer path is covered by tests only.
- **Checked in the browser** (API 5142, Vite 5175, 2100 px, temporary confirmed tag on word 486320): ΑΡΧΗ is a link to `/bible/strongs/G746`, hover shows ἀρχή, G0746, "Substantiv · Dativ · Singular · feminin", "beginning"; click pins the card with "1Mo 1,1", the TBESG definition and "Schließen"; other words are buttons and show "Noch nicht bestimmt". Tag deleted afterwards (`LxxWordTags` and `LxxTagAudit` empty), servers stopped, localStorage reset.
- **Tests:** 5 new `OriginalVerse.lxx.test.tsx` (link and card, pin and close, untagged de/en, proposed badge). Vitest: 1176 passed, 8 failed, all the known ones.

Numbering (2026-11-25): this roadmap goes next and takes migrations `0050`/`0051` and version 0.13.0. 0026 (morpheme alignment) is deferred indefinitely and will take whatever numbers are free when it is picked up.

**0028.6 (pass 1: NT forms, `lxx-prefill`) done (backend; 0028.6f open), with these findings and deviations:**

- **Code:** `Application/Features/Lxx` has `LxxFormNormalizer`, `LxxNtFormTagger` (pure `Compute` plus the run) and `ILxxNtFormRepository`; `Infrastructure/Lxx/LxxNtFormRepository` has the SQL (public `LxxNtFormSql`). Verb `lxx-prefill` (`Verbs/LxxPrefillVerb`), `POST api/maintainer/lxx/prefill` (202 with job, 409 when a seed or LXX job runs, job operation `lxxPrefill`) and `GET api/maintainer/lxx/status` (verses, words, tags per status and source, running flag, last prefill job with its log), contracts in `Domain.WebApi/Maintainer/LxxContracts.cs`, Bruno `Maintainer/LxxPrefill.bru` and `LxxStatus.bru`. Normalization and rules are in [05-tagging.md](05-tagging.md#pass-1-deterministic-nt-forms).
- **Approach:** all in memory (142k NT rows grouped by SQL, 486k LXX words, existing tags), then one transaction: delete `nt-form`/`proposed` rows that no longer qualify, `INSERT ... ON CONFLICT DO UPDATE ... WHERE` (only `nt-form`/`proposed` rows whose triple changed). Confirmed, `ai` and `human` rows are never written. A run takes about 11 s the first time and 4 s when nothing changes.
- **Measured on the dev DB (first run, 486,313 words):** 265,807 proposals (54.7 %), 139,749 words with an ambiguous form (28.7 %), 80,717 with a form not in the NT (16.6 %), 40 skipped unaccented capitals, 0 invalid Strong's/morphology, 0 empty. Per book 45 % (1 Chr) to 60 % (Jon), most 53 to 57 %; the second run changed 0 rows (0 inserted, 0 updated, 0 deleted).
- **Acceptance:** Gen 1:1 `ΕΝ` is a capital opening and is skipped (the accented form is unknowable); a lower-case `ἐν` (PREP, G1722, one analysis in the NT) gets its proposal. `οὐρανὸν`, `καὶ` and `τὴν` in Gen 1:1 get proposals; `ὁ` (T-NSM and T-VSM) and `θεὸς` (N-NSM, N-VSM) do not. **`λόγου` is not ambiguous in the NT** (27 times, always `N-GSM`) and does get a proposal; `ἦν` is not ambiguous either (always `V-IAI-3S`). The examples in 05-tagging.md were wrong; real ones are `ὁ`, `αὐτῶν` (gender) and `θεός`.
- **Finding, noise in the NT data:** "exactly one analysis" is strict, so one odd NT occurrence blocks a form, e.g. `τὸν` has `T-ASM` 1,628 times and `T-DSF` once, hence it is ambiguous. About a thousand forms differ in exactly two analyses. A majority rule (dominant analysis at 99 % or more, say) would raise the coverage; not done here because the task says exactly one. Decide before 0028.7 if the AI should see these forms as prefilled.
- **Finding:** pass 1 gives the NT analysis of the form, which is right for function words and wrong where LXX usage differs (case, gender and number of identical forms are only unambiguous inside the NT). All proposals are visible to maintainers only, and the review step checks them.
- **System user:** `UserId` stays NULL for `nt-form` rows (documented in 05-tagging.md); the table already allows it.
- **Local checks:** `api/maintainer/lxx/prefill` and `lxx/status` answer 401 anonymously on the local WebApi (5142). A maintainer session was not used; the 202 and 409 paths are covered by `LxxMaintainerTests`.
- **Dev DB:** the 265,807 `nt-form` proposals were left in place. Undo: `DELETE FROM "unshackled-word"."LxxWordTags" WHERE "Source" = 'nt-form' AND "Status" = 'proposed'`.
- **Tests:** 37 `LxxNtFormTaggerTests` (normalization, capitals, Strong's format, one-analysis rule, ambiguous skipped, confirmed/ai/human kept, per-book coverage), 2 `LxxNtFormDatabaseTests` (upsert idempotent and second run 0 rows, confirmed/ai untouched, changed rows updated, stale deleted; rolled back, skipped without `UNSHACKLEDWORD_TEST_POSTGRES`), 4 `LxxMaintainerTests` (202 with coverage in the log, 409, status, login required).
- **Deviations:** no version bump and no commit (by instruction). The SeedDb verb needs `UNSHACKLEDWORD_ConnectionStrings__PostgresConnection` locally because the checked-in connection string points to port 15435.

**0028.6f (card "LXX" on Wartung) done, with these findings and deviations:**

- **Code:** page section `pages/LxxSection.tsx` (not a shared component, no `Components.md` entry) and `lib/maintainer/lxx.ts` (`useLxxStatus` polling once a second while `running`, `useStartLxxPrefill`, `sumTags`, `invalidateReaderLxx`), modelled on `VerseSimilaritySection`. Placed in `MaintainerPage` right after "Ähnliche Verse" (write roles). `SeedOperation` got `lxxPrefill`; i18n `maintainer.lxx.*` (de/en) plus the operation label.
- **Content:** verses, words, tags and coverage (all tag rows / words, one decimal), counts by status and by source, button "Vorbelegen"/"Prefill" (label "Läuft …" while running), badge and time of the last job, job log, status line, 409 message "another seed or LXX job runs". After a running job ends, the queries `["bible","chapterLxx"]` (reader panel) and `["bible","lxxWord"]` (word card) are invalidated.
- **Checks:** typecheck, lint (8 pre-existing warnings), `check:components` (75 ok). Vitest 1180 passed, 8 failed, all the known ones. 4 new `LxxSection.test.tsx` (counts and percentage, start with running log, 409, invalidation).
- **Not verified in a browser:** needs a maintainer login, which was not available; covered by tests only.

**0028.7 (AI mode `lxx-tag`, prompt `lxx-tag-v1`) done (backend; 0028.7f open), with these findings and deviations:**

- **Code:** `AgentMode.LxxTag` / `AgentModes.LxxTag = "lxx-tag"`; `Application/Features/AgentMapping/Lxx` (`LxxAgentPackageBuilder`, `LxxCandidateIndex` + `LxxLexicon`, `LxxAgentAnswerParser`, `LxxProposalSubmitter`, `ILxxAgentDataRepository`), the validator branch `AgentProposalValidator.Lxx.cs` (`ValidateLxx`, `LxxFlagCodes`), `Infrastructure/Lxx/LxxAgentDataRepository` (public `LxxAgentSql`), schema `AgentAnswerSchema.LxxJson`, prompt `assets/agent-prompts/lxx-tag-v1.md`, migration `0052_LxxAgentMode.sql` (`ExpectedMigration` = 52). No new endpoint: `api/maintainer/agent-runs` (estimate/start/resume/cancel) and `Tooling.AgentWorker` take `mode: lxx-tag` because `AgentPackageBuilder` and `AgentProposalService` hand `lxx-tag` requests and packages to the LXX classes (optional constructor parameters, registered in `AddRepositories`). Design details are in [05-tagging.md](05-tagging.md#pass-2-ai-mode-lxx-tag-v1).
- **Package (Ruth 1:1, shortened):** `{"lang":"lxx","verses":[{"v":"v1","ref":"Rt 1:1","lxx":[[1,"ΚΑΙ",null,[["G2532","καί","and"],…]],[2,"ἐγένετο",["γίνομαι","G1096","V-2ADI-3S"]],…,[25,"Μωάβ,"],…],"hebRef":"Rt 1:1","heb":[[1,"וַיְהִ֗י","H1961_A","and/ it was","Hc/Vqw3ms",["H9001","{H1961}"]],…]}]}`. Answer: `{"verses":[{"v":"v1","words":[[1,"καί","G2532","CONJ",[1],"high"],[2,"γίνομαι","G1096","V-2ADI-3S",[1],"high"],…]}]}`. Hebrew words are numbered 1.. in reading order, not by STEP position: 75 STEP verses have duplicate positions (e.g. Num 26:1).
- **Size (dry run over Ruth on the dev DB, `LxxAgentDatabaseTests.DryRun_Ruth_OnePackagePerChapter`):** 4 packages (22/23/18/22 verses, 515/609/403/539 words, 60 % prefilled), 18.3k/20.9k/13.5k/18.8k estimated tokens, about 840 package tokens per verse (35 per LXX word, Hebrew included), plus the prefix of about 4.6k tokens (cached after the first call). The answer is about 16 estimated tokens per word, about 390 per verse; a whole Ruth chapter (8–10k output tokens) **does not fit the default `MaxOutputTokens` 8192**: set `MaxVersesPerPackage` about 10 (or `MaxOutputTokens` 16384+) for the LXX profile, otherwise the first cut-off answer halves the packages (one wasted call). Estimates only, no model was called.
- **Validator:** a word is dropped with a flag when its Strong's is not in TBESG, its lemma is not that entry's lemma, its code fails `GreekMorphologyCode`, a Hebrew number does not exist (always for additions), or it is answered twice; missing words are flagged; an empty Strong's (no TBESG entry, e.g. Μωάβ, Ὀρφά, Ἀβειμέλεχ) is the `no-strongs` flag. The verse is rejected only when unreadable, answered twice, or without any good word. Lemma matching is on the normalized form, so TBESG's oxia accents, case and STEP list lemmas (`ἔπω, ἐρῶ, εἶπον`) match; the stored lemma is TBESG's in NFC.
- **Finding, five-digit Strong's:** TBESG numbers 1,485 LXX-only entries from G10000 on (`G20969` πήσσω). `LxxWordTags_Strongs_CK` (four digits) would have refused them at confirm, so 0052 widens it to `^G[0-9]{4,5}[A-Z]?$`, and the gloss lookup in `LxxReadSql` parses the number with a regex instead of `substring(… FOR 4)`. The frontend's `normalizeStrongs` and the Strong's page are not checked for five digits (0028.9f).
- **Supersede fix:** `AgentStoreSql.SupersedeOpen` now uses the key of `AgentVerseProposals_Open_UQ` (book, chapter, verse, `Language`, `Flags.textForm`, `Flags.subverse`), `CountProposedVerses` counts by the same key. Also language-aware now: `FlaggedVerses` of the run list (an LXX `Flags` object counts only when its `flags` array is not empty), the mapping review (`AgentReviewService` refuses to merge LXX proposals, bulk merge skips them, the diff returns 404) and the scoring (an LXX run is not scored); `AgentProposalFlagsJson` reads both `Flags` shapes. `GetProposedRefIds` stays verse-level; the LXX builder skips by `Flags.lxxVerseId` instead (subverses and Daniel forms share a BBCCCVVV). The MCP package tool rejects `lxx-tag`.
- **No `LxxWordTags` writes from AI runs**: proposals live in `AgentVerseProposals` until the review page confirms them (0028.7f); 05-tagging.md never said otherwise.
- **Token usage per verse:** every attempt logs input/output tokens per package verse and tags the span with `verses` and `tokens.per_verse`; package and run totals are stored as before.
- **Dev DB:** migration 0052 was **not** applied (it ran only inside rolled-back test transactions). Before the first real run: `dotnet run --project src/UnshackledWord.Tooling.Postgres.Migration` (or restart the WebApi container), otherwise starting the run fails on `AgentRuns_Mode_CK`.
- **Deviations:**
  - 3 worked examples (Gen 1:1, Ruth 1:4 with two names TBESG lacks and a corrected NT genitive, the addition Job 36:28.1), all `[UNCONFIRMED]`; they are built from the fixture `tests/UnshackledWord.Application.Tests/Features/AgentMapping/Fixtures/lxx-prompt-examples.json` (281 kB: 6 units, the reachable part of TBESG and the NT forms with full frequencies), exported by `LxxAgentDatabaseTests.ExportPromptFixture` with `UNSHACKLEDWORD_LXX_FIXTURE_OUT`.
  - Package splits on a cut-off answer rebuild the BBCCCVVV range of the package, so a split in Daniel could re-send units of the other text form; harmless (same run supersedes), not fixed.
  - The candidate heuristic misses spellings Swete writes otherwise (`Βαιθλέεμ` vs TBESG `Βηθλεέμ` G0965); the prompt tells the model to answer the TBESG entry.
  - Pre-existing prompt test failures: `AgentPromptProviderTests…StablePrefix` failed because a heading in `greek-v2.md` read `## Example: Mk 1:6` (number missing), fixed to `## Example 2:`. `AgentPromptExampleTests` greek-v2 still fails: the maintainer re-exported Mk 1:1/1:6 (and their answers) from live data and removed `[UNCONFIRMED]` from the first two, so they no longer equal what the builder makes of the 0018 fixture (`lemma-prompt-examples.json`). The test's heading parser now accepts a confirmed heading; the fixture has to be re-exported (or the examples restored) by the maintainer.
- **Tests:** Application: 7 `LxxAgentPackageBuilderTests` (+7 Strong's cases: Ruth 1:1 content with Hebrew and prefills, addition without Hebrew, TBESG lemma in the prefill, chapter/40/subverse cut, resume, hand-over, candidates), 6 `LxxAgentValidatorTests`, 3 `LxxAgentPromptTests` (stable prefix, examples equal the built package and pass the validator), 2 `LxxAgentRunTests` (run loop end to end over Ruth 1:1-2 with a scripted fake model: one proposal per verse, `G99999` and `V-XYZ` dropped with flags, usage recorded; supersede by unit). Infrastructure: `AgentSqlTests` key test, 5 `LxxAgentDatabaseTests` against the dev DB (0052 twice, read SQL, language-aware supersede regression with Gen 1:1 Hebrew + LXX, Ruth dry run, fixture export). Application 1412 pass, 1 fails (greek-v2 examples, above); WebApi 281 (+42 skipped) and McpServer 53 pass.

**0028.7f done (backend 6dbda1e, frontend and the four run options, version unchanged, not committed):**

- **Backend (earlier):** contracts `Domain.WebApi/Maintainer/LxxReviewContracts.cs`; `Application/Features/Lxx/Review` (`LxxReviewService`); `Infrastructure/Lxx/LxxReviewRepository` + `LxxLexiconCache`; endpoints `Endpoints/Maintainer/LxxReview/*` (Admin/Maintainer); rules and routes in [05-tagging.md](05-tagging.md#review-and-confirm). Source rule `ai` / current source / `human`; audit `confirm`/`edit`; proposal `merged` only on an exact match; discard keeps nt-form proposals; safe confirm only AI `high`. Tests: 27 `LxxReviewServiceTests`, 7 `LxxReviewDatabaseTests`, 14 `LxxReviewEndpointTests`.
- **Run options (maintainer decisions 2026-11-26, [06a](06a-review-page.md#maintainer-decisions-2026-11-26)):** the run requests (`api/maintainer/agent-runs` and `/estimate`, `Tooling.AgentWorker`) take `includeConfirmed` (`--include-confirmed`) and `textForm` `og`/`th` (`--text-form`), both only with mode `lxx-tag` (400 otherwise; `textForm` 400 for other values). The executor builds the packages later from the stored run, so the options live on the run: migration `0053_AgentRunLxxOptions.sql` (`AgentRuns."IncludeConfirmed"` bool default false, `"TextForm"` nullable `og`/`th`, `ExpectedMigration` = 53; it is **not applied to the dev DB**, apply it before the first run and before the Infrastructure DB tests that insert runs). `AgentBuildRequest.TextForm` makes `LxxAgentPackageBuilder` build only the units of that form (Daniel; other books have no form). `AgentRunExecutor` passes both on build and on package splits.
- **Frontend:** route `edit/lxx/:bookId?/:chapter?` (`handle: { layout: "fill" }`), menu item "LXX prüfen" for write roles, `pages/LxxReviewPage.tsx`, `pages/lxxReview/*` (`ChapterList`, `UnitCard`, `HebrewLine`, `WordTable` with mobile cards, `WordEditorRow`/`WordEditor`, `LemmaSearchField`, `MorphologyField`, `SafeConfirmDialog`, `DiscardDialog`, `wordView.ts`), `lib/maintainer/lxxReview.ts` (contracts, hooks under `["maintainer","lxxReview",…]`, `useUnitDrafts` + `reconcileDraft`, `safeSelection`, `selectionRange`, `buildConfirmRequest`), `lib/agent/estimate.ts` (`sumEstimates`), i18n `lxxReview.*` and `agent.dialog.{includeConfirmed,blocks}`. No new shared component; `AgentRunDialog` got the optional props `modes`, `reviewHref`, `blocks`, `textForm` (in `Components.md`).
- **Decisions as built:** (1) the dialog shows "auch bestätigte Verse neu taggen" (default off) in mode `lxx-tag`; (2) a selection with gaps is one run per contiguous block: each block is estimated on its own (`useAgentEstimates`), the dialog shows the blocks and the summed estimate, "Lauf starten" posts block 1 and each next block only when the previous run is `completed` (a failed, cancelled or capped run stops the chain with a notice); a verse is selected when one of its units is, so its subverses and (without text form filter) both Daniel forms come with it; (3) in Daniel the `form` filter is sent as `textForm`; (4) default filter `open` (nt-form or AI proposals), `low` switchable.
- **Behaviour details:** per-unit save state (not the shared `isPending`), the mutation hooks do not await the refetch so the card state is set first and the refetch then reconciles the draft (own save: follow the server; unsaved edits against a changed proposal: `conflict`, edits kept); parse code upper-cased on blur only; confirm waits while an edited code is still being checked; safe confirm skips dirty and conflicting units and counts them in the dialog; units saved in this session stay visible; keyboard per 06a (rows are focusable, `j/k`, `e/Enter`, `p`, digits with a 600 ms two-digit window, `x`, `Esc`, `Ctrl/Cmd+Enter`); the layout switch (table/cards, list/selects, AI button in header/sticky bar) uses `useMediaQuery` so only one variant is in the DOM.
- **Checks:** typecheck, lint (8 pre-existing warnings), `check:components` (75 ok). Backend and Vitest numbers are in the hand-off report.
- **Not verified in a browser:** needs a maintainer login, which was not available; covered by tests only. Drag/shift-click range selection and the real focus movement are tested only through jsdom.

**0028.8 (seed export/import of confirmed tags and links) done (version unchanged, not committed):**

- **Files as built:** `assets/seed/lxx/tags.csv` = `RahlfsRef,TextForm,Position,SrcHash,Strongs,Morphology,Source`; `links.csv` = `RahlfsRef,TextForm,Position,SrcHash,HebLxxRefId,HebPosition,HebSrcHash` (comma CSV, header, LF, UTF-8 without BOM, sorted by Rahlfs key). **Deviation: no `Lemma` column** (04-model listed one, but a lemma is Greek script and the acceptance demands no Greek letters): import takes the lemma from TBESG by Strong's (a Strong's TBESG lacks is skipped). Extra column `Source` (`nt-form`/`ai`/`human`) keeps how a tag came about; empty or unknown on import = `human`. The Hebrew side is the `SourceKey` of the STEP word (`StepHebrewWords.LxxRefId`, position, hash), re-resolved with `SourceWordResolver`. The committed files are header-only (empty seed); `manifest.json` gets its `lxx` part on the first export, like `tsk`.
- **Code:** `Infrastructure/Seed/Lxx` (`LxxSeed`, `LxxSeedService`, `LxxSeedLines`), `Domain.WebApi` `ImportLxxSeedRequest` / `LxxSeedImportPreviewResponse`, endpoints `Endpoints/Maintainer/LxxSeed/{Export,Import,ImportPreview}` (`POST maintainer/lxx/seed/export|import`, `GET .../import/preview`, Admin/Maintainer, jobs `lxxSeedExport`/`lxxSeedImport`, kind `lxx`), SeedDb runner `LxxTagSeedRunner` (after `LxxTextImporter`; applies the files only while `LxxWordTags` has no confirmed row, else logs "LXX tags up to date, no import") and verb `lxx-seed export|import [--exact] [--dir DIR]`. Bruno `LxxSeedExport/Import/ImportPreview.bru`.
- **Import:** a line is anchored to its `LxxWords` row by (Rahlfs ref, text form, position) plus the stored hash; a missing word or changed hash is skipped and counted (`anchorMismatch`, list `skippedRows`). Merge: insert missing, differing or proposed tags become the file's confirmed tag, proposed links the file confirms become confirmed, nothing deleted. Exact ("Exakt wie Datei"): also deletes confirmed tags and links the files lack; proposals stay untouched in both modes. Rows get `UserId` NULL, one `LxxTagAudit` row `import` per changed verse (before/after of the touched words). The preview runs the mirror import in a rolled-back transaction.
- **Frontend:** export button "LXX-Tags exportieren" on Wartung (`SeedRoundTripSection`), card "LXX-Tags" on `/edit/imports` (`pages/LxxSeedJobsSection`, checkbox "Exakt wie Datei" with confirm dialog showing tags and links to delete), `lib/maintainer/lxxSeedJobs.ts`, i18n de/en, count labels `tags`, `links`, `anchorMismatch`. No new shared component.
- **Verified on a scratch DB** (`uw_0028_seed_test`, migrations + SeedDb Swete/STEP import, dropped afterwards): 5 confirmed tags and 4 links for Ruth 1:1 (plus a proposed tag and link) exported with the `lxx-seed` verb, confirmed rows wiped, imported again: identical (the hand-inserted lemmas were not NFC, import stores TBESG's NFC form), one audit row `import` without user, `rg` for Greek/Hebrew letters over both files finds nothing. Dev DB untouched (0 confirmed tags, 0 audit rows).
- **Tests:** 5 `LxxSeedFileTests`, 8 `LxxSeedDatabaseTests` (round trip, anchor mismatch, missing Hebrew word, merge update, proposal confirmed, exact mode keeps proposals, initial apply, no files), 6 `LxxSeedEndpointTests`; 3 + 2 Vitest tests (`SeedImportsPage.lxx`, `SeedRoundTripSection.lxx`).

**0028.9 (LXX occurrences by Strong's, backend) done (version unchanged, not committed):**
- **Request/response:** `GET api/elberfelder/strongsOccurrences/strongs/{strongs}?corpus=lxx&page=&pageSize=` (NT/OT behaviour unchanged; `corpus` other than `lxx` and a Hebrew number with `lxx` give 400). The response keeps the paging fields, `corpus: "lxx"`, `items: []`, `lxxItems[]` (`lxxWordId`, `lxxVerseId`, Rahlfs `ref`, `textForm`, book/chapter/verse/subverse, `words[]` with `matched` on the tagged word, `elb` = `{ hebRefId, bibleBookId, chapter, verse, text, elbIsAnchor }`, absent without Hebrew verse or anchor) and, for Admin/Maintainer only, `unconfirmedCount` (proposed tags with this Strong's). Anonymous callers never see proposed rows or their count; the list and `totalItems` are confirmed only, for everyone. The LXX answer is `Cache-Control: private, no-cache`.
- **Code:** `StrongsOccurrenceService.GetLxxAsync` (same key resolution as the NT list, `G746` to `G0746`, five digits allowed, lexicon variants without a letter), `ILxxStrongsOccurrenceRepository` / `Infrastructure/Lxx/LxxStrongsOccurrenceRepository` (count with `FILTER` by status on `("Strongs", "Status")`, page ordered by `SortOrder, Position` with the Elb1871 verse joined, then one query for the words of the page's verses), validator rules for `Corpus`. One occurrence per tagged word.
- **Timing (rolled-back transaction, 48,624 confirmed tags of G2532 simulated from the proposals):** count 15 ms, page 0 about 200 ms, page 100 about 340 ms (sort over all confirmed rows; fine at this scale).
- **Tests:** 6 service tests (anonymous without count, maintainer count, page beyond the end, Hebrew/invalid, marking, anchor), 1 database test (count by status, confirmed-only page, offset, verse words).

**0028.9f (Strong's page LXX corpus) done (version unchanged, not committed):**
- **UI:** on `/bible/strongs/G…` a Greek number shows the switch "NT | LXX" (`Button` pair in a `fieldset`, `aria-pressed`, URL `?corpus=lxx`, page reset); a Hebrew number shows no switch (only one corpus). The LXX list shows the Rahlfs ref (with `og`/`th` for Daniel), the Greek verse with the word in `mark.bg-mark-linked`, and the Elb1871 verse text (additions: "Zusatz der LXX, steht bei Elb1871 …"). The ref link opens the reader at the Elb verse and stores `uw.sidePanel = lxx` first (`writeSidePanelMode`, extracted from `useSidePanelMode`). Maintainers see the muted line "n weitere Vorkommen noch ungeprüft"; empty state "Noch keine geprüften LXX-Vorkommen". Book/rendering filters apply to the NT list only. `normalizeStrongs` already accepts five digits.
- **Code:** `StrongsPage` (local `LxxRow`, no shared component, `Components.md` unchanged), `useLxxStrongsOccurrences`, `parseCorpus`, `bibleApi.strongsOccurrencesLxx`, i18n `bible.strongs.corpus.*` / `bible.strongs.lxx.*`.
- **Tests:** 9 Vitest tests (`StrongsPageLxx.test.tsx`: switch for G only, list and marking, anchor addition, link target plus stored panel mode, maintainer line, no line anonymous, empty state, back to NT).
- **Browser check** (local API on 5142, Vite on 5199, one temporary confirmed tag for word 486320, deleted afterwards, 0 confirmed tags remain, `uw.sidePanel` reset): G746, LXX, Gen.1:1 with ἀρχῇ marked and the Elb verse; click opens `/bible/1/1#v1` with the "Septuaginta (Swete)" panel.

**0028.10 (Hebrew links in `api/lxx/chapter`) done (version unchanged, not committed):**
- **Response:** each LXX word gets `hebrewWordIds` (STEP Hebrew word ids, as the original panel uses them) from `LxxHebrewLinks`: confirmed for everyone, proposed too for Admin/Maintainer, plus `proposedHebrewWordIds` (the proposed subset, flag for the UI). Both omitted when empty; an unconfirmed link gives an anonymous caller nothing.
- **Query:** still one round trip (three result sets); the words query gets a `LEFT JOIN LATERAL` that aggregates the links per word with `array_agg ... FILTER (WHERE "Status" = ...)` into `ConfirmedHebrewWordIds`/`ProposedHebrewWordIds` (`LxxWordRow`); no extra rows. Visibility in `LxxChapterReadService.ToWord`.
- **Tests:** 1 `LxxChapterReadServiceTests` (visibility, order, empty arrays), 1 `LxxTagsDatabaseTests` (rolled-back transaction: confirmed/proposed arrays, no row multiplication).

**0028.10f (hover link in the reader) done (version unchanged, not committed):**
- **Behaviour:** hovering or keyboard-focusing an LXX word in the LXX panel marks the Elb1871 words that its Hebrew words map to (`bg-mark-linked`, the existing `sideLinks` marking), no request on hover. The Hebrew words themselves are not visible then (one side panel mode at a time). The unit's own Elb verse is used (`VerseSide` passes it), so Ps 10:1 / LXX 9:22 and Jeremiah land on the right verse.
- **Code:** `ChapterPage` (`hoveredLxx`, tied to the `book:chapter` key like the 0027 similar hit, so a chapter change drops it; also cleared on a mode switch), `VerseSide` prop `onHoverLxxWord`, `lib/bible/lxx.ts` `lxxHebrewWordIds`, `LxxWord.hebrewWordIds`; `OriginalVerse` untagged LXX words now report hover/focus too. `Components.md` updated.
- **Deviation:** the chapter originals (`useChapterOriginals`, which hold the Hebrew-to-German links) are now also loaded in LXX mode (one cached request per chapter on entering the mode). A revised verse (revision words) is not marked, like the Grundtext hover.
- **Tests:** 4 Vitest tests (`ChapterPage.lxxHover.test.tsx`: marks and clears on leave, no marks without links or with an empty list, keyboard focus, cleared on chapter change). Browser check with one temporary confirmed tag and link on Gen 1:1: hovering ΑΡΧΗ marks "Anfang"; leaving clears it; Ps 10 shows no marks. The test rows were deleted afterwards (0 confirmed tags, 0 links).

**0028.11 (docs) done:** CLAUDE.md (decision and architecture), README (LXX data checked), docs/deployment.md (LXX section). Fresh-database check: see below.

**Fresh-database check (2026-11-26):** scratch database `uw_0028_fresh`, all migrations (to 0053) plus a full SeedDb run in 5 min 14 s with no errors. The LXX text import gave 23,620 verses and 486,313 words in 10.4 s, the same as the dev DB, and Jer 32:1 has `HebRefId` 24025015. `LxxTagSeedRunner` applied the empty seed (0 tags, 0 links). The scratch database was dropped afterwards. The confirmed Ruth tags from the original "Done when" could not be checked, because no tag is confirmed yet.
