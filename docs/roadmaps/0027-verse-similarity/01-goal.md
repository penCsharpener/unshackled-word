# 0027 · Goal

## Today

The only cross references are TSK (`assets/tskxref.txt`, curated as a layer in 0024). TSK is hand-made and good, but it lists only what its editors noticed. Verses that share rare original-language words or short runs of them are not connected unless TSK happens to link them. Two examples:

- **"sharp two-edged sword":** ῥομφαία δίστομος ὀξεῖα (Rev 1:16, 2:12, 19:15 has ῥομφαία ὀξεῖα) and μάχαιρα δίστομος (Heb 4:12). In the OT, חֶרֶב פִּיפִיּוֹת (Ps 149:6; פִּיפִיָּה also in Isa 41:15). Measured in 0027.1: Heb 4:12 shares only δίστομος with Rev 2:12 (μάχαιρα ≠ ῥομφαία), so it ranks 11, Rev 1:16 34.
- **"not made with hands":** in Greek one rare word. ἀχειροποίητος G0886 (Mk 14:58, 2 Cor 5:1, Col 2:11) and χειροποίητος G5499 (Acts 7:48, 17:24, Eph 2:11, Heb 9:11, 9:24). Shared Strong's numbers alone rank these together, with no phrase logic.

## Feasibility

Measured in 0027.1 on the dev DB (`verse-similarity --dry-run`, defaults StopDfShare 0.02, TopK 30, MinScore 0.1, PhraseWeight 0.5, one machine, parallel scoring):

| | OT (Hebrew/Aramaic) | NT (Greek) |
|---|---|---|
| Verses (`LxxRefId`) | 23,261 | 7,958 |
| Tokens (root pieces / words in a main edition) | 305,671 | 142,084 |
| Distinct Strong's | 11,690 | 5,580 |
| Stop words | 101 (88 by df > 465 verses, 13 more H9xxx prefixes) | 91 (df > 159 verses) |
| Content tokens after the stop list | 164,784 | 50,076 |
| Pairs kept (top 30, score ≥ 0.1) | 680,025 (23,020 verses with hits) | 234,958 (7,929 verses with hits) |
| Load / corpus / scoring | 0.5–1.4 s / 0.1 s / 0.7–1.1 s | 0.6–0.9 s / 0.1 s / 0.1–0.2 s |

- **Whole dry run (both testaments): 2.3–3.8 s**, managed heap about 60–80 MB, peak working set about 200 MB (process including host and Npgsql). The inverted index (Strong's → verses) scores only pairs that share at least one non-stop word; no all-pairs step.
- **Storage:** 914,983 pairs (top 30 per verse above the minimum score), close to the estimated maximum of about 930k rows (about 60 MB with index), comparable to `StepGreekWordEditions` (1.1M rows).
- **On demand per verse** would also be fast (one SQL query over a posting table). The full precompute is chosen because it is cheap, gives a fixed ranking to inspect and evaluate against TSK, and keeps the reader endpoint a plain lookup.

## End state

- A table of the most similar verses per verse, within the same testament, computed from Strong's numbers with rare words weighted up and runs of shared words getting a bonus.
- It is rebuilt by SeedDb on a fresh database and by a maintainer job; nothing goes to `assets/seed`.
- The reader side panel has a third mode, "Ähnliche Verse", listing the hits per verse with the shared original words and phrases, and the German words linked to them highlighted.
- Every run records its parameters, duration and recall against TSK, so the parameters can be tuned with numbers.

## Out of scope (deferred)

- **OT ↔ NT matches** (Hebrew and Greek Strong's never meet). Later via LXX or a Hebrew→Greek Strong's equivalence map.
- **Any link to TSK:** no "adopt as TSK link", no hiding of hits TSK already has (the evaluation reads TSK, nothing writes it).
- **MCP tool**, export/import file, German-lemma similarity, revision variants.
- **User curation** of hits (confirm/hide). Signal to start it: maintainers keep wanting to save or suppress specific hits.
