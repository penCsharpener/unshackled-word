# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Free (no copyright/licence strings) Bible study data in German, Greek and Hebrew. Open-licence sources are imported into Postgres, exposed through a Web API, and used to tag/improve the Elberfelder 1871 translation (AI-tagged against STEP Bible data). See `README.md` for the roadmap and full setup.

## Direction and decisions

- **Licence:** the app and all data created with it are CC0. STEP Bible data is downloaded on the fly, never redistributed or modified; it only informs the Elb1871 revision, study tools and mappings.
- **Translations:** Elb1871 (unrevised) is the base text. Other versions (modernized literal revision first; later dynamic equivalence, easy German, children's) are not separate texts but per-version mapping tables on top of Elb1871, each with a level 1–10000 (steps of 1000 initially). The UI goal is a slider that gradually swaps wording from wooden-literal to simple. Changes are diff-based and versioned with timestamps.
- **Editing model (Linux-style):** many users suggest, only the maintainer merges. Reviewer hierarchy/roles come later. AI mappings are written under their own user id with timestamp and versioning; humans confirm. The tagging UI needs a "let AI redo this verse range" action.
- **Priorities:** 1) tagging UI to correct/refine AI mappings, 2) public reader with original-text insights, 3) study features like csv-bible.de, 4) vocab trainer (built elsewhere, merged later together with its auth system).
- **Access:** read-only study tools are public; writes require login. MCP server access policy is undecided.
- **Deployment:** target is `docker compose`. The seed runs only on initial startup; only the mapping seed may be updated afterwards. The official repo ships a rolling seed for mappings and the Elb1871 revision; self-hosters may keep their own data.
- **Scale:** expected to stay small.
- **Backend:** .NET 10 (upgrade to 11 later is simple), FastEndpoints, Serilog, FluentValidation, Postgres 18+, Dapper only. EF Core (old BibleTagger) is obsolete. Raw SQL follows the project's SQL-formatting skill for raw string literals.
- **Frontend (new, replaces Astro/Svelte/Next.js/Blazor experiments, which are reference for functionality only; the Bible chapter picker design is worth porting):** React 19+ SPA, TypeScript 7+, Vite 8 (Rolldown), Tailwind v4, Bun 1.4+, Biome. Clean modern layout, light/dark mode, mobile through 4k. UI in German and English via i18n.
- **Frontend conventions:** develop only inside Docker containers (no host `node_modules`). Use design tokens only, no hardcoded colors/spacing/font sizes. Check `Components.md` before building and register every new component there. Propose new components in chat first; prefer a prop/variant over a new component. Never add a dependency without asking. Run type check and lint before finishing. Planned: Biome rules against arbitrary Tailwind values, inline styles and raw `<button>`, plus a CI script scanning for banned patterns and duplicate component names.

## Commands

.NET 10 solution (`UnshackledWord.slnx`); the `.ps1` scripts in the root are thin `dotnet run/watch` wrappers.

```sh
docker run --name unshackledword.database -e POSTGRES_PASSWORD=test -p 5432:5432 -d postgres:18   # or: docker compose up -d (full stack; needs .env, see README)
dotnet run --project src/UnshackledWord.Tooling.Postgres.Migration   # 1. schema (run-db-migration.ps1)
dotnet run --project src/UnshackledWord.Tooling.SeedDb               # 2. download + import data (run-db-setup.ps1); downloads cached in ./temp
dotnet watch run --project src/UnshackledWord.Tooling.WebApi         # 3. API (run-api.ps1)
dotnet watch run --project src/UnshackledWord.Tooling.BibleTagger    # Blazor tagging UI (run-web.ps1)
dotnet build UnshackledWord.slnx
dotnet test                                                          # tests use Microsoft Testing Platform (see tests/Directory.Build.props)
dotnet test --project tests/UnshackledWord.Domain.Tests              # single project
dotnet test --project tests/UnshackledWord.Domain.Tests --filter-method "*Name*"   # single test (MTP filter syntax; .NET 10 SDK)
```

