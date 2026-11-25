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

## Code findings the plan rests on

- STEP words are keyed by `LxxRefId` (`StepGreekWords`, `StepHebrewWords`); `Elb1871Verses` has both `HebRefId` and `LxxRefId`. The reader works in `HebRefId`. The table stores `LxxRefId` pairs (the STEP key) and the endpoint resolves them through `Elb1871Verses`.
- Hebrew words hold compound Strong's (`H9003/{H7225G}`); `StepHebrewWordsNormalized` + `…ToHebrewWords` already split them per morpheme with `IsRoot`. Tokens are the root pieces; prefixes H9001–H9xxx are always stop words.
- Greek: `StepGreekWords.DisambiguatedStrongs`; edition membership via `StepGreekWordEditions` (0025). A word counts if it is in at least one main edition.
- Side panel modes: `SidePanelMode = "tsk" | "original" | "off"` in `lib/bible/sidePanel.ts`, `G` cycles; `VerseSide.tsx` renders per verse via `VerseSidePanel`. The TSK hover card (original text) is reused for hits.
- `MigrationsHealthCheck.ExpectedMigration` (`Infrastructure/Observability/HealthChecks.cs`) must be raised with the migration.
