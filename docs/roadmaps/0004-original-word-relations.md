# Roadmap 0004: Relations between original words (article → noun, pronoun → head)

## Goal

The mapping editor (roadmap 0003, `src/UnshackledWord.Web/src/components/MappingEditor.tsx`) can connect a German
word to original words, and a German word to another German word ("added word"). It cannot say that an **original
word belongs to another original word**. Example Rev 1:1: ὁ (article, no German counterpart) belongs to θεός
("Gott"); τοῖς → δούλοις, τοῦ → ἀγγέλου, αὐτοῦ → δούλῳ.

This roadmap adds:

- a STEP-derived baseline of such relations for Greek (parsed from `StepGreekWords."ConjoinWord"` at read time,
  never copied or modified),
- our own CC0 overrides on top of it (attach to another head, detach), for Greek and Hebrew,
- the relations in `GET/PUT verseMapping`, an "attach" interaction in the editor, and later in wordDetail,
  strongsOccurrences, the rolling seed and MCP.

Out of scope: user assignment / history (decided in 0003, 2026-11-09), changing any existing mapping row, AI
generation of relations, cross-verse relations in the editor, relation kinds beyond what morphology tells us.

## Decisions

| Topic | Decision | Source |
|---|---|---|
| Existing mapping rows | Never touched by this roadmap: no `UPDATE`/`DELETE` on `Elb1871GreekMapping` / `Elb1871HebrewMapping` in any migration; editor saves keep 0003 semantics | task brief, 2026-11-10 |
| STEP data | `ConjoinWord` is parsed at read time in C#; no STEP-derived rows are stored (see "Option comparison") | this roadmap (recommendation, maintainer to confirm) |
| Our data | Only overrides are stored, in two new tables; they are CC0 and go into the rolling seed | this roadmap |
| User / history | None now; table has `CreatedAt` and leaves room for `CreatedBy`/`ConfirmedAt` like 0002 S29 | 0003 decision |
| Write gate | `MappingEditing:Enabled` unchanged; PUT stays 403 while it is off | 0003 |
| Migration number | `0008_OriginalWordRelations.sql`. `0007` stays reserved for `0007_MappingProvenance.sql` (0002 S29). Both are independent, so order of shipping does not matter (DbUp journals by name) | 0002 S29 |
| Granularity | One verse per request, as in 0003 | 0003 |
| Head cardinality | An original word has at most **one** head (a forest per verse); a head may have many dependents | this roadmap |

## How to use this roadmap

Work in table order or as *Depends on* allows; one step = one commit; tick `[ ]` → `[x]`. Read "Background" and
"Conventions" first. Every slice runs end to end (backend + frontend where it has a UI part) and keeps
`dotnet build UnshackledWord.slnx`, `dotnet test` and `bun run check` (in the container) green. A UI draft for the
editor interaction (S04) will follow as `docs/drafts/word-relations-draft.html`; S04 waits for it.

## Steps

| ✔ | ID | Step | Depends on | Done when |
|---|---|---|---|---|
| [ ] | S01 | `StepConjoinWordParser` + `OriginalWordHeadResolver` (pure, Domain/Application) and STEP heads in `GET verseMapping` (`originalWords[].headId`, `headSource: "step"`); editor shows attached words read-only and no longer counts an attached article as unmapped | – | Parser + resolver tests (table below) pass; `VerseMappingServiceTests` cover Rev 1:1; `GET .../66/1/1` returns `headId` on ὁ, τοῖς, αὐτοῦ…; Vitest: attached ὁ shows under θεός with status `attached`; `bun run check` green |
| [ ] | S02 | Migration `0008_OriginalWordRelations.sql` (two override tables, no change to existing tables) + verification run on a scratch DB | – | Script runs on fresh and on populated scratch DB, runs twice without error; before/after counts and checksums of both mapping tables identical (queries below); DbUp journal shows `0008` |
| [ ] | S03 | Read overrides in `GET verseMapping` (`headSource: "manual"`, `stepHeadId` when STEP differs, detached words) | S01, S02 | Repository SQL test (FakeDbReader) + service tests: STEP only, manual re-attach, manual detach, override equal to STEP ignored, override pointing outside the verse dropped |
| [ ] | S04 | `PUT verseMapping` accepts `heads` (full effective head map, `null` = untouched); server stores the diff to STEP; editor "attach" interaction (drag o→o / select-then-tap / attach mode), undo, diff count | S03, UI draft | Validator tests (cycle, self, foreign id, duplicate, >1 head); service test: PUT effective map equal to STEP writes 0 rows; old body without `heads` leaves overrides untouched; Vitest for `attach`/`detach`/`computeGroups`; manual PUT on scratch DB |
| [ ] | S05 | Rolling seed: export overrides to `assets/Elb1871WordRelations.csv` in the backup, import runner `WordRelationImportRunner` (count-guarded, position + text matching) | S02 | Runner tests with in-memory rows (match, text mismatch skipped + logged, detach row); fresh scratch DB seeds the CSV; second run does nothing |
| [ ] | S06 | wordDetail: head words carry their dependents (`ὁ θεός` in the original line, article muted) | S03 | `WordDetailServiceTests` for Rev 1:1 "Gott"; Vitest for popover rendering; contract fixture updated |
| [ ] | S07 | strongsOccurrences: a dependent without own German word highlights the German word(s) of its head (KWIC for G3588, H0853) | S03 | Service test: G3588 in Rev 1:1 highlights "Gott"; flag `viaHead` in the row; Vitest for the KWIC marker |
| [ ] | S08 | MCP `OriginalTextTools`: heads in the original-text tool output | S03 | MCP tool test shows `ὁ→θεός` for Rev 1:1 |
| [ ] | S09 | Report query/endpoint "articles mapped to the same German word as their head" (AI double mapping) for manual review; no automatic cleanup | S01 | Query documented and tested on scratch DB; count reported to maintainer |

