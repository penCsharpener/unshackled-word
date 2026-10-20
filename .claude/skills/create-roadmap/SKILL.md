---
name: create-roadmap
description: Create or restructure an implementation roadmap in docs/roadmaps/ (numbered NNNN-<slug>.md) made of small, self-contained, test-backed steps that a smaller LLM can implement one by one. Use when the user asks to plan a feature as a roadmap, write a roadmap, add a roadmap, or restructure an existing roadmap.
---

# Create a roadmap

Roadmaps live in `docs/roadmaps/` and are implemented step by step, often by smaller models. Every step must be
small enough to finish in one session, keep the solution compiling, and come with tests. The reference example is
`docs/roadmaps/0001-mcp-server.md`; match its structure.

## Process

### 1. Pick the file name

List `docs/roadmaps/`. New roadmaps take the next four-digit number: `NNNN-<kebab-case-slug>.md`
(e.g. `0002-vocab-trainer.md`). When restructuring an existing roadmap, keep its number.

### 2. Research before writing

Read what the steps will build on, so the instructions name real files, types and columns:

- `CLAUDE.md` (layers, commands, configuration conventions),
- the SQL schema in `src/UnshackledWord.Tooling.Postgres.Migration/scripts/` for every table involved,
- one or two existing implementations of the same kind (endpoint, repository, importer, test) to copy style from,
- existing domain types that can be reused (`BibleReference`, `BibleBook`, `Result<T>`, `StrongsLanguage`, DBOs).
  Check them for bugs or mismatches with the schema; stale code becomes a fix step or a risk.
- `Directory.Packages.props` for package versions; check NuGet for new packages.

Resolve whatever you can from the code yourself (e.g. how an importer fills a column) and write the result into
the roadmap's background section.

### 3. Interview the user

Every roadmap gets an interview. Use `AskUserQuestion` (max 4 questions per call, several rounds if needed) for
every decision that changes the steps and that the code cannot answer. Typical topics:

- scope (which parts belong into this roadmap, what is explicitly out),
- hosting / transport / deployment (Docker, compose),
- where code lives (which layer/project, reuse vs. self-contained),
- test strategy (unit only, integration DB, snapshot),
- language of UI / descriptions / output,
- security (auth, read-only access),
- data placement (code vs. database table vs. config).

Put your recommended option first. Record every answer in the roadmap's **Decisions** table with the date.

### 4. Cut the work into steps

Rules for each step:

- **Small and single-responsibility:** one class, one repository method group, one tool, one formatter, one config
  change. If a step description needs "and" between two unrelated things, split it.
- **Self-contained:** after the step the solution builds (`dotnet build UnshackledWord.slnx`) and all tests pass
  (`dotnet test`). No step leaves dead references or half-registered services.
- **Tested:** every step that adds logic adds unit tests in the matching `tests/` project. Pure logic (parsers,
  formatters, builders, groupers) goes into its own class so it can be tested without a database.
- **Explicit:** name the files to create/change, the namespaces, the public signatures, the SQL (in repo style:
  quoted schema-qualified identifiers, aliases, leading commas, `WHERE 1=1`, Dapper parameters, never interpolated
  user input), sample output, and the test cases (a table of inputs → expected results where possible).
- **Ordered by dependency:** fix/extend shared domain code first, then infrastructure, then pure services, then the
  entry points (tools, endpoints, UI), then hosting and docs.
- IDs are `S01`, `S02`, … in table order.

### 5. Write the file

Use this structure:

```markdown
# Roadmap NNNN: <Title>

## Goal
<what the result enables, bullet list; what is explicitly out of scope>

## Decisions (interview YYYY-MM-DD)
| Topic | Decision |

## How to use this roadmap
<work in table order or as Depends on allows; one step = one commit; tick [ ] → [x]; read Conventions first>

## Steps
| ✔ | ID | Step | Depends on | Done when |
|---|---|---|---|---|
| [ ] | S01 | <short imperative title> | – | <observable, checkable criteria> |

## Background knowledge
<schema facts, id formats, semantics found in the code that implementers need>

## Conventions for all steps
<build/test commands, generic Definition of Done, where code goes (table), code style, templates for the recurring
kinds of classes and their tests>

## Step details
### S01 – <title>
<why, files, signatures, SQL, output sample, test cases>

## Risks
| Risk | Impact | Mitigation |

## Open questions / to verify during implementation
```

Table columns:

- **✔** – `[ ]`, ticked `[x]` when done.
- **Depends on** – IDs of steps that must be finished first, or `–`. Only real dependencies (needs the type, the
  registration, the project); this lets steps run in parallel.
- **Done when** – concrete, verifiable criteria (tests named, behaviour observable, command succeeds). Never just
  "implemented".

### 6. Risks

Always include a risks table. Think about: missing test coverage, data quality, id/versification or format
mismatches, performance and output size, breaking shared code, security, third-party API churn, licensing
(this project only uses licence-free data).

### 7. Self-check before finishing

- Every step has Depends on and Done when; no dependency points to a later-or-missing ID; no cycles.
- Every SQL column used exists in the migration scripts.
- Every file path mentioned exists (for changes) or sits in the right project (for new files).
- Each step can be implemented by reading only Conventions + Background + its own details section.
- Decisions table reflects all interview answers; open questions list what neither code nor user could answer.

Then tell the user the path, the number of steps, and the main risks in a few lines.
