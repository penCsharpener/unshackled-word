# 0023 · Export and import

## Where

`{SolutionTempPath}/notes/` — in compose `./temp:/src/temp` of `unshackledword.api`, git-ignored (`temp/*`). Never `assets/`, never part of `assets/seed/manifest.json`, never run at startup.

## Format

One file per export run, `notes-YYYYMMDD-HHmmss.json` (UTF-8, LF, stable order by `Id`), the newest is the default import source:

```json
{
  "format": "unshackled-word.notes",
  "formatVersion": 1,
  "exportedAt": "2026-11-22T18:00:00Z",
  "appVersion": "0.9.0",
  "notes": [{
    "id": "8f0c…", "authorEmail": "…", "authorDisplayName": "…",
    "kind": "words", "startRef": "Röm 8,28", "bibleBookId": 45, "chapter": 8,
    "startHebRefId": 45008028, "endHebRefId": 45008028, "startPosition": 4, "endPosition": 8,
    "quotedText": "denen, die Gott lieben",
    "bodyMarkdown": "…", "visibility": "public", "publicState": "approved",
    "approvedBodyMarkdown": "…", "approvedAt": "…", "approvedByEmail": "…",
    "reviewNote": null, "hiddenAt": null, "createdAt": "…", "updatedAt": "…"
  }]
}
```

`startRef` is for humans only; import reads the ids. The user download (`api/notes/mine/export`) uses the same format with only the caller's notes.

## Import (maintainer job, `POST api/maintainer/notes/import`, body: file name)

- Upsert by `Id`: a missing note is inserted; an existing one is updated only if the file's `updatedAt` is newer (re-running the same file changes nothing).
- Author by `EmailNormalized`. Unknown author → note skipped and listed in the report (no user creation). Unknown approver → approval kept, `ApprovedByUserId` null.
- Anchor checked against the current text: words notes whose `QuotedText` no longer matches are imported anyway and show as drifted.
- Report like the seed jobs (`SeedStepReport`: inserted, updated, unchanged, skipped with reasons), shown on the "Importe" page (`/edit/imports`) as a new card "Notizen" next to the seed imports; export button on the same card.
- Notes are deleted by nothing in the import: a note deleted in the database but present in the file comes back. Stated on the card.
