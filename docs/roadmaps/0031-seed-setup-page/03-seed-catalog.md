# 0031 · Seed catalog

The catalog is one C# list in `UnshackledWord.Seeding` (`SeedCatalog`): every step the page shows, in run order. The page, "Alle ausführen" and the SeedDb CLI all read it, so `SeedingService._runnerTypes` is replaced by it (the local filter keeps working on the catalog keys).

## A step

| Field | Meaning |
|---|---|
| `Key` | stable id, e.g. `elb1871-text` (URL and log) |
| `Group` | `base`, `step`, `other`, `derived` — one button per group |
| `Label` | German/English via i18n key `setup.step.<key>` |
| `Runner` | runner type, or `null` for read-only rows (`BibleBooks`) |
| `Tables` | tables whose `count(*)` the page shows |
| `Requires` | step keys that must be green first |
| `Source` | `migration`, `assets` (repo files) or `download` (with host, for the "needs internet" hint) |
| `Resumable` | true when running it on an amber state is safe (insert skips existing rows) |

## Status rule

- **grün** – every table of the step has rows.
- **gelb** – some tables have rows (also after a failed job that wrote some). A failed job that wrote nothing leaves the step rot with the flag `failed` (it can run again).
- **rot** – all tables empty.
- **grau** – red, but a `Requires` step is not green; the button is disabled and names the missing step.

Expected counts are not stored: environments differ (self-hosters' own data). The fake draft uses the dev numbers below.

## Steps (dev database counts, 2026-11-27)

The exact table list per runner is confirmed when the runner moves (0031.4/0031.7/0031.8): read its strategy/repository and list every table it inserts into.

| Group | Key | Runner | Tables (dev count) | Requires |
|---|---|---|---|---|
| base | `books` | — (migration 0001) | `BibleBooks` 66, `Languages` 2 | — |
| base | `stts` | `SttsSeedRunner` | `SttsTags` 54 | — |
| base | `revision-levels` | `RevisionLevelSeedRunner` | `Revisions` 3 | — |
| base | `elb1871-text` | `ElberfelderTextRunner` | `Elb1871Words` 722,743, `BibleVerseCountingMapping` 31,174 | books |
| base | `elb1871-verses` | `Elb1871VerseRunner` | `Elb1871Verses` 31,169 | elb1871-text |
| step | `step-text` | `StepDataBibleTextImporter` | `StepGreekWords` 142,095, `StepHebrewWords` 305,648, `StepGreekWordEditions` 1,100,328 (`GreekEditions` 22 is filled by migration 0047 and not a table of the step, or an empty database would show it amber) | books |
| step | `step-strongs` | `StepDataStrongsImporter` | `StepUnifiedStrongs` 22,808 (21,378 before the 0022.1b parser fix) | step-text |
| step | `step-morphology` | `StepDataMorphologyImporter` | `StepGreekMorphology` 1,644, `StepHebrewMorphology` 921 | step-text |
| step | `step-lexicon` | `StepDataLexiconImporter` | `StepStrongsLexicon` 22,717 (21,293 before the 0022.1b parser fix) (+ person/place/other lexicons, empty on dev) | step-strongs |
| step | `step-relationship` | `StepDataRelationshipImporter` | `StepHebrewWordsNormalized` 38,521, `StepHebrewWordsNormalizedToHebrewWords` 540,463 | step-text |
| step | `step-strongs-text` | `StepStrongsImport` | `StepStrongsToText` 682,558 | step-text, elb1871-text |
| other | `morphology` | `MorphologySeedRunner` | `Elb1871Morphology` 721,542 | elb1871-text |
| other | `tsk` | `TskRunner` | `Tsk` 378,805 | books |
| other | `tsk-curation` | `TskCurationSeedRunner` | `TskCurations` 5 (0 when `assets/seed/tsk/curations.csv` is absent: green with "nichts zu importieren") | tsk, elb1871-text |
| other | `sr-gnt` | `SrRunner` | `SrGntWords` 137,361 | books |
| other | `sbl` | `SblRunner` | `SblText` 7,939, `SblApparatus` 4,441 | books |
| other | `ai-mappings` | `AiMappingImportRunner` | `Elb1871GreekMapping` 169,981, `Elb1871HebrewMapping` 555,589 | elb1871-text, step-text |
| other | `lxx-text` | `LxxTextImporter` | `LxxVerses` 23,620, `LxxWords` 486,313 | step-text (TVTMS download) |
| other | `lxx-tags` | `LxxTagSeedRunner` | `LxxWordTags` 265,807 (dev data; the repo seed `assets/seed/lxx/tags.csv` is empty), `LxxHebrewLinks` 0 | lxx-text |
| other | `revision-seed` | `RevisionSeedRunner` | `RevisionVerses`, `RevisionWords`, `RevisionMapping` (0 on dev) | revision-levels, ai-mappings |
| derived | `verse-similarity` | `VerseSimilarityRunner` | `VerseSimilarities` 917,459 | step-relationship |
| derived | `stats-views` | refresh of the materialized views | `StatsStrongs`, `StatsStrongsRenderings`, `StatsGermanLemma`, `StatsGermanForms`, `StatsVocabRenderings`, `DqWordFacts` (populated yes/no, then count) | ai-mappings, morphology |

`revision-seed` with 0 rows is a valid green state only if `assets/seed/revision` is empty; the step then reports "nichts zu importieren" and the row shows grün with a note. Same for `lxx-tags` links.

## Coupling that the catalog replaces

`StepImportResult.InsertedGreekWords` makes the Greek-edition refresh and `VerseSimilarityRunner` depend on "this process just inserted words". In the page each step is its own decision: `step-text` refreshes editions when `StepGreekWordEditions` is empty, `verse-similarity` runs when `VerseSimilarities` is empty. The CLI keeps the old behaviour by running the catalog in order in one process.

## Library move

`src/UnshackledWord.Seeding` gets the runner folders from `Tooling.SeedDb/Services` (namespace `UnshackledWord.Seeding.<Source>`), their `ServiceCollectionExtensions`, `IRunner`, `StepImportResult`, the downloaders and `ParseHelper`, plus `SeedCatalog` and `SeedStepRunner` (runs one catalog step in a fresh scope, reports progress lines through `IProgress<string>`). Packages `CsvHelper` and `LemmaGenerator` move with it. `Tooling.SeedDb` keeps `Program`, `Worker`, the verbs and `SeedingService` (now a loop over the catalog). `Infrastructure/Seed` is not touched.
