# 0027 · Decisions

## Maintainer (interview 2026-11-25)

- **Strong's only.** Similarity is computed on original-language Strong's numbers (Hebrew/Aramaic for the OT, Greek for the NT), not on German lemmas.
- **OT ↔ NT is deferred.** Within-testament only; NT quotations of the OT are not found by this roadmap.
- **Completely separate from TSK.** Own tables, own panel mode, no writes to `TskCurations` or proposals.
- **Rebuild only.** Derived data like `StepGreekWordEditions`: rebuilt from the database, no export/import file, nothing in `assets/seed`.
- **Shown in the reader side panel** as its own mode.
- **Word runs get a bonus** on top of the shared-word score, and the matched run is shown as a phrase.

## Plan decisions

- **Precompute in C#, not SQL.** The scorer is a pure class in `Application/Features/VerseSimilarity` (unit-testable with a toy corpus), fed by a Dapper token loader. SQL would need a self-join over posting lists that is hard to bound; C# keeps memory and time predictable and lets the run log progress.
- **Rebuild swaps atomically.** A run writes into `VerseSimilarities` inside one transaction (delete + bulk insert via `unnest`/COPY), so readers see either the old or the new ranking, never a half table.
- **Explanations are computed at read time**, not stored: the endpoint intersects the Strong's of the two verses (cheap for at most 30 pairs per verse). The table stays slim, and the explanation always matches the current words.
- **Parameters live on the run row** (`StopDfShare`, `TopK`, `MinScore`, `PhraseWeight`) with defaults in config `VerseSimilarity:*`, so a rebuild with other values is a maintainer action, not a code change.
- **Public read**, maintainer-only rebuild (`Admin`/`Maintainer`), like every other derived table.
- **Version:** 0.12.0 with 0027.1, then a build number per row.
- **Tuned defaults (2026-11-25, roadmap 0027.6): `StopDfShare` 0.03, `TopK` 30, `MinScore` 0.1, `PhraseWeight` 0.5** (only `StopDfShare` changed, from 0.02). Dry runs on the dev DB (`verse-similarity --dry-run`, evaluation against the raw TSK links in the same testament; golden ranks forward/reverse, "–" = not in the stored top 30):

  | StopDfShare | PhraseWeight | MinScore | Stop words | Pairs | OT R@30 / R@10 % | NT R@30 / R@10 % | Hebr 4,12→Offb 1,16 | Hebr 4,12→Offb 2,12 | Mk 14,58→2Kor 5,1 | Apg 7,48→Apg 17,24 | Ps 149,6→Jes 41,15 |
  |---|---|---|---|---|---|---|---|---|---|---|---|
  | 0.01 | 0.5 | 0.1 | 394 | 893,031 | 18.7 / 11.4 | 18.7 / 10.7 | –/– | 11/22 | 8/1 | 1/1 | 4/2 |
  | 0.02 (old) | 0.5 | 0.1 | 192 | 914,983 | 20.2 / 12.5 | 20.7 / 12.4 | –/– | 11/29 | 6/1 | 1/1 | 3/4 |
  | 0.02 | 0.25 | 0.1 | 192 | 914,951 | 20.2 / 12.5 | 20.7 / 12.3 | –/– | 11/29 | 6/1 | 1/1 | 3/4 |
  | 0.02 | 1.0 | 0.1 | 192 | 915,000 | 20.0 / 12.3 | 20.7 / 12.4 | –/– | 11/29 | 7/1 | 1/1 | 3/4 |
  | 0.02 | 0.5 | 0.05 | 192 | 917,860 | 20.2 / 12.5 | 20.7 / 12.4 | –/– | 11/29 | 6/1 | 1/1 | 3/4 |
  | **0.03** | **0.5** | **0.1** | 130 | 917,459 | 20.8 / 13.0 | 21.3 / 12.9 | –/– | 13/– | 6/1 | 1/1 | 4/4 |
  | 0.03 | 1.0 | 0.1 | 130 | 917,501 | 20.5 / 12.7 | 21.3 / 12.9 | –/– | 13/– | 7/1 | 1/1 | 4/4 |
  | 0.03 | 0.5 | 0.05 | 130 | 921,109 | 20.8 / 13.0 | 21.3 / 12.9 | 30/– | 13/– | 6/1 | 1/1 | 4/4 |
  | 0.04 | 0.5 | 0.1 | 96 | 922,334 | 21.2 / 13.3 | 21.6 / 13.1 | –/– | 15/– | 6/1 | 1/1 | 4/5 |
  | 0.05 | 0.5 | 0.1 | 79 | 922,558 | 21.3 / 13.4 | 22.0 / 13.4 | –/– | 16/– | 8/1 | 1/1 | 4/4 |
  | 0.05 | 0.25 | 0.1 | 79 | 922,454 | 21.3 / 13.4 | 21.9 / 13.3 | –/– | 16/– | 8/1 | 1/1 | 4/4 |
  | 0.05 | 1.0 | 0.1 | 79 | 922,656 | 20.9 / 13.1 | 22.0 / 13.3 | –/– | 16/– | 8/1 | 1/1 | 4/4 |

  Reasoning:
  - **`PhraseWeight` and `MinScore` barely move anything** (≤ 0.3 points recall, golden ranks ±1). 0.5 stays: it already puts the synoptic parallels and Isa 2:4 → Mic 4:3 (12 shared bigrams) on top, and 1.0 costs OT recall. `MinScore` 0.05 only adds ~3,000 single-word hits below 0.1 (it brings Rev 1:16 in at rank 30 for Heb 4:12, but that alone is overfitting to a golden pair).
  - **`StopDfShare` is the only lever**, and recall rises with it (fewer cut words). 0.01 is clearly worse. 0.02 cuts πίστις "faith" in the NT (df above 159 verses), so Rom 1:17 ↔ Gal 3:11 / Heb 10:38 ("the righteous shall live by faith") match only on δίκαιος + ζάω. 0.03 keeps faith-level words and still cuts ἡμέρα, υἱός, οὐρανός, πνεῦμα (NT) and the Hebrew words above ~700 verses; recall +0.6 points in both testaments.
  - **0.04/0.05 gain another 0.5–0.7 points** and make some NT lists richer (Lk 3:22 adds "heaven, son, spirit", Mk 14:58 puts Jn 2:19–20 "three days" at 4–5), but bring formula and frequent-word noise: "first/beginning" fills Gen 1:1 (Job 40:19, Prov 3:9), and with 79 stop words long OT verses gain common-word hits. Heb 4:12 → Rev 2:12 falls to 15–16. The quotation formula καθὼς γέγραπται already shows from 0.03 (Rom 1:17 → Rom 3:10 rank 1, 1 Cor 1:31 rank 6); that is the visible cost of 0.03.
  - The qualitative check (`--show` Mk 14:58, Lk 3:22, Mt 14:19, Rom 1:17, Isa 2:4, Ps 149:6, Gen 1:1, Num 7:19) is the same at 0.02 and 0.03 for the parallel passages: Mt 26:61 / Mt 27:40 / Mk 15:29 for Mk 14:58, Mk 1:11 / Mt 3:17 for Lk 3:22, Lk 9:16 / Mk 6:41 for Mt 14:19, Mic 4:3 for Isa 2:4; Num 7 floods itself as expected ([07-risks.md](07-risks.md)).
  - **Golden pairs:** Acts 7:48 ↔ 17:24 rank 1 both ways and Mk 14:58 → 2 Cor 5:1 rank 6 (reverse 1) everywhere; Ps 149:6 → Isa 41:15 rank 3–4 everywhere. Heb 4:12 → Rev 1:16 is not in any top 30 (one shared word, δίστομος, against a long verse; see 0027.1). Heb 4:12 → Rev 2:12 stays in the top 30 (13); the reverse (29 at 0.02) drops out. Accepted: not a reason to keep "faith" a stop word.
  - Recall stays around 21 %: most TSK links are thematic or keyword links without shared rare Strong's, which this roadmap does not try to find.

## Code findings the plan rests on

- STEP words are keyed by `LxxRefId` (`StepGreekWords`, `StepHebrewWords`); `Elb1871Verses` has both `HebRefId` and `LxxRefId`. The reader works in `HebRefId`. The table stores `LxxRefId` pairs (the STEP key) and the endpoint resolves them through `Elb1871Verses`.
- Hebrew words hold compound Strong's (`H9003/{H7225G}`); `StepHebrewWordsNormalized` + `…ToHebrewWords` already split them per morpheme with `IsRoot`. Tokens are the root pieces; prefixes H9001–H9xxx are always stop words.
- Greek: `StepGreekWords.DisambiguatedStrongs`; edition membership via `StepGreekWordEditions` (0025). A word counts if it is in at least one main edition.
- Side panel modes: `SidePanelMode = "tsk" | "original" | "off"` in `lib/bible/sidePanel.ts`, `G` cycles; `VerseSide.tsx` renders per verse via `VerseSidePanel`. The TSK hover card (original text) is reused for hits.
- `MigrationsHealthCheck.ExpectedMigration` (`Infrastructure/Observability/HealthChecks.cs`) must be raised with the migration.
