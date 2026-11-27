# 0031 · Setup page

## Where

Route `/edit/setup`, title "Ersteinrichtung" / "Initial setup", entry in the admin-only gear menu next to users and admin log (roadmap 0020). Admin only on the API too (`AppRoles.Admin`), since it writes base data. `/edit/imports` and "Wartung" get a one-line link to it.

## Layout

```
Ersteinrichtung                                   [ Alle ausführen ]
Stand: 27.11.2026 20:41 · 12 von 22 Schritten fertig          [ ↻ ]

Grundtext Elb1871                                 [ Gruppe ausführen ]
 ● Bibelbücher (aus Migration)       BibleBooks 66 · Languages 2
 ● STTS-Tags                         SttsTags 54
 ● Elb1871-Text                      Elb1871Words 722.743 · …        [ Ausführen ]
 ○ Elb1871-Verse   braucht: Elb1871-Text                             [ Ausführen ] (disabled)
STEP Bible (Download, braucht Internet)           [ Gruppe ausführen ]
 …
Weitere Quellen …
Abgeleitete Daten …

┌ Laufender Job: elb1871-text · seit 00:42 ───────────────────────────┐
│ log lines (last 500, as SeedJobResultView)                           │
└──────────────────────────────────────────────────────────────────────┘
```

- One row per catalog step: status dot, label, `Source` hint, tables with counts (thousands separator of the UI language), prerequisite hint when grau, the button.
- "Gruppe ausführen" and "Alle ausführen" run the red steps of the group/all groups in catalog order and stop at the first failure. Both are disabled while a job runs (anywhere: seed import and export share the lock).
- While a job runs the page polls status every 2 s and re-reads the counts when it ends. The running step shows a spinner instead of its dot.
- Empty database and not logged in: the reader home page shows "Noch keine Bibeltexte importiert." and, for an admin, a link to the page.

Components: check `Components.md` first; reuse `SeedJobResultView` for the log. A status dot is the likely only new component (`StatusDot`, variants green/amber/red/grey, design tokens only) — propose it in chat before 0031.1.

## API sketch

| Method | Route | Body / response |
|---|---|---|
| GET | `api/admin/setup` | `{ steps: [{ key, group, source, status, requires: [key], missing: [key], resumable, wired, tables: [{ name, rows, populated? }] }], job?: SeedJobResponse }` |
| POST | `api/admin/setup/run` | `{ keys: [key] }` or `{ group }` or `{ all: true }` → 202 with the job, 409 when a job runs, 400 naming a step that is not red, not wired or has a missing prerequisite |

Status of the running job comes from the existing `api/maintainer/seed/status` (same `SeedJobRunner`); the job's `operation` is `setup`, `kind` the step key or `group:<name>`/`all`. `wired: false` marks steps whose runner has not moved yet (0031.5 until 0031.8): their button says "noch nicht angebunden".

## The fake draft (0031.1)

Frontend only. `lib/setup/setupFake.ts` returns the response shape above with the dev counts of [03-seed-catalog.md](03-seed-catalog.md), starting with everything red except `books`. "Ausführen" waits 1.5–4 s per step (random), appends fake log lines every 300 ms, then sets that step's counts. A query flag `?fake=reset` starts over. The fake module sits behind the same hook (`useSetupStatus`, `useRunSetup`) that 0031.5f points at the real API, and is deleted in 0031.10.
