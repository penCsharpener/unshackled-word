# Roadmap 0031: Seed setup page instead of the seed container

Folder roadmap (see the `roadmap-folder` skill): open only the file you need.

| File | Content |
|---|---|
| [01-goal.md](01-goal.md) | What is wrong with the seed container today (numbers), end state, out of scope |
| [02-decisions.md](02-decisions.md) | Maintainer decisions from the interview, plan decisions, code findings |
| [03-seed-catalog.md](03-seed-catalog.md) | Seed steps, their tables, prerequisites and status rules; the library move |
| [04-setup-page.md](04-setup-page.md) | The admin page `/edit/setup`, API sketch, the fake draft |
| [05-tasks.md](05-tasks.md) | Task table with the maintainer click-through gates |
| [06-risks.md](06-risks.md) | Risks worth naming before starting |
| [07-design-0031.1.md](07-design-0031.1.md) | Design of the fake draft page (layout, files, types, i18n, tests, component proposals) |

## Status (2026-11-27)

0031.1 built (fake draft of `/edit/setup`, nothing calls the API, version 0.16.0; the dot is `StatusBadge variant="dot"` with `busy`, option A). Waiting at Gate A: the maintainer clicks through the page. Rows 0031.2 and later are not started. Implementation pauses at every gate in [05-tasks.md](05-tasks.md) until the maintainer has clicked through the page.
