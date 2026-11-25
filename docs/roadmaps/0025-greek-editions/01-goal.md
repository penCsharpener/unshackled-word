# 0025 · Goal

## Today

STEP's TAGNT lists, for every Greek NT word, the printed editions that contain it. SeedDb stores the column unparsed in `StepGreekWords.Editions` (`VARCHAR(100)`, max length on the dev DB 48). Nothing reads it: the word detail box, the Grundtext panel, MCP and the agent package ignore it. The only edition signal in use is the coarse `Type` column (`NKO`, `K`, `N(k)O` …), already split into `IsInNestleAland`, `IsInTextusReceptus`, `IsInOther`.

The column holds **editions, not manuscripts** (TAGNT header: "Editions: Byz=RP 2005; NA27; NA28; TR=Scrivener 1894; SBL=Holmes 2010; Treg=Tregelles 1879; WH=Westcott+Hort 1881; Tyn=Tyndale House GNT 2017"). Dev DB, 142,095 words:

- 444 distinct strings. `NA28+NA27+Tyn+SBL+WH+Treg+TR+Byz` (all eight) covers 130,322 words (91.7 %). The rest, about 11,800 words, is where a reader learns something.
- The eight main editions each appear on 137,000–138,000 words. `TR` only: 768 words; `TR+Byz` only: 2,445.
- Word-order shifts are written as a suffix: `TR»1` (moved forward 1 word), `Byz«3` (moved back 3), about 3,700 tokens, mostly TR and Byz. A few point across verses (`Byz«14.24`, 60 tokens) and a few are doubled (`»1»1`).
- Rare tokens, about 300 words: `KJV` (252, the English KJV follows this Greek), `NIV` (10), and real manuscripts or versions on fewer than 30 words (`01`, `02`, `03`, `04`, `05`, `06`, `032`, `P66`, `P66*`, `Coptic`, `Latin`, `Syriac`).

## End state

- The string is parsed once into a normalized n:m model (catalog of editions, one row per word and edition, with the order shift), kept in sync by the STEP import.
- The word detail box shows, for a Greek word that is **not** in all eight editions, which editions have it, which lack it and how the order differs.
- The Grundtext panel marks Greek words that are not in all eight editions.
- The reader can pick one edition (e.g. NA28 or TR); Greek words that edition lacks are hidden in the Grundtext panel.

## Out of scope (deferred)

- Reordering the Greek text to an edition's word order (shifts are shown, not applied).
- Exposing editions in MCP read tools or the agent mapping package.
- Hebrew (TAHOT has no comparable column) and the LXX.
- Any change to the Elberfelder German text or its mappings based on editions.
- Manuscript-level data (the `MeaningVariants`/`SpellingVariants` columns) beyond the few manuscript tokens in `Editions`.
