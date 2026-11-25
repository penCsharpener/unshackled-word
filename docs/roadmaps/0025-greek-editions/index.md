# Roadmap 0025: Greek editions per word

Folder roadmap (see the `roadmap-folder` skill): open only the file you need.

| File | Content |
|---|---|
| [01-goal.md](01-goal.md) | What the `Editions` column holds today (with numbers), end state, deferred items |
| [02-decisions.md](02-decisions.md) | Maintainer decisions from the interview, plan decisions, code findings |
| [03-model.md](03-model.md) | `GreekEditions` catalog, `StepGreekWordEditions` n:m table, token grammar, refresh function |
| [04-ui.md](04-ui.md) | Word detail section, marks in the Grundtext panel, edition filter |
| [05-tasks.md](05-tasks.md) | Task table (backend and frontend woven) |
| [06-risks.md](06-risks.md) | Risks worth naming before starting |

## Status (2026-11-25)

Planned, not started. Interview held on 2026-11-25; the decisions are in [02-decisions.md](02-decisions.md). The maintainer accepted the plan decisions on 2026-11-25; 0025.1 can start. The proposed component `EditionList` and token `--uw-mark-variant` are confirmed in chat with the row that builds them (0025.4f, 0025.5f).

Renumbered 2026-11-25: was roadmap 0026; swapped with morpheme alignment so this one goes first with migration 0047 and version 0.11.0.
