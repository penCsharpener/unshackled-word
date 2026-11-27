# Changelog 0.10.23 → 0.14.16

For a production install on **v0.10.23 / migration 0046** (state after the TSK curation roadmap 0024). Written 2026-11-27 from the git history.

The work sits on three lines of history. Nothing here is pushed to a release branch yet:

| Line | Versions | Migrations | Content |
|---|---|---|---|
| `main` | 0.10.24 → 0.12.11 | 0047 – 0049 | TSK flash, Greek editions (0025), similar verses (0027), migration worker hardening |
| `feature/lxx` (not in `main`) | 0.13.0 → 0.13.17 | 0050 – 0053 | Septuagint (0028) |
| `feature/morphology-search` (this branch, branched from `main`, not from `feature/lxx`) | 0.14.0 → 0.14.16 | 0054 – 0055 | Grammar search (0029) |

Going to 0.14.16 with the LXX included means merging `feature/lxx` first (the 0029 roadmap says so too). The version numbers already continue from 0.13.x, so expect a trivial `package.json` / `Directory.Build.props` conflict.

## Highlights

1. **Grammar search (0.14.x).** Search Greek, Hebrew/Aramaic and the German word by grammar in one German query language: `agape Kasus:nom`, `Tempus:aor Diathese:pass`, `bara Stamm:qal`, `dt.Verbform:finit`. Autocomplete, reader marks, a cheat sheet, and a forms block on the Strong's page.
2. **Septuagint (0.13.x, on `feature/lxx`).** Swete's LXX text in Rahlfs numbering, readable in the reader side panel, with a tagging and review workflow (prefill from NT forms, AI mode `lxx-tag`, review page `/edit/lxx`), links to the Hebrew, and LXX occurrences on the Strong's page.
3. **Similar verses (0.12.x).** A new side panel mode "Ähnliche Verse", computed from shared Strong's numbers (idf cosine plus a bigram bonus, per testament), with a maintainer card for status, rebuild and evaluation against TSK.
4. **Greek editions per word (0.11.x).** Which critical editions (22 catalog entries, e.g. Byz and TR) carry each Greek reading: a section in the word detail, dotted marks and an edition filter in the Grundtext panel.
5. **Deployment hardening (0.11.10).** The migration worker now has a 15 minute script timeout and exits non-zero on failure; compose waits for a database healthcheck and starts SeedDb, WebApi and McpServer only after a successful migration.
6. **Reader: TSK pills flash (0.10.24).** Hovering a word flashes the TSK pills whose target verses contain the same Strong's number (very frequent lemmas excluded).

## Upgrade notes

### Migrations (all idempotent or additive; none rewrites existing data)

| # | Script | What it does | Branch |
|---|---|---|---|
| 0047 | `GreekEditions` | Tables `GreekEditions` (catalog) and `StepGreekWordEditions` (word to edition, about 1.1M rows), SQL functions `RefreshStepGreekWordEditions()` and `StepGreekWordEditionTokens()`; the script backfills the table (about 20 s on the dev machine) | main |
| 0048 | `GreekEditionsCodeLength` | Widens `Code` and `ShiftRaw` to varchar(100) | main |
| 0049 | `VerseSimilarity` | Tables `VerseSimilarityRuns`, `VerseSimilarities` (derived data, not in the seed) | main |
| 0050 | `Lxx` | `LxxVerses`, `LxxWords` | feature/lxx |
| 0051 | `LxxTags` | `LxxWordTags`, `LxxHebrewLinks`, `LxxTagAudit` | feature/lxx |
| 0052 | `LxxAgentMode` | Agent run mode `lxx-tag` | feature/lxx |
| 0053 | `AgentRunLxxOptions` | Run options for LXX tagging | feature/lxx |
| 0054 | `GrammarSearchGreekIndex` | Index `IX_StepGreekWords_Morphology` on `("Morphology","LxxRefId")` | this branch |
| 0055 | `GrammarSearchHebrewIndex` | Index `IX_StepHebrewWords_Grammar` on `("Grammar","LxxRefId")` | this branch |

- **Index builds (0054/0055) block writes** to `StepGreekWords` / `StepHebrewWords` while they run. On the dev database they were blocked for hours by a stale `UPDATE` session. Before upgrading prod, look at `pg_stat_activity` for long-running sessions on these tables.
- **Without `feature/lxx`** the numbering has a gap (0050–0053). That works: DbUp runs every script not yet in its journal, and the migrations health check only requires the highest applied number (`ExpectedMigration = 55` on this branch) to be reached. It cannot detect a missing script in the gap, though, so merge `feature/lxx` before deploying if the LXX is wanted.
- Since 0.11.10 each script has a 15 minute timeout and a failed script makes the migration container exit with 1.

