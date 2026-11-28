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

## Status (2026-11-28)

0031.1 built (fake draft of `/edit/setup`, version 0.16.0). 0031.2 done by the developer (empty database). 0031.3 and 0031.3f built (version 0.16.1): `unshackledword.tooling.seed-db` removed from `compose.yml` (the fresh override never named it) and the WebApi no longer depends on it; the public read endpoints already answered 200 with empty lists or 404 on an empty database (checked against a throwaway migrated database); `SttsDriftCheck` logs one information line; new public `GET api/meta-bible/status` (`hasText`) drives the empty-database notice ("Noch keine Bibeltexte importiert.", admin-only link to `/edit/setup`) on the reader home and chapter page. The setup page still runs on the fake. Waiting at Gate B: the maintainer clicks through the empty app. Rows 0031.4 and later are not started. Until 0031.6f an empty stack has no seed path except the SeedDb CLI.
