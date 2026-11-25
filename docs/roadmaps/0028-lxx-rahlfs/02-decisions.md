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

## Code findings the plan rests on

- TVTMS condensed table starts at line 162 (`#DataStart(Condensed)`), records like `OneToOne  Psa.10:1-18  Psa.10:1-18  Psa.9:22-39 …` with columns English KJV, Hebrew, Latin, Greek, Greek2 (Brenton). Subverses for LXX additions (`1Ki.2:35.1`…, Esther additions) are part of the format.
- Swete `00-Swete_versification.csv`: no header, `id<TAB>Book.Chapter:Verse` (`1  Gen.1:1`, `11  Gen.1:2`); the id is the first word id of the verse in the word files. Book codes like `Gen`, `Exo`, `Jos`, `Jdg`, `1Ki`.
- `StepGreekWords` is NT only and keyed by `LxxRefId`. LXX words get their own tables, so the NT importer, editions (0025) and verse similarity (0027) stay unchanged.
- Migrations end at `0049`; this roadmap takes `0050`/`0051` (0026 is deferred indefinitely and gives up its claim).