Ordering: S01 and S02 can run in parallel. S04 is the first slice with writes. S05–S09 are independent of each other.

## Background (verified read-only on the DB copy, 2026-11-10)

### STEP `ConjoinWord`

- Greek (`StepGreekWords`, 142,096 rows): every row has a value. Three shapes only:
  `#NN` (101,047 rows, no relation), `#NN»MM:Gxxxx` (28,874, head is later) and `#NN«MM:Gxxxx` (12,175, head is
  earlier). `NN` is STEP's own word number, `MM` the head's number, `Gxxxx` the head's Strong's number
  (with optional letter suffix, e.g. `G0032`).
- By dependent part of speech (morphology prefix): `T` » 20,592, `P` « 9,252, `PRT` » 2,547, `P` » 2,146, `R` » 1,212,
  `D` «/» 838/834, `CONJ` «/» 812/335, `I` » 499, … So it covers articles, possessive pronouns, relative pronouns,
  particles, not only articles.
- `NN` equals `PositionInVerse` for all but 13 rows (e.g. John 16:13 positions 25–29 carry `#27`–`#31`). Resolving
  the head as `PositionInVerse + (MM − NN)` in the **same `LxxRefId`**: 40,516 of 41,049 relations hit a word with
  the same 5-character Strong's base; 402 more resolve to the nearest word of that Strong's base in the verse;
  131 stay unresolved (shown as no head). 194 rows point at themselves (`#08»08`), mostly resolvable by the
  Strong's fallback. 325 heads are themselves dependents (chains, e.g. article → adjective → noun).
- No relation points to another verse (the format has no verse part).
- Articles: 20,754 words with `Morphology LIKE 'T-%'`, 20,668 of them have a relation; 8,920 have no German mapping
  row, 8,897 of these have a STEP relation; resolved by position, 8,894 unmapped articles have a head and for 8,466
  of them the head has a German mapping, so attaching makes them "covered".
- Only 121 STEP relations (of 41,038 resolved by position) have dependent and head mapped to the **same** German
  word, i.e. the AI rarely encoded the relation through the German side (option D); S09 lists them.
- Other observations (not explained yet, not in scope): 53 Greek mapping rows have a parent from another verse;
  150 Greek and 40,624 Hebrew mapping rows link a STEP word whose `LxxRefId` differs from the row's `HebRefId`
  (for Hebrew probably versification, `HebRefId` vs `LxxRefId`; to verify before S07 uses verse joins).
- Parsing all relations of the NT in SQL takes ~3 s; a single verse is an index scan on
  `IX_StepGreekWords_Book_Chapter_Verse` (0.03 ms). Read-time parsing costs nothing measurable.

### Hebrew

- `StepHebrewWords."ConjoinWord"` is **empty in all 305,652 rows** (column exists, importer leaves it empty or TAHOT
  has no such field). There is no STEP baseline for Hebrew.
- The Hebrew article is a prefix inside the word (`H9009` in `DisambiguatedStrongs`, `Td` in `Grammar`), so the
  "article without German word" problem is mostly a Greek one. The Hebrew analogue is the object marker אֵת
  (`Grammar = 'HTo'`): 3,904 of the 11,378 Hebrew words without German mapping. Construct chains (114,823 words
  with construct morphology) are a later candidate.
- Hebrew relations are therefore **manual only**; the same table shape is used.

### Existing mapping data (must be preserved)

- `Elb1871GreekMapping` 170,071 rows, `Elb1871HebrewMapping` 556,416 rows, `Elb1871Words` 722,753 rows,
  13,579 Greek and 11,378 Hebrew words have no mapping row.
- Quality notes from 0002 "AI mapping data": over-assigned rows (Joh 18:19 `Der` → Ὁ, τὸν, τῶν, τῆς), added-word
  rows that name an original, cross-verse parents. The readers already filter defensively
  (`VerseMappingService.ToLinks`, `WordDetailRepository`).
