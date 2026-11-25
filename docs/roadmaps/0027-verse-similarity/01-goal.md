# 0027 · Goal

## Today

The only cross references are TSK (`assets/tskxref.txt`, curated as a layer in 0024). TSK is hand-made and good, but it lists only what its editors noticed. Verses that share rare original-language words or short runs of them are not connected unless TSK happens to link them. Two examples:

- **"sharp two-edged sword":** ῥομφαία δίστομος ὀξεῖα (Rev 1:16, 2:12, 19:15 has ῥομφαία ὀξεῖα) and μάχαιρα δίστομος (Heb 4:12). In the OT, חֶרֶב פִּיפִיּוֹת (Ps 149:6, Prov 5:4).
- **"not made with hands":** in Greek one rare word. ἀχειροποίητος G0886 (Mk 14:58, 2 Cor 5:1, Col 2:11) and χειροποίητος G5499 (Acts 7:48, 17:24, Eph 2:11, Heb 9:11, 9:24). Shared Strong's numbers alone rank these together, with no phrase logic.

## Feasibility

Corpus: about 23,100 OT and 7,950 NT verses; about 305k Hebrew and 142k Greek words (exact numbers are measured in 0027.1).

- **All pairs naively:** about 480M verse pairs. Not needed. An inverted index (Strong's → verses) scores only pairs that share at least one non-stop word. Once the few hundred most frequent Strong's are dropped, the posting lists are short. Expected cost: seconds to a few minutes on one core in C#, in memory (a few hundred MB at most).
- **Storage:** top 30 per verse above a minimum score, at most about 930k rows (about 60 MB with index), comparable to `StepGreekWordEditions` (1.1M rows).
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
