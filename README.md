# unshackled-word

This project pursue the goal to provide all the necessary data to study the Bible in the original languages - primarily in German.
It was born out of the need to have a completely free data without any restrictions to copyright and licencing.
Of course there are already great Bible study tools and translations out there but most of the time there are strings attached.
In the spirit of thedoreanprinciple.org this project wants to go a different way.

## What does this repo provide?

1. A source for Bible data in German, Greek and Hebrew
   * the project `UnshackledWord.Tooling.SeedDb` downloads a variety of data sources that use open licenses and imports them into a postgres database, ie.:
     * [x] Step Bible Data (extensive Greek and Hebrew data)
     * [x] Treasury Of Scripture Knowledge
     * [x] Statistical Restoration Greek New Testament (SR)
     * [x] SBL Greek New Testament and Apparatus
     * [x] Elberfelder 1871 translation exported from TheWord BibleSoftware
     * [ ] LXX data
   * the `UnshackledWord.Tooling.WebApi` provides a way to query data imported data for studying and further enhancements
2. A fully AI tagged Elberfelder 1871
   * [x] with the help of Google Gemini AI the Elb 1871 was fully tagged with the Step Bible Data
   * this does not mean this tagged Bible is done but it's a start that must be improved upon
   * [ ] Hebrew OT tagged with the Greek LXX OT
3. Web frontend to study the Elberfelder 1871
   * [ ] for correcting and improving the tagging initially done with AI
   * [ ] for enhancing the data
   * [ ] studying the Scriptures (the baseline goal is something like the csv-bible.de)
   * [ ] creating a new, modern, more precise, copyright free Elberfelder translation for the 21. century
4. Vocab trainers for Greek and Hebrew
   * flash card vocab training of the original languages (baseline is cerego.com-like UX)
   * unlike Anki, the user should not need to grade his own learning success but an algorithm is doing that automatically
5. Enhancements to the Bible data akin to Logos Bible software
   * [ ] tagging for which person spoke which words of the biblical text
   * [ ] tagging for theologically themes
   * [ ] tagging for biblical concepts, metaphors and symbols

## Principles

