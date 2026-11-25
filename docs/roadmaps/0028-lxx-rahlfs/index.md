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

Interview held 2026-11-25 (see [02-decisions.md](02-decisions.md)). 0028.1 done (version 0.13.0); the rest is open.

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

Numbering (2026-11-25): this roadmap goes next and takes migrations `0050`/`0051` and version 0.13.0. 0026 (morpheme alignment) is deferred indefinitely and will take whatever numbers are free when it is picked up.