### SeedDb behaviour on an existing database

The seed still runs only once on a fresh database, apart from the gated steps below. On prod these steps will run at the first start after the upgrade:

- **Greek editions refresh:** runs because `StepGreekWordEditions` is empty while `StepGreekWords` is not (migration 0047 already backfills it, so this is usually a no-op).
- **Verse similarity rebuild:** runs once because `VerseSimilarities` is empty, after the STEP relationship import. About 12–15 s and about 300 MB peak memory on the dev machine (914,983 pairs). A failed rebuild is logged and does not stop the seed. Afterwards it only runs when STEP words were inserted, or when a maintainer starts it on the Wartung page.
- **LXX (feature/lxx):** downloads Swete's text (pinned commit of eliranwong/LXX-Swete-1930, downloaded, never redistributed) and imports it; confirmed tags and links come from `assets/seed/lxx` only while no tag is confirmed.

### Configuration and compose

- New config section `VerseSimilarity` (`StopDfShare` 0.03, `TopK` 30, `MinScore` 0.1, `PhraseWeight` 0.5) in the appsettings of SeedDb and WebApi; the defaults are fine.
- `compose.yml`: database healthcheck (`pg_isready`), `depends_on` with conditions (`service_healthy`, `service_completed_successfully`).
- **Uncommitted, not part of any version yet:** `compose.yml` additionally sets `ASPNETCORE_ENVIRONMENT=${ENVIRONMENT:-Production}` (ASP.NET reads the hosting environment only from this variable, so `ENVIRONMENT=Development` in `.env` used to be ignored and the API ran as Production) and `UNSHACKLEDWORD_URLS=http://+:8080`. Decide whether you want this in prod before committing.
- Prod `.env`: keep `ENVIRONMENT=Production` and `REQUIRE_SECURE_COOKIES=true` (see `.env.example`). `false` is only for plain-HTTP LAN testing.
- No new package versions in `Directory.Packages.props`; `src/UnshackledWord.Web/package.json` only changes its `version`.

### New public and maintainer surface

- Public (anonymous, under the general rate limit): `api/elberfelder/similarVerses/{bibleBookId}/{chapter}`, `api/elberfelder/similarVerses/verse/{hebRefId}`, `api/greekEditions`, `api/elberfelder/search/grammar` (+ `/chapter/{bibleBookId}/{chapter}`, `/neighbours`, `/vocabulary`, `/facets`), `api/elberfelder/search/verses/chapter` and `/neighbours`, `grammar` parameter on `strongsOccurrences`. On `feature/lxx`: `api/lxx/chapter`, `api/lxx/word/{id}`, `corpus=lxx` on `strongsOccurrences`.
- Maintainer only: `api/maintainer/verse-similarity` (+ `/rebuild`, a background job with an advisory lock). On `feature/lxx`: `api/maintainer/lxx/*` (status, prefill, review, lexicon, morphology, seed).
- Data quality (Wartung): new checks for unknown Greek editions and for words without an edition row.
- Bruno collection extended for all new endpoints.

## Per release

### 0.10.24 — Reader: TSK pills flash (2026-11-25)

Hovering a word flashes the TSK pills whose target verses contain an original word with the same Strong's key. `crossReferenceTexts` returns the distinct Strong's keys per target verse, without very frequent lemmas (`StatsRules.OriginalHighFrequencyThreshold = 1000`, matched by base number).

### 0.11.x — Greek editions per word (roadmap 0025, 2026-11-25)

- **0.11.0 – 0.11.2:** catalog and n:m table (migration 0047), SeedDb refresh after the Greek STEP import, data quality checks.
- **0.11.3 – 0.11.4:** catalog endpoint; editions per Greek reading in the word detail, with the `EditionList` section.
- **0.11.5 – 0.11.7:** edition codes and shift flag on the Greek words of the chapter originals; dotted mark and an edition note for words not in all editions in the Grundtext panel; an edition filter in the reader side panel (choice kept in `localStorage` `uw.reader.greekEdition`).
- **0.11.8 – 0.11.9:** fresh-database check (1,100,328 rows, 22 catalog rows, none unknown), review fixes (refresh only when Greek words were inserted or the table is empty, migration 0048, wording of the note for the 11 words in no main edition).
- **0.11.10:** migration worker hardening and compose startup order (see Highlights).

### 0.12.x — Similar verses (roadmap 0027, 2026-11-25)

