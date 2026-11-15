---
name: roadmap-folder
description: Use when creating a new roadmap for this repo or adding sections to one that already is a folder (docs/roadmaps/NNNN-name/). Defines the folder format - one index.md with links only, one small file per section, tasks and risks in their own files. Older single-file roadmaps are not converted by this skill.
---

# Roadmap as a folder

New roadmaps (from 0013 on) are folders, not single files. The point is small files: a reader or a model opens only the section it needs. Older roadmaps (`0001`–`0012`, single `.md` files) stay as they are; converting them is a separate task.

Use together with `create-roadmap` (interview, slicing, task-table rules). This skill only decides where things go.

## Layout

```
docs/roadmaps/NNNN-short-name/
  index.md            links and a status section, nothing else
  01-goal.md
  02-decisions.md
  03-<topic>.md       design sections, as many as needed (form design, data model, API, migration ...)
  0N-tasks.md         the task table
  0N+1-risks.md       risks worth naming before starting
  06-first-run.md     optional: results of the real run, written after implementation
  click-dummy.html    optional: prototype, linked from index.md
```

- `NNNN` is the next free four-digit number in `docs/roadmaps/` (check the directory, numbers are skipped). `short-name` is lower-case, dashes.
- Section files are numbered in reading order. Tasks and risks come last so they can reference everything before them. Insert a new design section by renumbering the later files and fixing the links in `index.md`.
- Every file starts with `# NNNN · <section title>` (index: `# Roadmap NNNN: <title>`).

## index.md

Only a one-line note about the format, a table of `File | Content` with relative links, and a `## Status (date)` section that says what is implemented, what is not, and what deviates from the plan. No design text, no task rows. Update the status section whenever work lands.

## Sections

| File | Content | Size |
|---|---|---|
| goal | What is wrong or missing today (with real numbers from code or the dev DB), the end state, explicitly out of scope | under ~30 lines |
| decisions | What the maintainer decided (with date when it changed), consequences, findings in the code that the plan rests on | under ~60 lines |
| design sections | One topic per file (form, model, API sketch, migration). Link the click dummy. Superseded designs are rewritten, not kept side by side | under ~200 lines |
| tasks | The table from `create-roadmap`: `# | Task | Depends on | Done when`, ids `NNNN.n`, `f` suffix for the frontend row of a backend row | one file |
| risks | Bullets, each with a bold claim and two to four sentences | under ~40 lines |

If a file grows past its size, split it by topic (`03a-…`, or a new numbered file) instead of letting it grow.

## Rules

- Never put the same fact in two files; link instead. Decisions live in `decisions`, everything else refers to them.
- Task rows reference design files by relative link (`[03-form-design.md](03-form-design.md)`), not by copying.
- When the plan changes during implementation, change the file that owns the fact and add a line to the index status; do not leave contradicting text in older files.
- A click dummy is one self-contained `.html` file (mock data, no build), reviewed before implementation starts.
- Do not convert old single-file roadmaps unless the user asks for that as its own task.

## Checklist before handing over

1. `index.md` links every file in the folder and nothing else is in it.
2. Every task row has a dependency and an observable "Done when"; backend rows have their `f` row.
3. Risks name the destructive or half-done states.
4. No file is over its size guide.
