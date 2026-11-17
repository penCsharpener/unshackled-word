# 0018 · Tasks

Work in table order or as "Depends on" allows; one row = one commit; every row keeps `dotnet build UnshackledWord.slnx`, `dotnet test` and the web `bun run check` green. Frontend rows (`f`) follow the conventions in `CLAUDE.md` (tokens only, `Components.md`, propose new components first).

| # | Task | Depends on | Done when |
|---|---|---|---|
| 0018.1 | Migration `0030_LinkRoles.sql`: `Role`, Hebrew `StrongsNumber`, `PartLemma`, `AgentVerseProposals.Roles` ([05-data-model.md](05-data-model.md)) | – | Migration runs twice on a copy of `uw_0016_test` without error; all existing links are `head`; DBOs and repositories read/write the columns |
| 0018.2 | Save path and `GetVerseMapping` carry role, morpheme and part lemma; `SaveVerseMappingValidator` enforces the invariants of [04-roles-and-counting.md](04-roles-and-counting.md) | 0018.1 | Unit tests per invariant (Mk 1:8, Mk 1:35, Gen 1:3, `zum`); a save with two heads is rejected, with the flag accepted |
| 0018.2f | `MappingEditor`: role shown on a link (unit muted), toggle head/unit, Hebrew morpheme picker on a link, part lemma field in `WordSplitter` | 0018.2 | Component tests: toggle, morpheme choice, part lemma round-trip; check in the browser on Gen 1:3 and Mk 1:35 |
| 0018.3 | `HebrewMorphemeHints` + role rules 1–5 as pure classes in Application ([05-data-model.md](05-data-model.md)) | 0018.1 | Table-driven tests for every rule, incl. "two rules fit → undecided" |
| 0018.4 | Backfill job `link-roles`: dry-run report, apply as change set, undo | 0018.3 | Dry run on `uw_0016_test` lists counts per rule; apply + undo leave the DB byte-identical to before (checksum of the three tables) |
| 0018.4f | Maintainer page "Wartung" > "Link-Rollen": start dry run, show report and samples, apply, undo | 0018.4 | Browser check of the full cycle on the dev DB |
| 0018.5 | `AgentLemmaRules`: `PTKVZ` rules, `LegacySpellings`, contraction and pronoun headwords from [03-lemma-rules.md](03-lemma-rules.md) | – | Tests with every example row of 03 |
| 0018.6 | Stats: `DqWordFacts` + `StatsStrongsRenderings` by morpheme and rendering key, only `head`; checks `R1`–`R6` | 0018.1 | After refresh: G0615 shows no `werden`/`haben`; H0559 shows no `und`; sum of rendering counts per Strong's = aligned count (SQL test on the copy) |
| 0018.6f | Strong's page renders `sprechen (3,9xx), sagen (…)` from the new view; data-quality page lists `R1`–`R6` | 0018.6 | Browser check on H0559, G3004, G0615 |
| 0018.7 | Mapping and revision seed carry `role`, `strongs`, `partLemma`; old files import with defaults | 0018.1 | Export → import round-trip test; an old-format seed file still imports |
| 0018.8 | Run mode `lemma`: package builder (fixed links, work filter of [06-ai-runs.md](06-ai-runs.md)), answer schema, validator additions | 0018.2, 0018.5 | Unit tests with the 10 example verses; packages skip verses without work |
| 0018.8f | `AgentRunDialog` offers mode `lemma`; proposal review shows role/morpheme and lemma diffs | 0018.8 | Browser check: start a `lemma` run on Mk 1, review and merge one verse |
| 0018.9 | Prompts `greek-v2`, `hebrew-v2`, `lemma-greek-v1`, `lemma-hebrew-v1`; example verses get role answers | 0018.5, 0018.8 | Every example answer passes the validator on the copy; maintainer confirmed the role answers |
| 0018.10 | Reference set gets roles; scorer (lemma, role, flags, tokens, cost per verse) | 0016.9, 0018.8 | Scorer output for a Gemma 4 run on the reference set, stored with the run |
| 0018.10f | Run page shows the scorer result next to the run | 0018.10 | Browser check with the Gemma 4 run |
| 0018.11 | First real run: Gemma 4 on the reference set, then one book (Ruth) end to end; write `08-first-run.md` | 0018.4, 0018.9, 0018.10 | Bar of [02-decisions.md](02-decisions.md) met or prompt v2 planned; Ruth stats refreshed and `R1`–`R6` counts recorded |