- Step ids are `SERIAL` (`StepGreekWords."Id"`, `StepHebrewWords."Id"`): they depend on import order. The rolling
  seed therefore matches by `HebRefId` + position + word text (`AiMappingImportRunner`, CSV columns
  `HebRefId,PositionInVerse,…,StepWord,StepPositionInVerse,…`). The relation seed must do the same.
- `AiMappingImportRunner` skips completely when either mapping table has rows; "updating the mapping seed" on a
  populated DB is not implemented today. The relation importer follows the same guard (see open question 4).

### Code touch points

- `src/UnshackledWord.Infrastructure/Repositories/VerseMappingRepository.cs` (`VerseMappingSql.GreekWords` /
  `HebrewWords` select original words by `LxxRefId IN (… Elb1871Words.HebRefId …)` – a German verse can span several
  STEP verses, so positions are unique only per `LxxRefId`).
- `src/UnshackledWord.Application/Features/VerseMapping/VerseMappingService.cs`, `SaveVerseMappingValidator.cs`.
- `src/UnshackledWord.Domain/Models/Study/VerseMappingRows.cs`, `src/UnshackledWord.Domain.WebApi/Elberfelder/GetVerseMapping/*`, `SaveVerseMapping/*`.
- Frontend: `src/UnshackledWord.Web/src/lib/bible/types.ts` (`VerseMappingOriginalWord`), `lib/bible/mappingEditor.ts`
  (`MappingState { edges, added }`, `computeGroups`, `wordStatus`, `diffStates`), `components/MappingEditor.tsx`
  (o→o drops are rejected today in the drop-target check, line ~253; "added mode" is the model for "attach mode").
- Seed: `src/UnshackledWord.Tooling.SeedDb/Services/AiMappingImport/*`, `SeedingService.cs`; backup export:
  `src/UnshackledWord.Infrastructure/Repositories/ElbDashboardRepository.cs` (`CreateBackupAsync`),
  `src/UnshackledWord.Tooling.WebApi/Endpoints/BibleTagger/BackupElbData/Mappings/BackupFileService.cs`.
- Migrations: DbUp (`dbup-postgresql`) runs `scripts/*.sql` by name and journals them; existing: 0001, 0002, 0004,
  0005, 0006 (0003 is a gap).

## Option comparison (data model)

| Option | How | For | Against | Verdict |
|---|---|---|---|---|
| A. Override tables + STEP parsed at read time | New tables hold only our decisions (attach to X / detach); STEP baseline comes from `ConjoinWord` per request | STEP untouched and never copied; one source of truth; tiny tables; re-imported STEP data is picked up automatically; works for Hebrew (overrides only) | Parser logic in C# (testable); corpus-wide queries must parse too (cheap, measured) | **Recommended** |
| B. Parsed baseline table at seed time + overrides | SeedDb writes `StepGreekWordRelation` rows from `ConjoinWord`; overrides on top | Plain joins for corpus queries | A second copy of STEP-derived data that goes stale on STEP re-import; must be kept out of the CC0 seed and backups; extra seed step on every self-hosted DB; mixes provenance | Rejected (revisit only if a corpus query gets slow) |
| C. Extend mapping tables (`HeadStepWordId` on `Elb1871GreekMapping`) | A relation is a column on a mapping row | No new table | Mapping rows are per German word; an article without German word has no row → would need rows with `ElbWordId` NULL (violates `NOT NULL`, `UNIQUE NULLS NOT DISTINCT`), breaks wordDetail/strongsOccurrences/chapterWords counting and the seed CSV shape; touches 726k rows | Rejected |
| D. Through the German side | Map "Gott" to ὁ **and** θεός | Works today, no schema change | Mixes "translated by" with "grammatically belongs to"; G3588 occurrences would list "Gott" as a rendering; inflates the over-assignment signal; cannot express a relation when the head has no German word either; ignores the STEP baseline | Rejected as model; existing rows of this kind stay as they are (S09 reports them) |

### Recommended schema (`0008_OriginalWordRelations.sql`)

Two tables, one per language, like the mapping tables (the step word ids point to different tables).

