# 0031 · Goal

## What is wrong today

`compose.yml` starts `unshackledword.tooling.seed-db` after the migration, and the WebApi waits for it (`depends_on … seed-db: service_started`). The seed runs `SeedingService` with 20 runners in a fixed order: STTS tags, revision levels, Elb1871 text and verses, repo morphology, TSK and its curation seed, SR GNT, SBL, five STEP importers, verse similarity, LXX text and tags, Strong's-to-text, AI mappings, revision seed.

- A fresh run takes about 8 minutes (0029.11), downloads several GB into `temp/` (4.1 GB on the dev machine) and is watched only through `docker compose logs`.
- It fails silently: the first fresh run of 0029.11 hit an Npgsql read timeout in `ElberfelderTextRunner` and **still exited 0**, so compose started the API on a half-filled database.
- `docs/deployment.md` warns that a bare `docker compose up -d` re-runs the seed on every start; only the per-runner "skip when already filled" checks keep that harmless, and not every runner has one (`ElberfelderTextRunner` has none).
- Nobody can see what has been imported. The dev database has 722,743 `Elb1871Words`, 142,095 `StepGreekWords`, 305,648 `StepHebrewWords`, 378,805 `Tsk` rows, 917,459 `VerseSimilarities` — and five of its six materialized views (`StatsStrongs`, `StatsStrongsRenderings`, `StatsGermanLemma`, `StatsGermanForms`, `DqWordFacts`) are not populated. That gap was invisible until a query for this roadmap failed on it.

## End state

- The stack starts without a seed container. Migration runs, the API starts in seconds on an empty database (books from migration 0001 only, no Bible text).
- The first person registers (the first account becomes Admin, `AuthRepository`), opens the gear menu → "Ersteinrichtung" (`/edit/setup`) and sees every seed step with its tables, row counts and a red/amber/green indicator.
- They run one step, a group, or "Alle ausführen"; progress shows on the page, the counts update, a failure is a red row with the log, not a silent exit 0.
- The SeedDb CLI stays as a thin wrapper around the same library for development, CI and its verbs.

## Out of scope

- Re-importing or emptying a step that is already green ("Leeren und neu importieren"). Signal to do it: an amber step after a failed run that cannot be fixed by running it again.
- Moving `BibleBooks` out of migration 0001 (decided: they stay, [02-decisions.md](02-decisions.md)).
- Runners that are not in `SeedingService` today (`ByzRunner`, `GbtRunner`, `OpenScriptureRunner`, `LemmatizerRunner`, `Elb1871SrMappingRunner`, `LocalOnlyRunner`, `StepFullTextSearchRunner`): they stay CLI-only.
- The vocab, notes, TSK and LXX imports on `/edit/imports` and the seed round trip on "Wartung": the setup page links to them, it does not replace them.
- A production frontend image (`docs/deployment.md` §5).