* Licence: the app and all data created with it are CC0. STEP Bible data is downloaded on the fly and never redistributed or changed.
* Elberfelder 1871 is the base text. Other versions (a modernized literal revision first, later more dynamic or simpler ones up to a children's Bible) are verse variants on top of it, one per literalness level (1–1000, Elb1871 = 100), so a reader can slide from literal to simple. Every text save is a new version.
* Everyone can suggest changes, the maintainer merges them. AI-made mappings are attributed to their own user and confirmed by humans.
* Read-only study tools are public, writing requires login.
* Self-hostable via `docker compose`. The seed runs on first start; only the mapping seed is updated afterwards.

## Docker Setup

If you want to give this project a go and take a look at the data create a `.env` file in the root of the solution.
Add these lines if you leave everything else as default:

```dotenv
POSTGRES_CONNECTIONSTRING="Host=unshackledword.tooling.database;Port=5432;Database=postgres;Username=postgres;Password=test;"
ENVIRONMENT=Development
```

Then run `docker compose up -d` in the solution root.
If this doesn't work for you please create an issue with the details.

For a production deployment (reverse proxy, TLS, secrets, SMTP, backups) see [docs/deployment.md](docs/deployment.md).

## Development Setup

### Local Postgres Database

To get started with development you need a local postgres database.

```
docker run --name unshackledword.database -e POSTGRES_PASSWORD=test -p 5432:5432 -d postgres:18
```

Rather than using user secrets I prefer one secrets .json file that lives somewhere outside the solution.

### Postgres Migrations

The first project to run is `UnshackledWord.Tooling.Postgres.Migration`.
Create a `Program.local.cs` right next to its `Program.cs` and add the following code to it:

```csharp
public partial class Program
{
    static partial void AddLocalSecrets(ConfigurationManager builder)
    {
        builder.AddJsonFile(@"<absolute-path-to-your-secrets-folder>\unshackled-secrets.json", true, true);
        // builder.AddJsonFile(@"<absolute-path-to-your-secrets-folder>\unshackled-secrets-2.json", true, true);
        // you can have multiple json files here. The later you add them the higher their priority.
    }
}
```

Any `.local.cs` will be excluded from git.
In your `unshackled-secrets.json` add the following content:

```json
{
  "ConnectionStrings": {
    "PostgresConnection": "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=test"
  }
}
```

Now you can run the migration project and it will setup the schema and tables for you.

### Seeding the Database

Second to run is `UnshackledWord.Tooling.SeedDb`. Here follow the same steps as for the migration project to add your secrets and connection string.
You will also need to check the appsettings.json file for any relative path and copy the settings over to your secrets file.
Now use absolute paths in your secrets file to point to the right folders on your machine. This is easier to debug and get started.
Important paths to override are:

```
{
  "AppSettings": {
    "DatabaseSeeding": {
      "SolutionTempPath": "<absolute-path-to-solution>/temp",
      "SolutionAssetsPath": "<absolute-path-to-solution>/assets",
    }
  }
}
```

Then you can run the project and it will download the data from the various sources and import it into your local database.
Depending on your internet connection and the performance of your machine this can take a while.
The dataset is quite big and some of the sources need to be downloaded first.
The downloaded files are 'cached' in the solution `temp` folder so all subsequent runs will be faster.

### Seed

Everything the project creates itself (German morphology and lemma corrections, word mappings, the STTS tag list; later the Elb1871 revision) is exported
into this repo as CSV and imported again on fresh installs. A new `docker compose up` therefore starts with the maintainer's current data. Roadmap:
`docs/roadmaps/0010-export-import-seed.md`.

**What is in the repo**

| Path | Content |
|---|---|
| `assets/seed/stts-tags.csv` | the 54 STTS tags (labels de/en, default and allowed UD tags) for the `SttsTags` table |
| `assets/seed/morphology/NN-Book.csv` | one line per German word: `Ref,Pos,Form,Lemma,Upos,Stts,Feats`, complete (also words without a lemma) |
| `assets/seed/anchors/NN-Book.csv` | per chapter: word count and fingerprint of the token list |
| `assets/seed/manifest.json` | format version, hash of the base text, row count and SHA-256 per file, `exportedAt` |
| `assets/Elb1871Mappings/NN-Book.csv` | German word to Greek/Hebrew word mappings, **format 2** |
| `assets/seed/revision/levels.csv` | the literalness presets (`Level,Code,NameDe,NameEn,DescriptionDe,DescriptionEn,IsDefault`) |
| `assets/seed/revision/verses/NN-Book.csv` | the active revision variants: `Ref,Level,BasedOnLevel,BaseHash,Text,Note,Reviewed` (the text itself is the data) |
| `assets/seed/revision/mapping/NN-Book.csv` | their mapping: format 2 plus `Level` and `Origin` (`Ref,Level,Pos,Form,Part,SrcRef,SrcPos,SrcHash,Added,ParentPos,ParentForm,Origin`) |

Migrations create schema only. All seed data is CSV and is imported after all migrations have run.

**Keys and anchoring.** No row carries a database id. A German word is `Ref` (`BBCCCVVV`: book, chapter, verse), `Pos` (position in the verse) and `Form`
(the word); a word part adds `Part`. A STEP word is `SrcRef` (the STEP verse), `SrcPos` and `SrcHash` (8 hex characters of a SHA-256 over the normalised
STEP word). Because a new install parses the base text itself, positions can differ from the exporter's database. On import every chapter whose
fingerprint matches is taken as it is; for the other chapters the old and the new token list are aligned (Myers diff) and each line is classified
`Exact`, `Shifted`, `Modified` (a one-word replacement between equal neighbours, e.g. a typo fix) or `Orphaned`. Orphans are never guessed: they are
not imported, they are listed in a report and counted in the table `SeedImports`, and the data-quality page shows the count.

**Licence rule.** The app and everything it creates is CC0. STEP Bible data is downloaded on the fly and is never redistributed or modified: the mapping
CSV contains no STEP text, only the 8-hex `SrcHash` (it cannot be turned back into the word), and there are no Greek or Hebrew letters in it. Check:
`rg -n '[\p{Greek}\p{Hebrew}]' assets/Elb1871Mappings` must print nothing. Format 2 replaces the earlier format 1 of `assets/Elb1871Mappings`, which had
the STEP word text (`StepWord`) and was matched by verse, position and exact word text (3,041 of 726,487 rows were silently dropped on a fresh install). The importer
still reads format 1 for self-hosters who keep an old export; the files in the repo are format 2 only.

**Maintainer routine** (run against your own database, from the repo root; configure the connection like the SeedDb project above):

```sh
dotnet run --project src/UnshackledWord.Tooling.SeedDb -- export-seed                       # morphology + mappings + revision (--only morphology|mappings|revision, --out DIR)
dotnet run --project src/UnshackledWord.Tooling.SeedDb -- anchor-check                      # read-only: how many words of the DB match a fresh parse of assets/
dotnet run --project src/UnshackledWord.Tooling.SeedDb -- rebase --confirm-backup           # after a parser or base-text fix, see below
git diff --stat assets/seed assets/Elb1871Mappings && git add assets && git commit
```

An export is deterministic: without database changes a second export is byte-identical except the `exportedAt` line of the manifest, and one edited lemma is a
one-line diff. `rebase` is for the moment you fix `ElberfelderParser` or `assets/elberfelder1871-theword-export.txt`: it exports, rebuilds the German words
from the fixed assets, imports morphology and mappings through the anchored importers again (the revision keeps its rows) and writes reports. It needs a database backup first
(`--confirm-backup`) and refuses to run while Mapping v2 holds rows.

**The same steps in the web UI** (menu "Wartung" / "Maintenance", section "Seed-Rundlauf", Admin/Maintainer): export to `assets/seed`, import (only into empty tables),
update (keeps locally changed variants/verses and lists them), rebase (confirmation dialog: "I have made a database backup") and a round-trip check that exports twice into
scratch folders and compares the files with each other and with `assets/seed` byte by byte. They are the same services the CLI calls (`SeedFileExportService`,
`MappingSeedImporter`, `RevisionSeedImporter`, `SeedRebaseService` in `UnshackledWord.Infrastructure/Seed`). Each step is a background job (one at a time, log lines and
the result - orphan counts, replaced/kept/new/removed lists, the `SeedImports` rows - are polled from `GET api/maintainer/seed/status`; `POST api/maintainer/seed/jobs` starts one,
see `api/unshackled-word/Maintainer`). The job state lives in the API process only. The compose file therefore mounts `assets` and `temp` writable into the API container
(they must be writable for the container user); without a reachable assets folder the page says so and only the zip download works. A rebase empties tables the readers use for
its duration, so run it when nobody reads.

**What `docker compose up` does**

| Start | STTS tags, morphology | Mappings |
|---|---|---|
| First, empty volume | migrations, then `SttsSeedRunner`, the German text, `MorphologySeedRunner` (then `RefreshVerseLemmaText`), the STEP data, `AiMappingImportRunner`: all imported and re-anchored | imported |
| Later, same seed | skipped (tables not empty) | skipped (same `SeedVersion`) |
| Later, newer mapping seed in the repo | skipped: morphology is never updated after the first import | updated verse by verse (see below) |

Mapping update: after each import the database remembers a content hash per verse (`SeedVerseState`). A newer seed replaces a verse only when its rows still
hash to the remembered value; a verse that was changed locally (mapping editor, or a self-hoster's own data) is kept and listed in the report. Set
`AppSettings:DatabaseSeeding:UpdateMappingSeed` to `false` (compose: `UNSHACKLEDWORD_APPSETTINGS__DATABASESEEDING__UPDATEMAPPINGSEED=false`) to turn updates off.

**Revision seed.** The revision (roadmap 0011) is a second rolling seed. First start: into an empty `RevisionVerses` each variant is re-created the way the editor
creates it (the text is tokenised and the base links come from the carry-over); only its mapping lines are taken from the seed (German side by `Pos` + `Form`
inside the variant's own text, original side by `SrcRef`/`SrcPos`/`SrcHash` with the same rules as the mapping seed, orphans reported). Imported variants have
no author (`CreatedByUserId` NULL) and keep their `Origin`/`Reviewed` state. Later starts with a newer revision seed: per `(verse, level)` a variant is
replaced (as a new version) only when it still hashes to the value remembered after the last import (`SeedVerseState`, kind `revision-<level>`); local changes
are kept and listed, new variants are added, variants gone from the seed are ended. Switch off with `AppSettings:DatabaseSeeding:UpdateRevisionSeed=false`.
`rebase` exports the revision as well and, after rebuilding the German words, recomputes the base links of the active variants; variants whose Elb1871 verse
changed stay active and show up under "Basistext geändert" in the revision overview. Reports: `revision-<version>.csv`, `revision-update-<version>.csv` and
`revision-update-<version>-variants.csv`. The `Reviewed` column in `verses/` is one column beyond the roadmap's list; without it a round trip would drop the
"mapping confirmed" state.

**Reports** go to `temp/seed-report/` (not committed): `morphology-<version>.csv` and `mappings-<version>.csv` (everything that is not an exact match: class, old key,
new key, form), `mappings-update-<version>.csv` and `mappings-update-<version>-verses.csv` (per verse: updated or kept local), `anchor-check.csv`. The
data-quality page shows "Seed: Stand ..., importiert ..., N Zeilen nicht zuordenbar" and, after an update, "Zuordnungs-Seed aktualisiert: X Verse, Y lokal geändert
behalten" with the report path (`GET api/dataQuality/overview`, field `seedImports`).

**Verifying a round trip** (export, fresh install, export again):

1. `export-seed --out <dir-A>` against the source database.
2. `docker compose down -v && docker compose up` (on a scratch project name, or `dotnet run` of Migration and SeedDb against an empty throwaway Postgres).
3. `export-seed --out <dir-B>` against the fresh database and `diff -r <dir-A> <dir-B>`: the files are identical except the lines the reports list as orphans
   (and keys that were shifted to a new position, which the report lists as `Shifted`).

Measured on the maintainer's data (2026-11-13): 721,054 of 721,552 morphology rows and 725,513 of 726,487 mapping rows are placed on a fresh parse
(498 and 974 orphans; the old format dropped 3,041); the seed is about 74 MB raw in git (about 12 MB packed).

### Web API

Now that the database is setup and seeded you can run the `UnshackledWord.Tooling.WebApi` project.
Again this will need the same secrets setup as the previous projects to connect to the database.

Writing mappings (`PUT /api/elberfelder/verseMapping/...`, used by the mapping editor) is switched off by default because
there is no login yet. Enable it only on a trusted instance with the environment variable
`UNSHACKLEDWORD_MAPPINGEDITING__ENABLED=true` (setting `MappingEditing:Enabled`); see `docs/roadmaps/0003-mapping-editor.md`.

### Data quality and word statistics

The Strong's page, the word statistics and the data-quality page read from a *snapshot*: the materialized views
`DqWordFacts` (migration `0012`, rebuilt by `0014`) and `StatsStrongs`, `StatsStrongsRenderings`, `StatsGermanLemma`,
`StatsGermanForms` (`0013`, rebuilt by `0014`). They pre-aggregate the German words (lemma + part of speech) against
the Strong's numbers of the mapping, so a page does not have to group 700k+ words per request.

- A snapshot is only refreshed by the "Neu berechnen" buttons (data-quality page and Strong's page, `POST api/dataQuality/refresh`, roles
  `Admin`/`Maintainer`). Nothing else starts a refresh, not startup, not the seed, not the migrations. A full refresh takes
  about 40 s on the full data; the pages show "wird berechnet …" and the new "Statistik: Stand …" time afterwards.
- The views are created empty. While `StatsRefreshedAt` is not set (fresh install, never refreshed) the Strong's page and
  the word statistics fall back to the live SQL, which gives the same answers more slowly and per form instead of per
  lemma + part of speech; the footer then says the numbers are live.
- After changing mappings or lemmas, refresh once to bring the snapshot in line. The snapshot holds STEP-derived values, so it stays
  in your own database and is never exported to the repo seed.
- `0014_AddedWordLinks.sql` drops and recreates `DqWordFacts` together with the four `Stats*` views; run the migrations
  (`run-db-migration.ps1`), then refresh.

See `docs/roadmaps/0008-mapping-stats-snapshot.md` for the design, measured sizes and the verification.

### Search

The Bible reader has a search overlay (`F` or the magnifier icon on every `/bible...` route). It uses two read-only,
public endpoints: `GET api/elberfelder/search/verses` (verse full text search) and `GET api/elberfelder/search/originals`
(per query word: the mapped Greek/Hebrew words by frequency, from the roadmap 0008 snapshot). The old experiments
`search/german` and `fts/single` are gone.

- **Query syntax:** words are AND-ed (`ich bin der Weg`); `"in quotes"` is a phrase; a leading `-` excludes a word or
  phrase (`Gott -Herr`); `oder` / `or` separates alternatives (`Herr oder Jahwe`). At most 8 words and 200 characters.
  The search starts 300 ms after the last keystroke (at once on space or Enter), from 2 characters.
- **Full text configuration:** migration `0015_VerseSearch.sql` adds the text search configuration
  `"unshackled-word".uw_german` (German snowball stemmer, **no stop words**, so `ich bin` and `daß` work; `daß` = `dass`,
  `Füße` = `fuesse`) and rebuilds `Elb1871Verses."SearchVector"` with a GIN index. The query is built in C#
  (`VerseSearchQueryBuilder`), user text never reaches SQL as a tsquery.
- **Lemma widening:** migration `0016_VerseLemmaText.sql` adds `Elb1871Verses."LemmaText"` (the lemmas of a verse from
  `Elb1871Morphology`, indexed with weight B). A single query word is also matched by its lemma(s): `ging` finds
  forms of `gehen`; at most the 3 most frequent lemmas per word, never for phrases or excluded words. The overlay says
  which lemma was used. The lemma of a word comes from the snapshot of roadmap 0008, so before the first "Neu berechnen"
  the search is stem-only and after lemma edits it lags until the next refresh.
- **Relevance order:** literal stem hits (weight A) come before lemma-only hits (weight B), `ts_rank_cd`; ties are in
  Bible order. Hits are returned as word tokens with `matched` flags (no HTML), plus hits per book for the bar chart.
- **Where `LemmaText` is refreshed:** `"unshackled-word"."RefreshVerseLemmaText"(int[] hebRefIds)` recomputes the
  column for the given verses (`NULL` = all). The lemma editor (single save, chapter save) and the bulk edit
  transactions (apply and revert) call it in the same transaction as the lemma change. **Every new write path to
  `Elb1871Morphology` has to call it too, and the morphology seed import of roadmap 0010 (0010.4) must call
  `RefreshVerseLemmaText(NULL)` once after its import**; otherwise the search keeps stale lemmas.

See `docs/roadmaps/0009-smart-search.md` for the design and measurements.

### Web Frontend

With the API running open the `src\UnshackledWord.AstroWeb` and create an `.env` file in the root of the project with the following content:

```env
PUBLIC_API_DOMAIN=http://localhost:5142/api
```

Now run `npm install` and then `npm run dev` to start the frontend.

### BibleTagger

A basic .NET Blazor frontend.

```sh
dotnet tool install --global dotnet-ef
```
### Migrations

Create new EF migration in the right folder. Run this in `src\UnshackledWord.Tooling.BibleTagger`

```sh
dotnet ef migrations add InitialCreate --output-dir Data/Migrations
```

Optional flags can be used to only output specific migrations

```sh
--from NameOfBeginngingMigration --to NameOfAnotherMigration
```

Output EF Migration as output script so add manually to migration worker

```sh
dotnet ef migrations script --idempotent -o ..\UnshackledWord.Tooling.Postgres.Migration\scripts\OutputScript.ignore.sql
```
To migrate database directly within BibleTagger

```sh
dotnet ef database update
```

### Managing JS/CSS libraries

```sh
dotnet tool install -g Microsoft.Web.LibraryManager.Cli
```

```sh
cd src\UnshackledWord.Tooling.BibleTagger\wwwroot
# libman init
libman update bootstrap
```