```sql
CREATE TABLE IF NOT EXISTS "unshackled-word"."GreekWordRelationOverride"
(
    "Id"                 SERIAL PRIMARY KEY,
    "DependentStepWordId" INT                      NOT NULL,  -- StepGreekWords."Id" of the attached word (ὁ)
    "HeadStepWordId"      INT                      NULL,      -- StepGreekWords."Id" of the head (θεός); NULL = detached (STEP relation suppressed)
    "LxxRefId"            INT                      NOT NULL,  -- verse of the dependent, for per-verse reads and the seed export
    "Source"              VARCHAR(10)              NOT NULL DEFAULT 'manual',
    "CreatedAt"           TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now(),
    CONSTRAINT "UqGreekWordRelationOverrideDependent" UNIQUE ("DependentStepWordId"),
    CONSTRAINT "CkGreekWordRelationOverrideNotSelf" CHECK ("HeadStepWordId" IS NULL OR "HeadStepWordId" <> "DependentStepWordId"),
    CONSTRAINT "CkGreekWordRelationOverrideSource" CHECK ("Source" IN ('manual', 'ai'))
);

CREATE INDEX IF NOT EXISTS "IdxGreekWordRelationOverrideLxxRefId"
    ON "unshackled-word"."GreekWordRelationOverride" ("LxxRefId");
CREATE INDEX IF NOT EXISTS "IdxGreekWordRelationOverrideHead"
    ON "unshackled-word"."GreekWordRelationOverride" ("HeadStepWordId");

-- "HebrewWordRelationOverride": identical, ids point to StepHebrewWords."Id".
```

Semantics, per original word *w* of a verse:

1. If an override row for *w* exists, it decides: `HeadStepWordId` = head, or `NULL` = no head.
2. Otherwise the STEP head from `ConjoinWord` (Greek only), if it resolves inside the verse's original words.
3. Otherwise no head.

Notes:

- **Direction** is not stored: a relation always reads "dependent → head"; STEP's `»`/`«` only says where the head
  is and is implied by positions.
- **Kind** is not stored in v1: it is derived from the dependent's morphology at read time (`article` for `T-`,
  `pronoun` for `P-`/`R-`/`D-`/`F-`/`I-`/`X-`/`K-`/`C-`/`S-`, `particle` for `PRT`, `other`); Hebrew from `Grammar`
  (`To` = object marker). Open question 2 decides whether the maintainer needs a stored kind (e.g. apposition).
- **No foreign keys** to `StepGreekWords`/`StepHebrewWords`, consistent with the mapping tables (STEP tables are
  reloaded by the seeder; an FK would block that). Readers drop rows whose ids are not in the verse.
- **Cross-verse:** the schema does not forbid it (`HeadStepWordId` is any step id), but STEP has none and the API
  rejects heads outside the verse's original words in v1. `LxxRefId` is the dependent's verse.
- **One head per word:** `UNIQUE ("DependentStepWordId")`. Cycles are prevented by the validator, and the reader
  breaks any cycle it still finds (drops the override that closes it).
- **Article without German word:** stays without mapping row (as today). It is "covered" through its head: the
  verse mapping is still correct because the German side does not change; the UI shows the article attached to θεός
  and status `attached` instead of `none`. If the AI already mapped "Gott" → ὁ as well, that row is kept.
- **Provenance later:** `Source` allows `'ai'`; `CreatedBy`, `ConfirmedBy`, `ConfirmedAt` can be added with the same
  migration pattern as 0002 S29 (nullable/defaulted columns, no rewrite).
- Size: overrides only, expected in the hundreds to low thousands of rows.

## Migration plan

1. **Before** (on the target DB, read-only): run the verification queries below, keep the output.
2. Apply `0008_OriginalWordRelations.sql` via the migration project
   (`dotnet run --project src/UnshackledWord.Tooling.Postgres.Migration`, or the compose service). It only creates
   two tables and two indexes each, all `IF NOT EXISTS`; no existing table is altered, so a populated self-hosted
   DB is not touched and the script is idempotent even outside DbUp.
3. **After:** run the verification queries again; counts and checksums must be identical; the new tables exist and
   are empty.
4. **Backfill:** none. The STEP baseline is read time (option A). Our overrides start empty, or come from the
   relation seed CSV on a fresh DB (S05).
5. **Over-assigned rows:** untouched. The editor makes them easier to fix (attach the article, remove the extra link);
   S09 lists candidate rows. No automatic cleanup.
6. **Rolling seed / self-hosters:** a fresh DB gets mappings (existing runner) and then relation overrides
   (`WordRelationImportRunner`, after `AiMappingImportRunner` in `SeedingService`). An existing DB keeps its data;
   the new runner only imports when both override tables are empty.
7. **Rollback:** not a numbered script (DbUp is forward-only). Manual, after exporting the overrides with the backup
   endpoint: `DROP TABLE IF EXISTS "unshackled-word"."GreekWordRelationOverride", "unshackled-word"."HebrewWordRelationOverride";`
   and `DELETE FROM "schemaversions" WHERE "scriptname" LIKE '%0008_OriginalWordRelations.sql';` (check the journal
   table/column names on the target first). Mapping data is unaffected either way.

### Verification queries (before and after, must match)

