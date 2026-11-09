# Roadmap 0003: Mapping editor (tagging UI, priority 1)

## Goal

A page in the React app where the maintainer corrects and refines the AI mappings between Elberfelder 1871 words and
the STEP Greek/Hebrew words of one verse. Design draft: `docs/drafts/mapping-editor-draft.html`.

## Confirmed decisions (maintainer, 2026-11-09)

| Topic | Decision |
|---|---|
| User assignment | Skipped for now. |
| History / versioning | Skipped for now. A save overwrites the verse's mapping rows (`CreatedAt = now()`). |
| Granularity | One verse per request. A save replaces **all** mapping rows of the verse's German words in one transaction. Words without a link have no rows. |
| Row conventions | Same as the seed import (`AiMappingImportRunner`): a link has `StepWordId`; an added word has `IsAddedWord = true`, `ParentGermanWordId` and no `StepWordId`; `HebRefId` and `PositionInVerse` come from the German word; `GermanWordPart` stays `NULL`; Greek rows also get `StrongsNumber` (the linked STEP word's disambiguated Strong's number). The readers (wordDetail, strongsOccurrences, chapterWords) therefore keep working unchanged. |
| STEP data | Read live from the STEP tables (never copied into the repo). |

## Contract

Both routes: `/api/elberfelder/verseMapping/bookId/{bookId}/chapterId/{chapter}/verseId/{verse}`. camelCase, nulls omitted.
C# classes: `Domain.WebApi/Elberfelder/GetVerseMapping` and `SaveVerseMapping`.

`GET` (anonymous, `Cache-Control: no-store`; 404 unknown verse, 400 invalid ids):

```json
{ "bibleBookId": 43, "chapter": 1, "verse": 1, "language": "greek", "editingEnabled": false,
  "germanWords":   [{ "id": 613300, "positionInVerse": 1, "text": "Im" }],
  "originalWords": [{ "id": 50909, "position": 1, "original": "...", "transliteration": "...", "strongs": "G1722",
                      "gloss": "...", "morphologyCode": "PREP" }],
  "links": [{ "elbWordId": 613300, "stepWordId": 50909, "isAddedWord": false },
            { "elbWordId": 613303, "isAddedWord": true, "parentElbWordId": 613304 }] }
```

Defensive reading (as in `WordDetailRepository`): a parent from another verse is ignored, a `stepWordId` that is not one of
the verse's original words is dropped (the seed over-assigned some rows), added-word rows never link to an original
(they come back with the parent only), duplicates are removed.

`PUT` body `{ "links": [{ elbWordId, stepWordId?, isAddedWord, parentElbWordId? }] }` returns the saved mapping in the GET
shape. 403 while the write gate is closed, 404 unknown verse, 400 with a message per violation:
all `elbWordId`s belong to the verse; all `stepWordId`s belong to the verse's original words; `parentElbWordId` belongs
to the verse and differs from `elbWordId`; added words have a parent and no `stepWordId`; normal links have a
`stepWordId` and no parent; no duplicate `(elbWordId, stepWordId)` pair.

## Write gate (temporary)

There is no auth system yet, so writes must not be open on a public deployment. Setting `MappingEditing:Enabled`
(bool) is `false` in `appsettings.json` and must stay `false` everywhere in the repo, including
`appsettings.Development.json`. Switch it on per deployment with the environment variable
`UNSHACKLEDWORD_MAPPINGEDITING__ENABLED=true`. GET reports it as `editingEnabled`; PUT answers 403 when it is off.
The gate is read per request in `VerseMappingService`. When real login/roles arrive, replace the setting by an
authorization policy on the PUT endpoint (and drop `editingEnabled` or derive it from the caller).

## Slices

| # | Slice | Status |
|---|---|---|
| S1 | `GET verseMapping` (German words, original words, defensive links) | [x] |
| S2 | `PUT verseMapping` (FluentValidation, delete + insert in one transaction, seed conventions) | [x] |
| S3 | Write gate `MappingEditing:Enabled`, 403 when off | [x] |
| S4 | Bruno requests, unit tests (validator, service, repository SQL, PUT rejected when disabled) | [x] |
| S5 | Write path verified on a scratch database (migration project, hand-made rows, GET/PUT over HTTP) | [x] |
| S6 | React editor page against this contract (separate developer task) | [ ] |
| S7 | Replace the write gate with real auth/roles | [ ] |

## Open items

- "Let AI redo this verse range" endpoint (AI mappings are written under their own user id with timestamp).
- Confirmation state (human confirmed vs. AI proposed) per mapping row; needs a schema change.
- History/versioning of mapping changes and user assignment (explicitly postponed).
- Mapping rows have no user id yet; add together with auth.