The new frontend is `src/UnshackledWord.Web` (React/Vite/Bun/Tailwind/Biome/Vitest, not in the `.slnx`). It runs only in Docker (service `unshackledword.web`, port 5173, `node_modules` in a named volume). Run its commands inside the container, e.g. `docker compose run --rm --no-deps unshackledword.web bun run check` (typecheck + lint + `check:components` + tests); `bun run format` auto-fixes. Dev server: `docker compose up unshackledword.web`. API base URL comes from `VITE_API_URL`. Components live flat in `src/components` and must be listed in `Components.md`.

Old frontends (reference only, each independent, in `src/`): `UnshackledWord.AstroWeb` (`npm install && npm run dev`, needs `.env` with `PUBLIC_API_DOMAIN=http://localhost:5142/api`, Biome for lint), `UnshackledWord.SvelteWeb` (bun), `UnshackledWord.ElbTagger.WebApp` (Next.js). Only AstroWeb is documented in README; the other two are not in the `.slnx`.

EF migrations for BibleTagger (run in `src/UnshackledWord.Tooling.BibleTagger`): `dotnet ef migrations add <Name> --output-dir Data/Migrations`; the generated idempotent script is copied manually into `UnshackledWord.Tooling.Postgres.Migration/scripts/` (see README).

`api/unshackled-word/` is a Bruno collection for hitting the Web API.

## Architecture

Layered projects under `src/`:

- `Domain` – models (`Dbo/`, `Dbo/Step/`, `BibleStructure/`, `Grammar/`, `Dto/`, `Settings/`). `Domain.WebApi` – request/response contracts shared by API and BibleTagger client, grouped per feature.
- `Application` – abstractions (repository/importer interfaces, `Abstractions/Step`), repositories interfaces, `Features/` (e.g. Backup).
- `Infrastructure` – repository implementations and services. `Persistence.Postgres` – Npgsql/connection setup.
- `Tooling.Postgres.Migration` – runs the numbered raw SQL in `scripts/` (`0001_…sql` etc.) to create schema/tables; add new schema changes as a new numbered script.
- `Tooling.SeedDb` – large importer pipeline; one folder per data source under `Services/` (StepBible + LexiconParser, Tsk, SBL, StatisticalRestorationGnt, ByzTxt, GlobalBibleTools, ElberfelderParser, Elb1871Lemmatizer, Elb1871WordsSrGntWordsMapper, AiMappingImport…). Reads static inputs from `assets/` (Elberfelder export, per-book mapping CSVs in `assets/Elb1871Mappings`, globalbibletools CSVs, TSK). `SeedDb.TestRunner` is a scratch console for exercising importers.
- `Tooling.AiWorker` – Gemini-based worker that produces the Elberfelder ↔ STEP/Greek/Hebrew word mappings and morphology.
- `Tooling.WebApi` – FastEndpoints API (route prefix `api`, camelCase JSON, nulls omitted); one endpoint folder per feature under `Endpoints/` (BibleTagger, Elberfelder, MetaBible). Swagger/OpenAPI enabled in Development with open CORS.
- `Tooling.BibleTagger` – Blazor app (with ASP.NET Identity account pages) that talks to the Web API (`UNSHACKLEDWORD_APIURL`) and has its own EF Core `Data/Migrations`.

Tests in `tests/` mirror the layers (Application, Domain, Infrastructure, Persistence.Sqlite, SeedDb); `TestTooling` holds shared helpers.

## Configuration conventions

- Package versions are centralized in `Directory.Packages.props`; shared build settings (net10.0, nullable, implicit usings) in `Directory.Build.props`, which optionally imports a git-ignored `Directory.Build.local.props`.
- Every executable project has a `partial class Program` with `static partial void AddLocalSecrets(...)`. Implement it in a git-ignored `Program.local.cs` (any `*.local.cs` is ignored) to load an out-of-repo secrets JSON (connection string `ConnectionStrings:PostgresConnection`, and for SeedDb absolute `AppSettings:DatabaseSeeding:SolutionTempPath` / `SolutionAssetsPath`). In Docker, config comes from env vars prefixed `UNSHACKLEDWORD_` (double underscore for nesting).
- `.editorconfig`: LF line endings, 4-space C#.
