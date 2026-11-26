# 0028 · Decisions

## Maintainer decisions (interview 2026-11-25)

1. **Text: Swete, renumbered to Rahlfs.** The Greek words come from Swete, which is public domain. We give every verse a Rahlfs reference so that TVTMS (Greek column) can map it to STEP and Elberfelder. CCAT Rahlfs is not used at all, not even as a check.
2. **Morphology: AI-tagged, human-confirmed.** Lemma, Strong's and parse are AI proposals under the AI's own user id, and the maintainer confirms them. This is the editing model of the Elb1871 mappings. The confirmed result is CC0 and goes into the seed.
3. **First visible results, all four:** the LXX verse in the reader, LXX word details, Hebrew ↔ LXX word links, and searching the LXX by Strong's.
4. **Books: protocanonical only.** These are the books with a Hebrew counterpart. The Greek additions to Esther and Daniel and the extra passages in 3 Kingdoms are kept as extra verses (subverses) of their book.

## Plan decisions

- **Rahlfs numbering is the key and TVTMS the bridge.** We store the Rahlfs reference (book, chapter, verse, subverse) and resolve `LxxRefId` (English/NRSV counting, the STEP key; see [03-versification.md](03-versification.md)) and `HebRefId` from the TVTMS Greek column. We don't write a mapping of our own straight to English counting.
- **Only the Swete → Rahlfs differences are our own data:** `assets/seed/lxx/swete-to-rahlfs.csv`, a short CC0 exception list. Where Swete and Rahlfs agree, a verse keeps Swete's reference.
- **The Swete text is downloaded, not redistributed.** We pin the commit and download it to `temp/SeedData/Swete/`, the same way as STEP. eliranwong's repo is GPL-3.0, so our seed files carry only references, positions and a short word hash (the `SourceKey` pattern of `Application/Features/Seed/Anchoring`), never the Greek text.
- **Public reads show only confirmed tags.** A proposal is visible only to `Admin`/`Maintainer` (on the review page and in the reader with a "Vorschlag" mark). An untagged word shows its Greek form only. A learning tool that teaches a wrong parse is worse than one that shows none.
- **Morphology codes are the Robinson-style codes of STEP's Greek NT** (`StepGreekWords.Morphology`, e.g. `V-AAI-3S`), so the existing parse display and filters work unchanged. Lemma and Strong's come from TBESG. The validator rejects a Strong's that TBESG doesn't have.
- **Proposals reuse `AgentVerseProposals`.** `Language` gains `'lxx'`, word tags go in `Lemmas`, and Hebrew links go in `Links`. Runs, packages, profiles and the review service stay as they are.
- **Seed rule like TSK curation:** SeedDb applies `assets/seed/lxx/*.csv` only to a fresh database (empty `LxxWordTags`). After that, only the maintainer export ("Wartung") and import ("Importe") change it.
- **Double texts:** Swete prints Daniel in both OG and Theodotion, and Rahlfs has both as well. Both are kept with `TextForm` (`og`/`th`), and the reader shows Theodotion by default. Joshua and Judges: Swete prints codex B, which Rahlfs keeps as the B column, so B is the only form we have. 0028.1 checks this against the CSVs.

## Decisions in 0028.2 (versification)

- **"Rahlfs" = the TVTMS Greek column.** We store TVTMS's Greek key, even where it differs from a printed Rahlfs, for example Jer 30 (Ammon at 30:17-21) and Prov 24-31 (Hebrew numbers in the Greek order). TVTMS is the bridge, so our key has to be its key.
- **Inline additions are subverses, by a marker rule.** Swete's `[N]` marks are its a, b, c … pluses, not verse numbers of their own. A verse is cut at its markers, and the k-th segment becomes subverse k (Est 1:1.1-17, 1Ki 2:35.1-14, Prov 3:16.1 …). This rule is code, not 232 CSV rows. The CSV handles only the irregular cases:
  - Est 1:1 `[18]` is the canonical 1:1;
  - Est 5:1-2 follow TVTMS's D numbering;
  - 2Sa 23:30 `[31]` stays one verse.
  
  Subverses that TVTMS lacks become additions after their main verse.
- **Swete following Greek2 (Gen 2, Exo 36/39, Job 16/39-41, Neh 4) maps back to the Greek column.** We use word splits (`Gen.3:1#1-15` = Gen 2:25) and joins (`Exo.36:23-24` = Greek 36:23). We do not use the Greek2 column itself.
- **TVTMS errors are fixed in code** (`TvtmsGreekFixes`: Mal 4, Dan 3:52-90, Dan 3:98-100, Exo 38:19, Deu 28:69). They are not written as fake Swete exceptions. 1Ki 14:1-20 needed no fix, because Swete leaves those verses empty.
- **Empty Swete verses** (one blank word, 240 of them) are `absent` by rule and are not imported.
- **`RahlfsRefId` = book·10⁸ + chapter·10⁵ + verse·10² + subverse.** The planned verse·10 + subverse cannot hold Est 4:17.30.
- **Psalm titles:** `LxxRefId` is verse 0, as in STEP's Hebrew words (`Psa.3.0`). `HebRefId` is Hebrew verse 1.
- **Reading order = Swete order.** Addition A precedes Est 1:1 even though its `RahlfsRefId` sorts after it.

## Code findings the plan rests on

- TVTMS condensed table starts at line 162 (`#DataStart(Condensed)`), records like `OneToOne  Psa.10:1-18  Psa.10:1-18  Psa.9:22-39 …` with columns English KJV, Hebrew, Latin, Greek, Greek2 (Brenton). Subverses for LXX additions (`1Ki.2:35.1`…, Esther additions) are part of the format.
- Swete `00-Swete_versification.csv`: no header, `id<TAB>Book.Chapter:Verse` (`1  Gen.1:1`, `11  Gen.1:2`); the id is the first word id of the verse in the word files. Book codes like `Gen`, `Exo`, `Jos`, `Jdg`, `1Ki`.
- `StepGreekWords` is NT only and keyed by `LxxRefId`. LXX words get their own tables, so the NT importer, editions (0025) and verse similarity (0027) stay unchanged.
- Migrations end at `0049`; this roadmap takes `0050`/`0051` (0026 is deferred indefinitely and gives up its claim).
