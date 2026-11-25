# 0028 · Goal

## What is missing

The README lists "LXX data" and "Hebrew OT tagged with the Greek LXX OT" as open goals. Today the OT reader shows only the Hebrew (STEP TAHOT) beside the Elb1871. Nothing in the database holds a Greek OT word. The only LXX code is `SeedDb/Services/EliranWongData/RalfsLxxParserStrategy`: a registered but unused stub that reads a local clone of eliranwong's Rahlfs repo and writes nothing.

Every digital Rahlfs 1935 is the CCAT/CATSS file. Its terms forbid commercial use and need a user agreement, which conflicts with the project's CC0 rule. Swete's *Old Testament in Greek* (1909–1930) is public domain, and eliranwong's [LXX-Swete-1930](https://github.com/eliranwong/LXX-Swete-1930) has it as word CSVs, but without morphology.

The other pieces are already in the repo:
- **TVTMS** (STEP, CC BY) is downloaded by SeedDb to `temp/SeedData/Step/`. Its "Greek" column is LXX numbering in the Rahlfs form (Hebrew Jer 25:15 = Greek Jer 32:1; Hebrew Ps 10:1 = Greek Ps 9:22).
- **TBESG** (11,035 entries) extends Strong's to LXX vocabulary (G6100–G9979 and G20000+).
- **The agent mapping pipeline** (roadmap 0016) already handles packages, provider profiles, proposals and maintainer review.

## End state

- The protocanonical LXX books are imported from Swete, and every verse carries a Rahlfs reference plus its STEP (`LxxRefId`) and Elberfelder (`HebRefId`) counterparts.
- The OT reader has a side panel mode "LXX" that shows the Greek verse that matches each Elb1871 verse, including the Greek additions.
- Every LXX word gets lemma, TBESG Strong's and a morphology code. The AI proposes them, the maintainer confirms them, and the public sees only confirmed tags.
- LXX words are linked to the Hebrew words they render, so hovering an LXX word marks the Hebrew word and its German rendering.
- The Strong's page lists LXX occurrences.
- Our tags and links are CC0 seed files keyed by Rahlfs reference and word position; the Swete text itself is downloaded at seed time.

## Out of scope (deferred)

- Deuterocanonical books (Tobit, Judith, Maccabees, Sirach, Wisdom, Baruch, Odes, Psalms of Solomon).
- Correcting the Swete text itself, and other LXX readings (Rahlfs, Göttingen or codex A apparatus).
- Using the LXX in verse similarity (0027) and in the vocab trainer (0022). Signal to revisit: confirmed tags cover at least one whole book.
- A German translation of the LXX, or Elb1871 ↔ LXX word mappings that bypass the Hebrew.