```sql
SELECT 'greek' AS "Table"
     , count(*) AS "Rows"
     , md5(string_agg(concat_ws('|', egm."Id", egm."ElbWordId", egm."StepWordId", egm."HebRefId", egm."StrongsNumber"
                               , egm."IsAddedWord", egm."ParentGermanWordId", egm."PositionInVerse", egm."GermanWordPart"
                               , egm."CreatedAt"), ',' ORDER BY egm."Id")) AS "Checksum"
FROM "unshackled-word"."Elb1871GreekMapping" egm
UNION ALL
SELECT 'hebrew'
     , count(*)
     , md5(string_agg(concat_ws('|', ehm."Id", ehm."ElbWordId", ehm."StepWordId", ehm."HebRefId", ehm."IsAddedWord"
                               , ehm."ParentGermanWordId", ehm."PositionInVerse", ehm."GermanWordPart", ehm."CreatedAt")
                     , ',' ORDER BY ehm."Id"))
FROM "unshackled-word"."Elb1871HebrewMapping" ehm
UNION ALL
SELECT 'step-greek-conjoin'
     , count(*)
     , md5(string_agg(concat_ws('|', sgw."Id", sgw."ConjoinWord"), ',' ORDER BY sgw."Id"))
FROM "unshackled-word"."StepGreekWords" sgw;
```

Expected today on the DB copy: greek 170,071, hebrew 556,416, step-greek-conjoin 142,096 rows. `string_agg` over
556k short rows needs a few seconds and some MB of work memory; run it outside peak use. Additionally after the
migration:

```sql
SELECT count(*) FROM "unshackled-word"."GreekWordRelationOverride";   -- 0
SELECT count(*) FROM "unshackled-word"."HebrewWordRelationOverride";  -- 0
```

## API contract

Route unchanged: `/api/elberfelder/verseMapping/bookId/{bookId}/chapterId/{chapter}/verseId/{verse}`.

### GET (additive, old clients keep working)

`originalWords[]` gains optional fields (omitted when null):

| Field | Meaning |
|---|---|
| `headId` | effective head (`StepWordId` of another original word of this verse) |
| `headSource` | `"step"` or `"manual"` (`"ai"` later) |
| `stepHeadId` | STEP's head when an override differs from it (attached elsewhere or detached), so the UI can offer "restore STEP" |
| `relationKind` | `"article"`, `"pronoun"`, `"particle"`, `"objectMarker"`, `"other"` (derived from morphology) |

```json
"originalWords": [
  { "id": 131918, "position": 7, "original": "ὁ", "strongs": "G3588", "morphologyCode": "T-NSM",
    "headId": 131919, "headSource": "step", "relationKind": "article" },
  { "id": 131919, "position": 8, "original": "θεὸς", "strongs": "G2316", "morphologyCode": "N-NSM-T" }
]
```

A detached word: no `headId`, `headSource: "manual"`, `stepHeadId: 131919`.

### PUT

Body gains `heads` next to `links`:

```json
{ "links": [ ... unchanged ... ],
  "heads": [ { "stepWordId": 131918, "headStepWordId": 131919 }, { "stepWordId": 131921, "headStepWordId": 131922 } ] }
```

- `heads` is the **complete effective head map** of the verse (every original word that should have a head).
  `null`/absent = relations untouched (old clients, Bruno request `SaveVerseMapping.bru`); `[]` = no word has a head
  (this writes detach rows for every STEP relation of the verse).
- The server computes the diff against the STEP baseline: head equal to STEP → no row; different → override row with
  the head; STEP head but word missing in `heads` → override row with `HeadStepWordId NULL`. All override rows of the
  verse's original words are replaced in the same transaction as the links (`ReplaceMappingAsync` extended, or a
  second repository call inside the same transaction).
- Validation (400, one message per violation): `stepWordId` and `headStepWordId` belong to the verse's original words;
  not equal; no duplicate `stepWordId`; no cycle (A→B→A, also longer). Links rules from 0003 unchanged.
- 403 while `MappingEditing:Enabled` is off (unchanged), 404 unknown verse. Response = GET shape.

### Other consumers

| Consumer | Impact | Slice |
|---|---|---|
| `GET wordDetail` (`WordDetailService`) | Original line of a German word shows head + dependents in verse order (`ὁ θεός`), dependents muted; no change to `mapping`/`coMappedWords` | S06 |
| `GET strongsOccurrences` (KWIC) | For a dependent without own mapping row, the matched German positions are those of its head; row flag `viaHead: true` | S07 |
| MCP (`src/UnshackledWord.Tooling.McpServer/Tools/OriginalTextTools.cs`) | Optional `head` per word in the original-text output | S08 |
| AiWorker (`Mapping/Greek/GreekMappingService.cs`, Hebrew) | None now. Later: pass STEP heads in the prompt so Gemini does not map articles separately (open question 5) | – |
| Seed (`AiMappingImportRunner`, mapping CSVs) | Unchanged. New CSV + runner for overrides | S05 |
| BibleTagger (Blazor, obsolete) | None | – |

## Editor UX (outline; UI draft follows)