- **0.12.0 – 0.12.1:** Strong's token loader, scorer and the `verse-similarity` SeedDb verb (`--dry-run`, parameter overrides); migration 0049, atomic rebuild (one transaction, advisory lock) and the SeedDb step.
- **0.12.2 – 0.12.5:** public read endpoints with explanations (shared Strong's, shared phrases, German word positions); side panel mode "Ähnliche Verse" (`G` cycles modes), with the shared words highlighted in the hit and in the source verse.
- **0.12.6 – 0.12.9:** maintainer status and rebuild endpoints; "Ähnliche Verse" card on Wartung with last run, parameters and evaluation; TSK is used as an evaluation baseline per run, defaults tuned.
- **0.12.10 – 0.12.11:** docs, fresh-database check, review fixes.
- Result on the dev database: 914,983 pairs (OT 680,025, NT 234,958), at most 30 per verse, no cross-testament pairs.

### 0.13.x — Septuagint (roadmap 0028, 2026-11-26, branch `feature/lxx`)

- **0.13.0 – 0.13.2:** Swete and TVTMS parsers, `lxx-check` verb, the Swete-to-Rahlfs exception list (`assets/seed/lxx/swete-to-rahlfs.csv`, 133 rows) and the versification chain; migration 0050 and the text import.
- **0.13.3 – 0.13.6:** `api/lxx/chapter`; side panel mode "Septuaginta (Swete)"; tag tables, morphology validator, LXX word detail and word card in the reader.
- **0.13.7 – 0.13.12:** prefill pass from unambiguous NT forms and the "LXX" card on Wartung; AI tagging mode `lxx-tag` (prompt `lxx-tag-v1`); review backend and the review page `/edit/lxx`.
- **0.13.13 – 0.13.15:** seed export/import of confirmed tags and links (`assets/seed/lxx`); LXX occurrences on the Strong's page; Hebrew links in the chapter read and hover marking.
- **0.13.16 – 0.13.17:** docs, fresh-database check, review fixes.
- Open: the first real (paid) `lxx-tag` run needs the maintainer's approval. Tags are proposals until confirmed at `/edit/lxx`; the public sees confirmed tags only.

### 0.14.x — Grammar search (roadmap 0029, 2026-11-26 to 2026-11-27)

- **0.14.0 – 0.14.3:** Greek grammar filters on the search API (migration 0054), chapter matches and hit neighbours, a vocabulary endpoint with a label sync test (labels must equal `de.ts`), facet counts per value.
- **0.14.4 – 0.14.8:** grammar mode in the Grundtext tab; the query stays in the box and marks hits in the reader (`SearchMarkBar`); Hebrew and Aramaic filters (migration 0055, root segment only); autocomplete with lemma suggestions and counts; Hebrew in the search overlay and reader.
- **0.14.9 – 0.14.14:** German side with the `dt.` prefix (`Elb1871Morphology`, German lemmas; `dt.Kasus` withheld because the German case tags are wrong too often, `dt.Numerus`/`dt.Geschlecht` reliable on nouns only); grammar filter on Strong's occurrences; reader marks for verse-search queries; forms block ("Formen") and grammar filter on the Strong's page; `dt.` in the overlay; cheat sheet in the search overlay.
- **0.14.15 – 0.14.16:** docs, fresh-database check (identical results to the dev API), review fix for the `keyNotInLanguage` message.
- Measured (dev): `agape Kasus:nom` 36 words in 32 verses, `Tempus:aor Diathese:pass` 1,706 in 1,451, `Kasus:nom` without a lemma 47 ms.

## Not fixed / known issues

- **Seed failure that exits 0:** on a fresh database the first SeedDb run failed in `ElberfelderTextRunner` with an Npgsql read timeout (default 30 s on the word bulk insert into a cold bind-mounted Postgres) and still exited 0. With `Command Timeout=900` in the connection string it passes. Not relevant to an upgrade of an existing database.
- **Hebrew lexicon gaps:** the old dev database lacks 542 Hebrew lexicon numbers (H1254 among them), so `bara`/`H1254` are `unknownWord` there. Fresh databases have them (importer fixed in 0022.1b). Check prod; the fix is the SeedDb verb `step-lexicon-repair`, then an API restart.
- **Agent-prompt tests:** `AgentPromptProviderTests` and `AgentPromptExampleTests` (greek-v2) fail in Application (1,400 of 1,402 pass). `assets/agent-prompts/greek-v2.md` is modified and uncommitted.
- **Not done:** roadmap 0026 (morpheme alignment) is deferred; no code. The English keys and values for grammar search, word order and distance between slots, and Hebrew prefix/suffix filters are out of scope for 0029.
- **Frontend tests:** the web test fixes (jsdom under Node 24 in the Dockerfile, the TSK and NoteReview tests) are uncommitted, so they are in no version yet. After them the full web check passed (154 files, 1263 tests).
