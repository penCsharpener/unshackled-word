# Roadmap 0010: Repo seed export/import (stable keys, re-anchoring) and STTS tags

Carved out of `0007-smart-search.md` (now an index). Owns the prerequisite 0008.0 of roadmap 0008
(`Elb1871Morphology` table and its import on fresh installs).

## Goal

Everything the project itself creates (German morphology and lemma corrections, mappings, later the Elb1871 revision)
is exported into the repo as a seed, committed, and imported on fresh installs, so a new `docker compose up` gets the
maintainer's current data. Rows are identified by references that survive a fresh install and fixes in the STEP parsers
or in `assets/elberfelder1871-theword-export.txt`, never by `SERIAL` ids. A second, small feature rides along: the full
STTS tag list as a seeded table for the UI, and a check that `Elb1871Morphology` only uses valid, consistent tags.

Why this is needed now (maintainer's DB copy, 2026-11-12/13, read-only):

- `Elb1871Morphology` (721,552 rows) has **no `CREATE TABLE` in `scripts/` and no importer**. `0010_LemmaBulkEdit.sql`
  creates indexes on it and `0012_DataQuality.sql` joins it in `DqWordFacts`, so a fresh install fails at 0010 today.
  This blocks 0008 (its slice 0008.0 points here).
- The ids are not portable: the maintainer's DB has 722,753 `Elb1871Words`, a fresh parse of today's `assets/` gives
  722,257 (0005 "Seeding audit"). About 500 words differ, so positions in verse differ in some verses already now; the
  first export from this DB onto a fresh install is itself a re-anchoring case.
- The existing rolling seed loses rows: `assets/Elb1871Mappings/*.csv` (66 files, 33 MB) is matched by verse + position
  + exact word text (`AiMappingImportRunner`), and a fresh seed imports 169,527 / 553,919 of 170,071 / 556,416 Greek /
  Hebrew rows (0005 audit): 3,041 rows silently dropped. The CSV also carries STEP word text (`StepWord`), which
  conflicts with "STEP data is never redistributed".
- STTS: the column `Stts` is filled for every row with 43 distinct tags, all valid STTS, but the tag set is not
  consistent with the UD part of speech: with one default UD tag per STTS tag, 43,872 rows disagree (largest groups
  `ADJD`/`ADV` 8,512, `VMFIN`/`AUX` 6,673, `NE`/`NOUN` 2,146, `PTKVZ`/`ADV` 2,242), and some tags are suspicious even
  where they agree (`zu` is `PTKVZ` 10,922 times, relative `welche` is `PWAT` 1,161 times, `PRELS`/`PTKZU` never occur).
  The lemma editor never changes `Stts` (`LemmaContracts.cs`: "Stts is left untouched"), so every PoS edit can add a
  mismatch. `ElbLemmaValueMaps.Stts` (C#) lists 43 tags with labels; 11 STTS tags are missing there.

End state: `docker compose up` on an empty volume creates all tables, imports the morphology seed (re-anchored onto
whatever the parser produced), imports the mapping seed with the same anchoring, writes a report of anything it could
not place, and later starts only ever update the mapping seed, verse by verse, where the self-hoster has not changed the
data. The maintainer runs one export command and commits the diff.

Out of scope: the Mapping v2 schema and its own seed format details (0005, especially 0005.11; this roadmap provides
the keys and the anchoring they use), 0004's override CSV (same), the Elb1871 revision editor (only its seed slot is
reserved), automatic exports, exporting anything STEP-derived (`DqWordFacts`, `Stats*`, STEP words).

## Decisions

Only rules stated by the maintainer (in 0007, 0008 or CLAUDE.md). Everything else is under "Open questions" with a
recommendation; the tasks follow the recommendation until it is answered.

| Topic | Decision | Source |
|---|---|---|
| What is exported | All changed data: German morphology/lemma corrections, mappings, the Elb1871 revision; committed into the repo as the seed for fresh installs | 0007 |
| Keys | No int ids. What is guaranteed: book, chapter, verse, and maybe position in verse; the key must work on an old and a totally new installation and survive fixes in the STEP parsers and in `assets/elberfelder1871-theword-export.txt` | 0007 |
| Algorithms | "The more is based on algorithms the better": derive and match by algorithm, store as little hand-made data as possible | 0007 |
| Schema changes | If 0004/0005 change the DB structure, the seed format of `assets/Elb1871Mappings` must be renewed with it | 0007 |
| STTS | Full list of STTS POS tags with meanings for the UI, can be a seeded DB table; `Elb1871Morphology` must only contain matching tags | 0007 |
| `Elb1871Morphology` | `CREATE TABLE` + import are still to be done, prerequisite 0008.0 (owned here) | 0008 decisions |
| Licence | App and data CC0; STEP data downloaded on the fly, never redistributed or modified | CLAUDE.md |
| Compose semantics | Seed runs only on initial startup; only the mapping seed may be updated afterwards; the official repo ships a rolling seed for mappings and the Elb1871 revision; self-hosters may keep their own data | CLAUDE.md |
| Stack | Dapper only, raw SQL in repo style, numbered migration scripts, CsvHelper for CSV | CLAUDE.md, skills |
| Schema vs data | Schema (incl. the `Elb1871Morphology` and `SttsTags` `CREATE TABLE`) is a numbered migration; migrations carry **no data**. All seed data is CSV (compact for git, human readable) and is imported by the seed after all migrations on first start | maintainer, 2026-11-13 |
| Morphology seed | Exported complete (every German word), one CSV per book | maintainer, 2026-11-13 |
| STEP text in mapping CSV | The `StepWord` text column in `assets/Elb1871Mappings` is replaced by the 8-hex `SrcHash` | maintainer, 2026-11-13 |
| Mapping seed updates | On by default on later starts; only verses not changed locally are replaced | maintainer, 2026-11-13 |
| STTS source of truth | The `SttsTags` table (seeded from CSV). `ElbLemmaValueMaps.Stts` stays for in-code handling but must agree with it; a test and a startup check fail on drift | maintainer, 2026-11-13 |
| STTS on PoS edits | When the UD part of speech is edited, the matching STTS tag is proposed, never set automatically | maintainer, 2026-11-13 |

## How to use this roadmap

Work in table order or as *Depends on* allows; one task = one commit. A slice is the backend row plus its `f` row of the
same number; both land before the slice counts as done, and every slice leaves `dotnet build UnshackledWord.slnx`,
`dotnet test` and `docker compose run --rm --no-deps unshackledword.web bun run check` green. Conventions of 0004
("Conventions for all steps") apply. DB experiments on a scratch DB (`postgres-test-db` or a throwaway `postgres:18`),
never on the shared DB copy; "fresh compose stack" means `docker compose down -v && docker compose up` on a scratch
project name. Rows without UI have no `f` row; their "Done when" is a command whose output can be checked.

## Tasks

| ✔ | # | Task | Depends on | Done when |
|---|---|---|---|---|
| [x] | 0010.1 | **= 0008.0, part 1.** Schema-only migration (no rows) that DbUp orders after `0009_LemmaEdits.sql` and before `0010_LemmaBulkEdit.sql`, so fresh installs have the table before 0010 and 0012. DbUp sorts by script name and 0010–0012 are already applied, so the name must sort in that gap; working name `scripts/0009a_Elb1871Morphology.sql` (final name: open question 2): `CREATE TABLE IF NOT EXISTS "Elb1871Morphology"` with the exact shape of the maintainer's table (`Id serial` PK, `HebRefId`, `PositionInVerse`, `Lemma varchar NOT NULL`, `PartOfSpeech varchar NOT NULL`, `Stts`, `Degree`, `VerbForm`, `Tense`, `Person`, `Number`, `Mood`, `Case`, `Gender` all `varchar NULL`, collation `und-x-icu` as `Elb1871Words`), `UQ_Elb1871Morphology_Verse_Position` unique on (`HebRefId`, `PositionInVerse`), column comments; `ElbMorphologyRaw` untouched until open question 1 is answered | – | Fresh scratch DB: DbUp runs 0001–0012 without error (today it stops at 0010); on a restored copy of the maintainer's DB the script is a no-op (`\d` identical before/after); `REFRESH MATERIALIZED VIEW "DqWordFacts"` succeeds on the fresh (empty) DB |
| [x] | 0010.2 | Anchoring library `Application/Features/Seed/Anchoring` (pure C#, no DB): `TokenKey` normalisation (NFC, case kept, punctuation stripped as in `PlainWord`), `ChapterFingerprint` (SHA-256 of the chapter's normalised token list, 12 hex), `SequenceAligner` (Myers diff over two token lists of one chapter) returning per old token `Exact` (same ref, pos, text), `Shifted` (same text, other pos or neighbouring verse of the same chapter), `Modified` (inside a 1:1 replace block with equal neighbours, e.g. a typo fix) or `Orphaned`; plus `SourceKey` (`LxxRefId`, position, `SrcHash` = 8 hex of SHA-256 over the STEP word NFC-normalised, diacritics/cantillation removed, lower-cased) with the same aligner over hashes. SeedDb verb `anchor-check` (`dotnet run --project src/UnshackledWord.Tooling.SeedDb -- anchor-check`) parses `assets/elberfelder1871-theword-export.txt` with the existing `ElberfelderParser` in memory and aligns it against the words of the configured DB | – | Unit tests: identical chapter, inserted word, deleted word, word moved to the next verse, typo fix, verse split, two equal words swapped, empty verse; `anchor-check` on the maintainer's DB prints counts per class and the chapters with `Orphaned > 0`; the numbers go into "Background" (expected: the ~500-word difference shows up as `Shifted`/`Orphaned` in a few chapters) |
| [x] | 0010.3 | Morphology export: SeedDb verb `export-seed --only morphology` writes `assets/seed/morphology/NN-Book.csv` (columns `Ref,Pos,Form,Lemma,Upos,Stts,Feats`; `Ref` = `HebRefId` `BBCCCVVV`, `Form` = `PlainWord`, `Feats` = UD style `Case=Nom\|Gender=Masc\|Number=Sing` from the eight feature columns in fixed key order, empty when none), one line per German word in `Ref,Pos` order, also for words without a morphology row (empty `Lemma` etc., so the file is the complete token manifest); `assets/seed/anchors/NN-Book.csv` (`Chapter,Words,Fingerprint`); `assets/seed/manifest.json` (`format: 1`, base text file + SHA-256 + word count, per file row count + SHA-256; no timestamp in any row, one `exportedAt` line in the manifest). CsvHelper, invariant culture, UTF-8 without BOM, LF | 0010.2 | Export on the maintainer's DB writes 66 + 66 files with 722,753 morphology lines in total; a second export without DB changes is byte-identical (`git diff --stat` empty except `exportedAt`); one lemma edit in the editor changes exactly one line in one file |
| [x] | 0010.4 | **= 0008.0, part 2.** `MorphologySeedRunner` in SeedDb, registered after `Elb1871VerseRunner`: runs only when `Elb1871Morphology` is empty; reads the seed, aligns each chapter whose fingerprint differs from the freshly parsed words (equal fingerprint = fast path, positions taken as they are), inserts `Exact`/`Shifted`/`Modified` rows with the new positions, skips `Orphaned`; table `SeedImports` (`Id`, `Kind`, `SeedVersion` = manifest SHA-256, `ImportedAt`, `Rows`, `Exact`, `Shifted`, `Modified`, `Orphaned`) in a new migration script; report `temp/seed-report/morphology-<version>.csv` (old key, new key or empty, class, form); **must call `"unshackled-word"."RefreshVerseLemmaText"(NULL)` once at its end** (function from migration 0016 of roadmap 0009; without it `Elb1871Verses."LemmaText"` stays empty and the smart search has no lemma widening on a fresh install; the migration already fills it for existing data). Commit the first real export (0010.3 output) in the same slice | 0010.1, 0010.3 | Fresh compose stack: `Elb1871Morphology` has one row per placed seed line, `SeedImports` shows the counts, the report lists every orphan; `POST api/dataQuality/refresh` fills `DqWordFacts`; a second `docker compose up` imports nothing ("skipped: not empty" in the log); 0008.1 can start |
| [x] | 0010.4f | Data-quality overview: line under the refresh timestamp "Seed: Stand <exportedAt>, importiert <ImportedAt>, 12 Zeilen nicht zuordenbar" from `SeedImports` (new field `seedImports` in `GET api/dataQuality/overview`, maintainer/reviewer only as the page is) | 0010.4 | Vitest with and without `seedImports`, with 0 and > 0 orphans; i18n de/en |
| [x] | 0010.5 | STTS table. Schema-only migration `scripts/00NN_SttsTags.sql` (next free number): `SttsTags` (`Tag varchar(8)` PK, `LabelDe`, `LabelEn`, `DescriptionDe`, `DescriptionEn`, `ExampleDe`, `Upos` = default UD tag, `AllowedUpos text[]`, `Alias` (e.g. `PAV` for TIGER's `PROAV`), `IsPunctuation`, `SortOrder`). Data: `assets/seed/stts-tags.csv` with all 54 tags of STTS 1999 in TIGER spelling (`AllowedUpos` as `\|`-separated list), imported by `SttsSeedRunner`, the first runner in `SeedingService` (before the morphology import), only into an empty table. `GET api/lemmas/valueMaps` reads the STTS part from the table. `ElbLemmaValueMaps.Stts` stays for in-code handling and is extended from 43 to the same 54 tags; drift guard: (a) a unit test in `UnshackledWord.Domain.Tests` that parses `assets/seed/stts-tags.csv` and fails when tag set or de/en labels differ from `ElbLemmaValueMaps.Stts`, (b) a WebApi startup check that compares the C# list with `SttsTags` and logs an error naming the differing tags (empty table = not seeded yet, warning only) | – | Migration runs twice on a fresh DB and inserts no rows; fresh compose stack: `SELECT count(*) FROM "SttsTags"` = 54; every tag in use on the copy (43) is in the table; changing one label in the CSV makes the drift test fail; a DB row deleted on a scratch DB produces the startup error; `GetValueMaps` test returns 54 STTS entries with de/en labels |
| [x] | 0010.5f | Lemma editor: STTS column (read-only, toggled via the existing `ToggleMenu` column visibility) shows the tag with the localized label as tooltip/`title`; word detail popover shows the STTS label next to the UD part of speech | 0010.5 | Vitest: label in de and en, unknown tag rendered as the raw tag with a warning style token; `bun run check` green |
| [x] | 0010.6 | Validation: two data-quality checks in `DqCheckRepository` / `DqOverviewService` following L1–L5: **L6** "STTS unbekannt oder leer" (`Stts` NULL or not in `SttsTags`), **L7** "STTS passt nicht zur Wortart" (`PartOfSpeech <> ALL("AllowedUpos")`), with facets book / tag / UD tag and jump to the lemma editor; read from `Elb1871Morphology` live (no `DqWordFacts` change, so 0008's chained views stay untouched) | 0010.5 | Check tests with FakeDbReader; on the scratch copy L6 = 0 and L7 equals the `AllowedUpos` result documented in "Background" (≤ 43,872, the strict one-to-one count); counts visible via `GET api/dataQuality/overview` |
| [x] | 0010.6f | Data-quality page: `StatTile`s for L6/L7 and their findings lists (`FindingsTable`, existing columns plus "STTS" and "Wortart") | 0010.6 | Vitest for both tiles and a finding row; i18n de/en; manual: a click on an L7 finding opens the lemma editor on that word |
| [x] | 0010.7 | Fix path: `Stts` becomes editable in `PUT` lemma word/chapter saves and in bulk apply (`LemmaEditValidator`: tag must exist; for **edited** rows the pair must be allowed by `AllowedUpos`, unchanged legacy rows are not blocked); when only the UD tag is edited and the old STTS no longer fits, the response proposes the matching STTS (the tag whose `Upos` is the new UD tag, refined by the edited features, e.g. `VERB` + `VerbForm=Fin` → `VVFIN`) and never writes it without the user accepting it; `Elb1871MorphologyEdits.BeforeValues`/`AfterValues` include `stts`; then `ALTER TABLE "Elb1871Morphology" ADD CONSTRAINT … FOREIGN KEY ("Stts") REFERENCES "SttsTags"("Tag")` in a new schema-only script, guarded by a check that L6 = 0 (on a fresh install both tables are still empty when it runs, so the seed order `SttsSeedRunner` → `MorphologySeedRunner` keeps it valid) | 0010.6 | Validator tests (unknown tag rejected, inconsistent edited pair rejected, untouched legacy mismatch accepted); repository test that the edit log carries `stts`; the FK script fails loudly on a scratch DB with one injected unknown tag and passes on the copy; a fresh compose stack still seeds |
| [x] | 0010.7f | Lemma editor: STTS dropdown (existing `Select` in `MorphologyGrid`) filtered to the tags allowed for the row's UD tag, with "alle anzeigen"; a UD change with an unfitting STTS shows the proposed tag inline with "übernehmen" (nothing changes without that click); bulk edit dialog gets an optional STTS target | 0010.7 | Vitest for the filtered options, the inline proposal and the bulk field; L7 count drops by one after fixing one finding on the scratch copy |
| [x] | 0010.8 | Mapping export in the anchored format (legacy tables, before 0005 cut-over): `export-seed --only mappings` writes `assets/Elb1871Mappings/NN-Book.csv` format 2: `Ref,Pos,Form,Part,SrcRef,SrcPos,SrcHash,Added,ParentPos,ParentForm` (`SrcRef` = STEP `LxxRefId`, so verses split by versification are unambiguous; `SrcHash` replaces the STEP word text); German side keyed like the morphology seed; rows in `Ref,Pos,SrcRef,SrcPos` order; manifest entry with `format: 2` | 0010.3 | Export on the copy writes 66 files with 170,071 + 556,416 lines in total minus documented drops (none expected); no Greek or Hebrew letters in any file (`rg -n '[\p{Greek}\p{Hebrew}]' assets/Elb1871Mappings` empty); second export byte-identical |
| [x] | 0010.9 | `AiMappingImportRunner` reads format 1 (today's CSV, unchanged behaviour) and format 2: German side through the morphology seed's token manifest + `SequenceAligner`, source side by `SrcRef`/`SrcPos`/`SrcHash` with the same rule order as 0005's `SyncSourceWords` (exact, then n-th occurrence of the hash in the verse, then aligner, else orphan); writes `SeedImports` (`Kind = 'mappings'`) and a report like 0010.4; still only into empty tables. Commit the first format-2 export in this slice | 0010.8, 0010.4 | Fresh compose stack imports ≥ 170,071 / 556,416 rows minus the orphans listed in the report (target: fewer than today's 3,041 drops); a test fixture with a shifted German word and a shifted STEP word lands on the right ids; format-1 fixture still imports as before |
| [x] | 0010.10 | Mapping seed update on populated DBs (the one seed allowed to change after first startup): table `SeedVerseState` (`Kind`, `HebRefId`, `ContentHash`, `SeedVersion`) written by every mapping import; on a later start with a new mapping `SeedVersion`, per verse: DB hash = stored hash (untouched since import) → replace that verse's rows from the new seed and store the new hash; DB hash ≠ stored hash (self-hoster or editor changed it) → keep, list in the report as "kept local"; setting `AppSettings:DatabaseSeeding:UpdateMappingSeed` (default `true`, self-hosters can switch it off); morphology is never updated after the first import | 0010.9 | Scratch stack: import seed A, edit one verse in the mapping editor, start with seed B that changes that verse and two others → the two others change, the edited verse stays, report lists it as kept; start again with B → nothing changes; setting off → nothing changes |
| [x] | 0010.10f | Data-quality overview line from 0010.4f extended: "Zuordnungs-Seed aktualisiert: 2 Verse, 1 lokal geändert behalten" with the report path | 0010.10 | Vitest for the update summary; i18n de/en |
| [x] | 0010.11 | Maintainer rebase after a parser or base-text fix: SeedDb verb `rebase` = `export-seed` (all kinds) → rebuild `Elb1871Words`/`Elb1871Verses` from the fixed `assets/` (or the STEP refresh path of 0005.4b) → re-import morphology and mappings through the anchored importers → report; refuses to run while Mapping v2 holds rows (0005's FK and append-only guards; there `SyncSourceWords` is the path) and without `--confirm-backup` | 0010.9 | Scratch copy: change one word in one verse of the theword export, run `rebase`: that chapter shows `Modified`/`Shifted` rows in the report, every other chapter `Exact`, the edited lemma of a word in that chapter is still on the same German word; without `--confirm-backup` the verb exits non-zero before writing |
| [x] | 0010.12 | Round trip and docs: README section "Seed" (what is exported, how keys and anchoring work, the export/commit routine, what compose does on first and later starts, the report files, the licence rule for STEP data) including the verification steps export → fresh stack → export again | 0010.4–0010.11 | Export from the copy → fresh compose stack → export from the fresh stack: morphology and mapping files identical except lines listed as orphans in the reports; `dotnet test` and `bun run check` green |

Ordering: 0010.1 is the hard start and unblocks 0008 together with 0010.4 (0010.2 → 0010.3 → 0010.4). The STTS strand
(0010.5–0010.7f) is independent of the seed strand and can run in parallel after 0010.1. The mapping strand
(0010.8–0010.10f) follows 0010.4. 0010.11 is optional before the first parser fix and must exist before it.

## Design

### Keys: what identifies a row across installations

| Object | Key in the seed | Why |
|---|---|---|
| German word | `Ref` (`HebRefId` = `BBCCCVVV`), `Pos` (`PositionInVerse`), `Form` (`PlainWord`) | Book/chapter/verse are guaranteed (0007); `HebRefId` equals book/chapter/verse for every word (0 mismatches on the copy). Position is a hint, `Form` makes it checkable. Elb1871 text is public domain, so the form may be stored |
| Word part (compound) | + `Part` (part text, `Schaf`/`hirten`) | 4,204 real parts in the Hebrew mapping (0005) |
| STEP word | `SrcRef` (`LxxRefId`), `SrcPos`, `SrcHash` | STEP ids are `SERIAL` and move on re-import; the STEP text must not be redistributed, a short hash of it is enough to verify and align and cannot be turned back into the text |
| Verse-level states | `Ref` + content hash | For the update rule (0010.10) |
| Revision edits (later) | `Level`, `Ref`, `FromPos`, `ToPos` + the base `Form`s they replace | Mirrors 0005's `GermanEdits` (positions From..To at a level); the forms let the aligner move an edit with its words |

No key contains a DB id. The fresh DB numbers its rows itself; importers resolve keys to ids at import time.

### Re-anchoring

Old installation and new installation can differ because the theword export was fixed, `ElberfelderParser` tokenises
differently, or STEP shipped a changed release / its parser was fixed. The seed carries enough to align old and new
token sequences, so all of these are one algorithm:

1. **Fingerprint per chapter.** The export stores the SHA-256 (12 hex) of each chapter's normalised token list. On import,
   the importer computes the same over the freshly parsed words. Equal → all keys are exact, no alignment (the common
   case; 1,189 chapters).
2. **Chapter-level alignment** where the fingerprint differs: Myers diff of old tokens (from the morphology seed, which
   has one line per German word) against new tokens; equal runs give `Exact`/`Shifted`, a 1:1 replace between equal
   neighbours gives `Modified` (a typo fix keeps its lemma and mapping, flagged), everything else `Orphaned`. Chapter
   rather than verse, so words that moved across a verse boundary are found.
3. **Source side** uses the same aligner on `SrcHash` sequences per `LxxRefId`, in the order 0005's `SyncSourceWords`
   uses (exact key, then n-th occurrence of the same text in the verse), so seed import and DB-side registry sync agree.
4. **Never guess beyond that.** Orphans are not imported; they are listed in `temp/seed-report/*.csv` and counted in
   `SeedImports`, and the data-quality page shows the count. A human decides.

### Formats

- One CSV per book (`NN-Book.csv`, as today), sorted by key, one line per row, no ids, no timestamps, CsvHelper with
  invariant culture, UTF-8 without BOM, LF. A changed lemma is a one-line diff.
- `assets/seed/manifest.json` holds the format version, the base text hash and per-file hashes; the importer refuses an
  unknown format version and logs a manifest/file hash mismatch.
- Every seed is CSV (decided): morphology, mappings, STTS tags, later the revision. Migrations create schema only and
  never insert rows; the seed imports the CSVs after all migrations have run on first start.
- Morphology is exported **complete** (decided; 722k lines, estimated ~30 MB like the mapping CSVs), one CSV per book.
  The tagger run that produced `Elb1871Morphology` is not in the repo, so there is no algorithm to replay.
- Not exported, because they are derived by algorithm: verse text and words (from `assets/`), `SearchVector`,
  `LemmaText` (0009), `DqWordFacts`, `Stats*` (0008), Strong's numbers (read from STEP).

### Docker compose semantics

| Start | Morphology seed | Mapping seed | STTS table |
|---|---|---|---|
| First (empty volume), after all migrations | imported, re-anchored, report | imported, re-anchored, report | imported from `stts-tags.csv` (first runner) |
| Later, same seed | skipped | skipped (same `SeedVersion`) | skipped |
| Later, new seed in the repo | skipped (rule: only mappings update) | on by default: per-verse update where the DB still equals the last import, local changes kept and reported | skipped (a changed tag list needs a deliberate maintenance step; the drift check reports it) |

Self-hosters keep their data: nothing overwrites a verse they changed, and `UpdateMappingSeed = false` turns updates
off entirely.
The official repo's rolling seed is simply the latest committed export.

### Relation to `assets/Elb1871Mappings` and roadmaps 0004/0005

- **Today (legacy tables):** 0010.8/0010.9 keep the folder and file names and add format 2 next to format 1; the
  importer reads both, so the switch is one commit of exported files.
- **0005 (Mapping v2)** changes the unit from edges to links. 0005.11 already plans `assets/MappingV2/NN-Book.csv`
  exported from v2 and imported by natural key. It should use this roadmap's keys (`Ref`/`Pos`/`Form`/`Part` for German
  members, `SrcRef`/`SrcPos`/`SrcHash` for source members), the `SequenceAligner`, `SeedImports` and the per-verse update
  rule, so there is one anchoring implementation. The legacy CSV becomes read-only at 0005.12 (cut-over) and can be
  deleted once a v2 export is committed. 0005.11 needs a short pointer to this roadmap when 0005 is next edited.
- **0004 (word relations)** plans `assets/Elb1871WordRelations.csv` matched by `LxxRefId` + position + STEP word text;
  it should use `SrcRef`/`SrcPos`/`SrcHash` instead (same licence argument).
- **Revision layer:** reserved as `assets/seed/revision/` with the key in the table above; built with the revision
  roadmap. It is a rolling seed like mappings (CLAUDE.md), so it will use the 0010.10 update rule.

## Background

- `Elb1871Morphology` on the copy: 721,552 rows; indexes `Elb1871Morphology_pkey` (`Id`), unique
  `UQ_Elb1871Morphology_Verse_Position`, `Elb1871Morphology_Lemma_idx`, `Elb1871Morphology_LemmaLower_idx` (the last
  two from 0010). Every row matches an `Elb1871Words` row; 1,201 words have no morphology row (data-quality L1).
  `Id` default `nextval('Elb1871Morphology_Id_seq')`. Leftover empty table `ElbMorphologyRaw` (0 rows, has `Category`,
  no `Stts`). 18,196 distinct lower-cased lemmas.
- Edit history: `Elb1871MorphologyEdits` 783 rows, `Elb1871MorphologyChangeSets` 4. History stays in the DB (user ids
  are local); the seed carries the resulting state only.
- STTS tags in use (43): NN 136,524, ART 77,691, KON 67,789, PPER 66,612, VVFIN 48,194, APPR 41,769, VAFIN 37,949,
  NE 35,809, … PTKA 2. Never used: PIDAT, PPOSS, PRELAT, PRELS, PTKZU, TRUNC, VAIMP, VMINF and the punctuation tags
  `$,` `$.` `$(` (punctuation is not a word in `Elb1871Words`). `ElbLemmaValueMaps.Stts` today covers exactly the 43 used (0010.5 extends it to 54).
- Format change of `assets/Elb1871Mappings` (0010.8/0010.12): the 66 files were format 1 (`StepWord` = STEP text, matched by verse + position + exact text) and are now format 2 (`SrcRef,SrcPos,SrcHash`, no STEP text, anchored); `AiMappingImportRunner` still reads format 1, the repo ships format 2 only. README section "Seed" documents the routine, the compose behaviour and the round-trip verification.
- Seed today: `SeedingService` order Elberfelder → TSK/SR/SBL → STEP → `AiMappingImportRunner`; every runner skips
  non-empty tables; `compose.yml` mounts `./assets` into the seed container.
- `anchor-check` (0010.2, 2026-11-13, maintainer DB vs fresh parse of `assets/`): 722,753 DB words vs 722,257 parsed; old tokens
  exact 719,103, shifted 2,910, modified 0, orphaned 740 (244 new-only tokens), in 188 chapters. The
  typical case is a hyphenated word (`Sohn-seine`, Genesis 34:8): the DB holds three tokens (`Sohn`, `-`, `seine`), the file one, so
  no 1:1 `Modified` applies; report in `temp/seed-report/anchor-check.csv`. Seed import on a fresh parse (0010.4): 721,054 of 721,552 morphology
  rows placed (exact 718,152, shifted 2,902, modified 0, orphaned 498), 188 chapters aligned; round trip export → fresh DB →
  export shows 0 differences besides the 498 orphans and the 2,902 shifted keys. Seed size: 39.5 MB raw, 5.2 MB as tar.gz.
- Mapping seed (0010.8–0010.11, 2026-11-13, legacy tables of the maintainer's DB): export 726,487 lines (170,071 Greek + 556,416 Hebrew,
  nothing dropped), 66 files, 34.1 MB raw, 7.2 MB as tar.gz, no Greek or Hebrew letters (295 Hebrew rows had Hebrew text in the German
  `GermanWordPart` column: exported with an empty `Part`). Format 2 has one column beyond the list in 0010.8: `ParentRef`, empty unless
  the parent word is in another verse (232 rows). Two STEP words can share `LxxRefId` + position (75 pairs in Hebrew), so `SrcHash`
  is part of the identity. 275 Hebrew rows carry a `HebRefId` that differs from their German word's verse; the export keys by the
  word, the import writes the word's verse (the rows are repaired by the round trip). Import on a fresh parse: 725,513 of 726,487 rows
  placed (exact 722,262, shifted 3,251, modified 0, orphaned 974 = 855 German + 119 parent, 0 STEP, 0 duplicates; formerly 3,041
  dropped), 188 chapters aligned; export → fresh DB → export shows 0 differences after key mapping. Format 1 on the same fresh parse
  still imports 723,446 rows as before. Update path (0010.10): `SeedVerseState` + migration `0019_SeedVerseState.sql`; verse hash
  computed identically in C# (`MappingVerseHash`) and SQL. `rebase` (0010.11) on a one-typo + one-inserted-word edit of the base text:
  2 chapters aligned, morphology exact 721,016 / shifted 37 / modified 1, mappings exact 725,474 / shifted 37 / modified 2, an edited
  lemma stayed on its word.
- Fresh-install proof (0010.12, 2026-11-13, throwaway `postgres:18`, migrations 0001-0019 in order incl. `0009a`/`0009b`, then SttsSeedRunner, German text, morphology, STEP bible text, mapping seed): `SttsTags` 54, morphology 721,054 (exact 718,152 / shifted 2,902 / orphaned 498), mappings 725,063 placed (orphaned 1,424 = 855 German + 119 parent + 450 source; the 450 source orphans are Jude and 3 x 4 lines of Psalm 44/57/108/149: the STEP download of that day returned HTTP 404 for 8 of its files, so `StepGreekWords` had no Jude), `SeedVerseState` 31,168 verses. Second start: nothing imported ("Elb1871Morphology is not empty", "Mapping seed is up to date", `SttsTags: 0 of 54 inserted`).
- Script numbering: 0001–0019 exist (0019 = `SeedVerseState`); 0008.1 claims `0013_MappingStats.sql`; 0005's planned `0009`/`0010` names already
  collide with `0009_LemmaEdits.sql`/`0010_LemmaBulkEdit.sql` and must be renumbered when 0005 starts.

## Risks

| Risk | Impact | Mitigation |
|---|---|---|
| `0009a_…` sorts between two applied scripts | On existing DBs DbUp runs it late (it is not journaled); on fresh DBs it runs before 0010 | `CREATE … IF NOT EXISTS` and a shape identical to the existing table; verify on a restored copy that it is a no-op (0010.1) |
| Re-anchoring places a lemma or mapping on the wrong word | Readers learn a wrong lemma/original with no visible sign | Only `Exact`/`Shifted`/`Modified` are imported, `Modified` is listed in the report; aligner tests include swapped equal words; data-quality checks run on the result |
| First export from the maintainer's DB onto a fresh parse (722,753 vs 722,257 words) | Some hundred rows orphaned on day one | 0010.2 measures it before anything is committed; orphans listed per chapter so the parser/export difference can be fixed first |
| Hash instead of STEP text in the mapping CSV | A changed STEP spelling makes the hash miss | Aligner falls back to position between equal neighbours (`Modified`); report lists the rest |
| Per-verse update overwrites a self-hoster's work | Lost local edits | Only verses whose content hash still equals the last import are replaced; the setting disables updates; report lists kept verses |
| Morphology seed is large (~30 MB) and changes often | Repo growth | Line-level diffs only for changed words; 66 files keep each diff local to a book |
| `rebase` on a DB with Mapping v2 | FK and append-only guards stop it halfway | Verb refuses when v2 holds rows; v2 uses `SyncSourceWords`; backup flag required |
| STTS FK added while bad tags exist | Migration fails on a self-hoster's DB | Script checks L6 = 0 first and stops with a clear message; L6 is visible on the data-quality page before |
| C# STTS list and `SttsTags` drift apart | UI labels and in-code rules disagree | Unit test against `stts-tags.csv` fails in CI; startup check logs the differing tags against the DB |
| Seeds only after all migrations | A migration that needs data (e.g. a backfill) finds empty tables on fresh installs | Migrations stay schema-only by rule; anything that needs data runs as a seed step (e.g. `RefreshVerseLemmaText` at the end of the morphology import) |
| STTS ↔ UD map too strict | 43,872 strict mismatches flood L7 and hide real errors | `AllowedUpos` lists accepted conventions (e.g. `VMFIN` → `AUX` or `VERB`); review the list against the top groups before shipping 0010.6 |
| Tags valid but wrong (`zu` as `PTKVZ` 10,922×) | L6/L7 pass while the data teaches wrong grammar | Out of reach of a tag check; candidate for a later "suspicious tag per form" check and for the 0009 lemma widening risk |
| Export written from the WebApi container | Writes into the repo through a mounted volume | Export is a SeedDb verb run by the maintainer, never an API endpoint |

## Deferred

- Revision layer seed content (`assets/seed/revision/`), with the revision roadmap.
- Exporting edit history or user attribution.
- Automatic export (CI job or scheduled) and a seed diff viewer in the UI.
- Suspicious-tag checks per form (e.g. `zu` + `PTKVZ` without a verb in the verse).

## Open questions for the maintainer

Answered on 2026-11-13 (see "Decisions"): mapping seed updates on by default, `SrcHash` instead of STEP text, complete
morphology export per book, `SttsTags` as source of truth with a drift-checked C# list, STTS proposed on PoS edits, all
seeds CSV after all migrations. Still open:

1. **`ElbMorphologyRaw`** (empty, not in any script): drop it with a schema-only migration (recommended) or keep?
2. **Name of the `Elb1871Morphology` migration:** it must sort between the applied `0009_LemmaEdits.sql` and
   `0010_LemmaBulkEdit.sql` (DbUp orders by name). Recommendation: `0009a_Elb1871Morphology.sql`; the alternative,
   adding the `CREATE TABLE IF NOT EXISTS` to the top of the applied `0010_LemmaBulkEdit.sql`, edits an applied script
   and is not recommended.
3. **Where does the export run?** Recommendation: SeedDb verbs (`export-seed`, `anchor-check`, `rebase`) run by the
   maintainer against his DB; the existing `BackupFileService` endpoint either delegates to the same exporter or is
   retired.
4. **`Modified` matches** (typo fixes): import them with a flag (recommended) or treat them as orphans?
5. **Drift check severity at startup:** log an error (recommended) or refuse to start the API?