New interaction "attach original word to original word", built like the existing "added word" mode:

- Drag an original tile onto another original tile (today rejected), or select an original word, then tap
  "Anhängen an…" / "Attach to…" and tap the head. Keyboard: same as select-then-tap.
- An attached word is drawn as a small dependent tile under/next to its head, joined by a short connector, and is
  part of the head's group (`computeGroups` unions dependent and head). Status `attached` (not `none`), so 8,897
  articles no longer look unmapped.
- Info panel of an attached word: "gehört zu θεός (STEP)" / "(manuell)", actions "Lösen" (detach) and, if overridden,
  "STEP-Zuordnung wiederherstellen".
- Undo and the change counter include head changes; the diff lists "attached ὁ → θεός", "detached αὐτοῦ".

```
Original:   [Ἀποκάλυψις] [Ἰησοῦ] [Χριστοῦ] [ἣν→] [ἔδωκεν] [←αὐτῷ]  [θεὸς]   [δεῖξαι]  [δούλοις]
                                                                   ╰ ὁ      (STEP)    ╰ τοῖς  ╰ αὐτοῦ
Deutsch:    Offenbarung  Jesu  Christi, die  ihm   Gott  gab, ...   (Gott ↔ θεὸς; ὁ covered via θεὸς)
```

TS contract (`src/UnshackledWord.Web/src/lib/bible/types.ts`):

```ts
export type RelationSource = "step" | "manual" | "ai";
export type RelationKind = "article" | "pronoun" | "particle" | "objectMarker" | "other";
export interface VerseMappingOriginalWord {
  // existing fields …
  headId?: number;
  headSource?: RelationSource;
  stepHeadId?: number;
  relationKind?: RelationKind;
}
export interface VerseMappingHead { stepWordId: number; headStepWordId: number; }
export interface SaveVerseMappingBody { links: VerseMappingLink[]; heads?: VerseMappingHead[]; }
```

`MappingState` (`lib/bible/mappingEditor.ts`) gains `heads: ReadonlyMap<number, number>` (dependent → head) with
pure `attach`, `detach`, `headsToState`, `stateToHeads`; `computeGroups`, `wordStatus` and `diffStates` include it.

## Conventions for all steps

- Backend: Dapper only, raw SQL as `const string` in the `*Sql` static class next to the repository, repo SQL style
  (quoted schema-qualified identifiers, aliases, leading commas, `WHERE 1=1`, Dapper parameters). Pure logic in its own
  class with unit tests; repository tests with `tests/UnshackledWord.TestTooling/FakeDbReader.cs`
  (see `tests/UnshackledWord.Infrastructure.Tests/VerseMappingRepositoryTests.cs`); service tests in
  `tests/UnshackledWord.Application.Tests/Features/VerseMappingServiceTests.cs`.
- Frontend: inside Docker only (`docker compose run --rm --no-deps unshackledword.web bun run check`); design tokens
  only; new strings in German and English i18n files; check and update `Components.md` (no new component without
  asking; prefer a variant of the existing word tile).
- DB checks: never write to the shared DB copy. Migration tests run on a scratch database in the local
  `postgres-test-db` container (`docker/postgres-test-db/docker-compose.yml`).
- Bruno: update `api/unshackled-word/Elberfelder/GetVerseMapping.bru` / `SaveVerseMapping.bru` in the slice that
  changes the contract.

## Step details

### S01 – STEP heads in GET verseMapping (Sonnet-ready)

Why: shows the existing STEP relations without any DB change and fixes the "unmapped article" impression.

Files:

| File | Change |
|---|---|
| `src/UnshackledWord.Domain/Models/Study/StepConjoinWord.cs` (new) | `public readonly record struct StepConjoinWord(int Number, int? HeadNumber, string? HeadStrongs)` |
| `src/UnshackledWord.Domain/Models/Study/StepConjoinWordParser.cs` (new) | `public static StepConjoinWord? Parse(string? value)` |
| `src/UnshackledWord.Application/Features/VerseMapping/OriginalWordHeadResolver.cs` (new) | `public static IReadOnlyDictionary<int, int> ResolveStepHeads(IReadOnlyList<VerseOriginalWordRow> words)` (dependent id → head id) and `public static string? RelationKind(string? morphologyCode, bool hebrew)` |
| `src/UnshackledWord.Domain/Models/Study/VerseMappingRows.cs` | `VerseOriginalWordRow` gains `int LxxRefId`, `string? ConjoinWord` |
| `src/UnshackledWord.Infrastructure/Repositories/VerseMappingRepository.cs` | `VerseMappingSql.GreekWords` adds `, sgw."LxxRefId"` and `, sgw."ConjoinWord"`; `HebrewWords` adds `, shw."LxxRefId"` and `, NULL::varchar "ConjoinWord"` (Hebrew is empty; avoid reading it) |
| `src/UnshackledWord.Domain.WebApi/Elberfelder/GetVerseMapping/GetVerseMappingResponse.cs` | `VerseMappingOriginalWord` gains `int? HeadId`, `string? HeadSource`, `int? StepHeadId`, `string? RelationKind` |
| `src/UnshackledWord.Application/Features/VerseMapping/VerseMappingService.cs` | `BuildResponse` fills `HeadId`/`HeadSource = "step"`/`RelationKind` from the resolver |
| `src/UnshackledWord.Web/src/lib/bible/types.ts` | fields from the TS contract (GET part only) |
| `src/UnshackledWord.Web/src/lib/bible/mappingEditor.ts` | `WordStatus` gains `"attached"`; `wordStatus` takes an optional `heads` map; `computeGroups` takes optional `heads` and unions them |
| `src/UnshackledWord.Web/src/components/MappingEditor.tsx` | read-only: attached tile shown with its head (existing tile variant, token-based), info line "gehört zu X (STEP)"; i18n keys `bible.mapping.status.attached`, `bible.mapping.info.attachedTo` (de + en) |

