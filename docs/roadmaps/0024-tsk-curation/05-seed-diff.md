# 0024 · Seed change file

`assets/tskxref.txt` stays the default seed, unchanged. All curation lives in one diff file.

## Format: `assets/seed/tsk/curations.csv`

Seed CSV conventions of the existing seed parts (`SeedCsv`: UTF-8 without BOM, LF, header row), sorted by `sourceRef, targetStart, targetEnd` so diffs in git stay small and reviewable.

| Column | Example | Note |
|---|---|---|
| `source` | `1Mo 1,1` | readable, ignored on import |
| `sourceRef` | `1001001` | `HebRefId` |
| `targetStart` | `20008022` | `HebRefId` |
| `targetEnd` | `20008024` | empty = single verse |
| `origin` | `tsk` / `added` | |
| `rating` | `highlight` / `hide` / empty | |
| `startPosition`, `endPosition` | `2`, `2` | empty = no word link |
| `quote` | `Anfang` | Elb1871 words at link time, for drift |
| `comment` | text | CSV-quoted, max 500 |
| `updatedAt` | `2026-11-23T14:00:00Z` | |

No user ids, emails or names (public repo). `manifest.json` gets a `tsk` part with `rows` and `sha256`, written by the export.

## Export

Maintainer job `POST api/maintainer/tsk/export` on `SeedJobRunner` (one job at a time, 202 + poll `api/maintainer/seed/status`), writes the file and the manifest part; also part of "Seed-Rundlauf" export. Button in "Wartung".

## Import

`POST api/maintainer/tsk/import` (job), button on `/edit/imports`. Rules:

- Key exists in the file and the database: the file wins if its `updatedAt` is newer; otherwise unchanged.
- Key only in the file: inserted. `origin = tsk` whose key has no `Tsk` row (counting map changed) is skipped and reported.
- Key only in the database: kept by default; option "Exakt wie Datei" deletes it (with a count shown before confirming).
- Word links are validated against `Elb1871Words` like an edit; a mismatching quote is imported and shows as drifted.
- Every applied change is written to `TskCurationAudit` with `Source = import`. Report counts `inserted / updated / unchanged / deleted / skipped`.

## Initial seed

`Tooling.SeedDb` applies `assets/seed/tsk/curations.csv` right after the TSK import, only when `TskCurations` is empty (fresh database). Later startups never touch it; updates come only from the import button, matching the rule that only the mapping seed is updated after the first start.