Parser rules (`^#(\d+)(?:([«»])(\d+):(G\d+[A-Z]?))?$`; anything else → `null`):

| Input | Result |
|---|---|
| `#07»08:G2316` | `(7, 8, "G2316")` |
| `#12«11:G1401` | `(12, 11, "G1401")` |
| `#01` | `(1, null, null)` |
| `#23»23:G0032G` | `(23, 23, "G0032G")` (self; resolver handles it) |
| `null`, `""`, `"abc"`, `#x»1:G1` | `null` |

Resolver rules (per `LxxRefId` group, positions unique per group):

1. Skip words whose parse is `null` or has no `HeadNumber`.
2. Candidate position = `PositionInVerse + (HeadNumber − Number)`. If a word with that position exists in the same
   `LxxRefId`, is not the word itself, and `Strongs[..5] == HeadStrongs[..5]` → head.
3. Else: the word in the same `LxxRefId` with `Strongs[..5] == HeadStrongs[..5]`, not itself, nearest to the
   candidate position (ties: lower position) → head.
4. Else: no head.
5. Remove cycles (should not happen with STEP data): if following heads from a word returns to it, drop the edge of
   the word with the highest position in the cycle.

Resolver test cases (`tests/UnshackledWord.Application.Tests/Features/OriginalWordHeadResolverTests.cs`): Rev 1:1
subset (positions 7/8, 10/11/12, 22/23/24 → ὁ→θεὸς, τοῖς→δούλοις, αὐτοῦ→δούλοις, τοῦ→ἀγγέλου, αὐτοῦ→ἀγγέλου); offset
numbering (John 16:13 style: `#28»29` at position 26 → position 27); self-reference resolved by Strong's fallback;
Strong's mismatch with no fallback → no head; head in another `LxxRefId` of the same German verse → no head; two-word
cycle → one edge dropped. Kind: `T-NSM` → `article`, `P-GSM` → `pronoun`, `PRT-N` → `particle`, `HTo` (hebrew) →
`objectMarker`, `N-NSM` → `other`.

Service test (`VerseMappingServiceTests`): Rev 1:1 words from a fake repository → response has `HeadId` on ὁ and
`HeadSource "step"`, θεὸς has none. Repository test: SQL constant contains `"ConjoinWord"` and `"LxxRefId"`.
Vitest (`mappingEditor.test.ts`, `MappingEditor.test.tsx`): an article with `headId` and no edge has status
`attached`; group number of ὁ equals that of θεὸς and "Gott"; tile renders under its head; read-only mode unchanged.

Done when: all tests above pass, `dotnet test` and `bun run check` green, `GET .../bookId/66/chapterId/1/verseId/1`
shows the heads, Bruno `GetVerseMapping.bru` docs mention the new fields.

### S02 – Migration 0008

Files: `src/UnshackledWord.Tooling.Postgres.Migration/scripts/0008_OriginalWordRelations.sql` (schema above, both
tables). Test procedure (document the commands in the commit message):

1. `docker exec postgres-test-db psql -U postgres -c 'CREATE DATABASE uw_migration_test'`.
2. Run the migration project against it (connection string via `Program.local.cs`/env), scripts 0001–0006 only
   (move 0008 aside or run before adding it), insert ~20 hand-made mapping rows in both tables (incl. an added word and
   an over-assigned word), run the verification queries → save output.
3. Run the migration project again with 0008 → verification output identical; override tables exist, empty.
4. Run `0008` a second time with `psql -f` → no error (idempotent). Run the migration project again → DbUp skips it.
5. Repeat 2–3 on a `pg_dump` of the real mapping tables if the maintainer wants a full-size check (optional,
   local only).

### S03 – Read overrides

Repository: `GetHeadOverridesAsync(bool hebrew, IReadOnlyCollection<int> stepWordIds)` →
`SELECT o."DependentStepWordId", o."HeadStepWordId", o."Source" FROM … WHERE o."DependentStepWordId" = ANY(@Ids)`.
Service merges by the semantics in "Recommended schema": override wins, override head outside the verse → treated as
detach, override equal to STEP → shown as `step`. Response sets `StepHeadId` when the override differs.

### S04 – Write heads + editor attach

`SaveVerseMappingRequest.Heads` (`List<VerseMappingHead>?`), validator rules from "API contract", service diff,
repository `ReplaceHeadOverridesAsync` in the same transaction as the links (`DELETE … WHERE "DependentStepWordId" =
ANY(@VerseWordIds)` then insert). Frontend: `attach`/`detach` in `mappingEditor.ts`, o→o drop allowed, attach mode,
PUT sends `heads` (always the full map once loaded). Wait for the UI draft before the TSX part.

### S05 – Rolling seed for overrides

CSV `assets/Elb1871WordRelations.csv`, columns `Language,LxxRefId,PositionInVerse,Word,HeadLxxRefId,HeadPositionInVerse,HeadWord`
(empty head = detach). Export in `BackupFileService`/`ElbDashboardRepository`; import in
`src/UnshackledWord.Tooling.SeedDb/Services/WordRelationImport/WordRelationImportRunner.cs` (+ repository,
`ServiceCollectionExtensions`), registered after `AiMappingImportRunner` in `SeedingService`; skip when either override
table has rows; match words by `LxxRefId` + position + text (like `AiMappingImportRunner`), log and skip misses.

### S06–S09

As in the step table; each reuses `OriginalWordHeadResolver` and the S03 merge (move the merge into a small
`OriginalWordHeads` service class in S03 so S06–S08 can call it for many verses: one query for words incl.
`ConjoinWord`, one for overrides).

## Risks

| Risk | Impact | Mitigation |
|---|---|---|
| STEP numbering offsets / wrong targets (533 of 41,049 not hit by position) | Wrong or missing heads shown as "STEP" | Strong's-checked resolution + nearest fallback; 131 unresolved show no head; maintainer overrides; resolver tests on real cases |
| STEP re-import changes `SERIAL` ids | Override rows point at wrong words | Readers drop ids outside the verse; seed CSV is position + text based; after a STEP re-import, export → reseed overrides (same issue exists for mapping rows today) |
| Changing STEP data (`ConjoinWord` semantics) between STEP releases | Baseline shifts silently | Read-time parsing follows STEP automatically; overrides equal to STEP become no-ops; parser returns `null` on unknown shapes |
| Licence: seed CSV contains Greek/Hebrew word text from STEP (as the mapping CSV already does) | Redistribution question | Only positions + word text for matching, no STEP fields; verify with STEPBible-Data licence (CC BY 4.0) together with 0002's licence check; alternative: positions only + checksum of word |
| Migration numbering clash with 0002 S29 (`0007`) | DbUp order on fresh vs. existing DBs differs | 0007 and 0008 independent by design; keep it so (0007 must not reference the override tables) |
| Checksum query load on 556k rows | Slow on small hosts | Run once before/after; it is read-only |
| UI clutter (41k relations in the NT, incl. pronouns) | Editor harder to read | Draft decides visibility; e.g. show only articles by default, others on hover |
| Over-assigned AI rows plus attached article (double coverage) | Confusing groups | Kept as is; S09 report; editor shows both, maintainer cleans manually |
| Validation gaps (cycles, foreign ids) | Corrupt overrides | Validator + reader cycle breaking, tests for both |

## Open questions for the maintainer

1. Confirm option A (read-time parsing, overrides only) over a seeded baseline table (option B).
2. Is a stored relation **kind** needed (article / attributive adjective / apposition / possessive), or is the kind
   derived from morphology enough for v1?
3. Which STEP relations should the editor show by default: all 41k (articles, pronouns, particles, relatives) or only
   articles (`T-`), others on demand?
4. Rolling seed on populated DBs: today `AiMappingImportRunner` imports only into empty tables. Should a later step
   merge an updated seed (mappings and overrides) into a self-hoster's DB, and with which conflict rule?
5. Should the AiWorker prompt receive the STEP heads (to stop mapping articles separately), and should future
   AI-written relations use `Source = 'ai'`?
6. Hebrew: is a rule-based baseline wanted (e.g. אֵת `HTo` → next word, construct chains), stored as `Source = 'rule'`
   or computed at read time like STEP?
7. Should an attached article that the AI also mapped to the head's German word ("Gott" → ὁ, θεός) be offered a
   one-click "remove redundant link" in the editor (it would change an AI row, which is allowed only by explicit save)?
8. Names: `GreekWordRelationOverride` / `HebrewWordRelationOverride` and the API field names `headId`/`heads` – ok?
