> **Update 2026-11-15:** `RevisionLevels` became `Revisions` (own id, `RevisionVerses.RevisionId` replaces `Level`, script `0021_Revisions.sql`). The base Elb1871 now sits at level 200 together with Elberfelder 2027 (2027 wins where it has a variant); Extra is level 100. Level 0 is the API pseudo level "Elb1871 only". Levels of revisions with variants are editable. Text below that says "Elb1871 = 100" is outdated.

# Roadmap 0011: Bible revision (verse variants per literalness level, unified revision mapping, reader slider)

Replaces the draft notes of 2026-11-13. Supersedes the revision layer that roadmap 0005 designed but never built
(`GermanVersions`, `GermanEdits`, `GermanEditWords`); fills the seed slot roadmap 0010 reserved (`assets/seed/revision/`).

## Goal

Elb1871 stays untouched. Next to it grows a revision: for any verse the maintainer can write one variant per
literalness level. A variant is a full copy of the verse (text, words, mapping to the original), so replacing two words
by five or five by two is never a special case. The public reader shows, per verse, the variant that fits the reader's
level and falls back to Elb1871 wherever no variant exists; a slider moves the level from wooden-literal to simple.

What exists today (code and migrations, 2026-11-13):

- The base text is `Elb1871Words` (722,753 rows on the maintainer's DB) and `Elb1871Verses`. Nothing can express
  "this verse reads differently"; there is no revision table of any kind.
- The mapping is two edge tables, `Elb1871GreekMapping` (170,071 rows) and `Elb1871HebrewMapping` (556,416 rows), keyed
  by `ElbWordId`. Every reader query exists twice (`VerseMappingSql` has a Greek and a Hebrew twin of each statement).
  A revised verse has new words, so it cannot reuse these rows.
- Roadmap 0005 (Mapping v2) contains a span-based revision design (replace positions From..To at a level). 0 of its 22
  tasks are built. This roadmap does not wait for it and does not use its revision tables.
- The level scale is stated three different ways: 1–10000 (CLAUDE.md, README), 0–10000 with Elb1871 = 0 (0005), and
  1–1000 with Elb1871 = 100 (the draft). The draft wins (decision below); the other documents are corrected in 0011.14.
- The reader (`ChapterPage`) loads `chapterWords` / `chapterText` without any level; the eye button cycles original
  text → mapping editor. Word ids in the reader are `Elb1871Words."Id"`.
- Writes: the lemma editor uses `Roles(Admin, Maintainer)`; the mapping editor's save is still `AllowAnonymous` behind
  the `MappingEditing:Enabled` setting.

End state: a maintainer hovers a verse, clicks the pen, rewrites the verse at a level, saves, and lands in the mapping
form with everything that could be carried over from the original already linked. Readers see the revised verse with a
marked left border, the original-language view and the word popup work on it, the search finds it, and
`export-seed` writes it into the repo so a fresh `docker compose up` has it.

Out of scope (see "Deferred"): suggestions by other users and a review queue, drafts, AI mapping of revised verses,
German lemma/morphology for new words, stats on revision text, changing verse boundaries, roadmap 0005 itself.

## Decisions

| Topic | Decision | Source |
|---|---|---|
| Base text | Elb1871 is never changed. The revision is a fork: everything comes from Elb1871 unless a variant exists | draft |
| Unit | A modification copies the whole verse; words are added/removed freely and the mapping is readjusted on the copy | draft |
| Level scale | 1–1000. 1 = extremely wooden, 1000 = toddler language, Elb1871 = 100. Replaces 1–10000 (CLAUDE.md, README) and 0–10000 (0005) | draft, maintainer 2026-11-13 |
| Several variants per verse | A verse may exist once per level; the level is part of the key | draft |
| Revision of a revision | A new variant can start from an existing variant instead of from Elb1871; it copies that variant | draft |
| Reader | The public view prefers the revision. A slider sets the level without showing the number; changing it reloads the chapter | draft |
| Marking | Revised verses have a marked left border in the reading view | draft |
| Editing entry | Pen icon next to the eye icon on verse hover; the form opens below the verse like the original-language panel | draft |
| Save flow | Save text → split by spaces → store `WordInContext` (with punctuation) and `PlainWord` (without), as in `Elb1871Words` → form switches to the mapping form with as much carried over from the original as possible | draft |
| Stats | Stay on Elb1871 for now (`DqWordFacts`, `Stats*`, Strong's renderings) | draft |
| Relation to 0005 | Standalone. 0011 ships its own tables; legacy mapping tables stay untouched; 0005's `GermanVersions`/`GermanEdits`/`GermanEditWords` are superseded by this roadmap | maintainer 2026-11-13 |
| Mapping table | One unified `RevisionMapping` for Greek and Hebrew (evaluation in "Design") | maintainer 2026-11-13 (recommendation accepted) |
| Who writes | Admin and maintainer only (`Roles(AppRoles.Admin, AppRoles.Maintainer)`). A save is live immediately. Every text save is a new version with author and timestamp; old versions are kept | maintainer 2026-11-13 |
| In scope besides the editor | Revision seed export/import (0010 keys and anchoring) and search over revision text | maintainer 2026-11-13 |
| Stack and conventions | Dapper only, numbered schema-only migration, seed data as CSV, FastEndpoints, React SPA with design tokens, i18n de/en, `Components.md` | CLAUDE.md, 0010 |

Architect's assumptions (not stated by the maintainer; the tasks follow them, "Open questions" lists the ones worth a
second look):

| Topic | Assumption |
|---|---|
| Levels are presets | A level must exist in `RevisionLevels` before a verse can use it. The slider has one stop per preset. Free numbers would give levels 150, 151, 160 that differ in three verses each |
| Which variant a level shows | Start at Elb1871 (100) and walk towards the slider value; the last variant passed wins. Slider 600 with variants at 200 and 400 shows 400; slider 150 shows Elb1871; slider 50 with a variant at 20 shows Elb1871, slider 10 shows the variant at 20 |
| Default level | One preset is flagged `IsDefault`; the reader starts there. API calls without `level` get Elb1871 (100), so the MCP server, Bruno and old frontends see no change |
| Word identity | Revision words refer to their base word by `(HebRefId, PositionInVerse)`, never by `Elb1871Words."Id"` (ids are not portable, 0010). The mapping refers to the original word by `StepWordId` like the legacy tables; the seed uses `SrcRef`/`SrcPos`/`SrcHash` |
| Mapping history | Text versions are kept. Mapping saves replace the mapping rows of the active version (as the legacy editor does) and stamp who/when; no per-save mapping history until 0005 |
| Verse boundaries | A variant belongs to exactly one Elb1871 verse (`HebRefId`). Splitting, merging or renumbering verses is not possible |

## How to use this roadmap

Work in table order or as *Depends on* allows; one task = one commit. A slice is the backend row plus its `f` row(s) of
the same number; both land before the slice counts as done, and every slice leaves `dotnet build UnshackledWord.slnx`,
`dotnet test` and `docker compose run --rm --no-deps unshackledword.web bun run check` green. Conventions of 0004
("Conventions for all steps") apply. DB experiments on a scratch DB (`postgres-test-db` or a throwaway `postgres:18`),
never on the shared DB copy. Rows without UI have no `f` row; their "Done when" is a command whose output can be
checked. Bruno files are updated in the slice that adds or changes a contract.

New components, approved by the maintainer on 2026-11-13 (CLAUDE.md asks for that before building): `LevelSlider`
(0011.3f2), `RevisionEditor` (0011.2f), `VersionHistory` (0011.8f); register each in `Components.md` in its slice.
Everything else reuses `MappingEditor`, `OriginalVerse`,
`Select`, `TextField`, `Button`, `SaveBar`, `Dialog`, `StatTile`, `FindingsTable`.

## Tasks

| ✔ | # | Task | Depends on | Done when |
|---|---|---|---|---|
| [ ] | 0011.1 | Schema-only migration `scripts/0020_BibleRevision.sql` exactly as in "Design → Migration" (`RevisionLevels`, `RevisionVerses`, `RevisionWords`, `RevisionMapping`, indexes, comments; no rows). Level presets as seed: `assets/seed/revision/levels.csv` (`Level,Code,NameDe,NameEn,DescriptionDe,DescriptionEn,IsDefault`) with `100,elb1871,…` and `200,revision,…,true`; `RevisionLevelSeedRunner` in SeedDb directly after `SttsSeedRunner`, only into an empty table, sets `IsBase` for level 100. Dbos in `Domain/Models/Dbo/Revision`. Endpoint folder `Endpoints/Revision/` with `RouteGroupConfig` (prefix `revision`) and `GET api/revision/levels` (anonymous; `level`, `code`, `name` in the request language, `isBase`, `isDefault`, sorted by level) | – | Fresh scratch DB: DbUp runs 0001–0020; the script runs twice without error and inserts no rows; on the maintainer's copy `\d` shows the four tables and nothing else changed; after `dotnet run --project src/UnshackledWord.Tooling.SeedDb` `GET api/revision/levels` returns 100 (base) and 200 (default); a second seed run imports nothing |
| [ ] | 0011.2 | Text write path. `RevisionTokenizer` in `Application/Features/Revision` (pure C#): collapse whitespace, split by spaces, `WordInContext` = token as typed, `PlainWord` = the rule `ElberfelderParser` uses for `Elb1871Words."PlainWord"` (extract that rule into one shared function, do not copy it). `RevisionRepository` + `RevisionService.SaveTextAsync`: in one transaction end the active variant of `(HebRefId, Level)` if any, insert `RevisionVerses` with `Version` = last + 1 and `BaseTextHash`, insert `RevisionWords` (all `Change = 'new'` until 0011.4). `POST api/revision/verse/bookId/{b}/chapterId/{c}/verseId/{v}` body `{ level, text, expectedVersion?, note? }`, `Roles(Admin, Maintainer)`; validator: level exists and is not 100, text 1–2000 chars after trim, at most 300 tokens; `409` when `expectedVersion` is not the active version; `404` when the verse does not exist. `GET api/revision/verse/bookId/{b}/chapterId/{c}/verseId/{v}` (anonymous): Elb1871 text plus every active variant (`revisionVerseId`, `level`, `version`, `text`, `createdAt`, `createdBy` display name, `mappingReviewedAt`) | 0011.1 | Tokenizer test: tokenising `Elb1871Verses."VerseText"` of Gen 1, Ps 119 and Joh 1 reproduces the `WordInContext`/`PlainWord` rows of `Elb1871Words` (any verse that does not is listed in "Background" with the reason); service tests with FakeDbReader for version increment, 409 and 404; Bruno: POST as maintainer returns 200, as `user` 403, anonymous 401 |
| [ ] | 0011.2f | `ChapterPage`: pen icon button next to the eye button, same visibility rule as the eye (hover; always visible on coarse pointers), rendered only for `hasWriteRole`. It opens a third panel state `verseView = "revise"` below the verse with the new `RevisionEditor`: level `Select` (presets except 100), multi-line text field prefilled with the active variant of that level or else the Elb1871 text, optional note, save/cancel. After saving, the panel shows the saved variant and its version. API client in `lib/revision/api.ts`, types in `lib/revision/types.ts`, i18n de/en | 0011.2 | A maintainer opens Joh 3:16, changes `eingeborenen` to `einzigen` at "Revision", saves, reloads the page, opens the pen again: the field holds the revised text, version 1. A signed-out visitor sees no pen. Vitest: prefill from base, prefill from variant, 409 shows "inzwischen geändert, neu laden" |
| [ ] | 0011.3 | Read path. Optional query parameter `level` (1–1000, default 100) on `GET elberfelder/chapterWords/…` and `GET elberfelder/chapterText/…`. One shared SQL fragment `RevisionSql.EffectiveVerses` ("Design → Effective variant") decides per verse; base verses come from `Elb1871Words` as today, revised verses from `RevisionWords` + `RevisionMapping` (empty until 0011.4). Response additions: `revisedVerses: [{ verse, level, revisionVerseId }]`; words of revised verses carry `revisionWordId` and `id` = the base word id when `BasePositionInVerse` is set, else 0. Replace the string-interpolated book/chapter in `GetWordsForChapter` by parameters while touching it | 0011.2 | Endpoint tests: level 100 or no parameter returns byte-identical JSON to before for a chapter with a variant; level 200 returns the revised words for the one revised verse and base words for all others; level 150 returns base words; scratch DB: `EXPLAIN ANALYZE` of the chapter query at level 200 for Ps 119 stays under 20 ms |
| [ ] | 0011.3f | Reader loads `levels` once, takes the level from `localStorage` (`uw.readerLevel`, try/catch) or else the `isDefault` preset, and passes it to `chapterWords`/`chapterText` (level is part of the query keys). Revised verses get the marked left border (existing accent border token, no new colour) and `title`/`aria-label` "revidiert: <level name>". Word keys become `r<revisionWordId>` / `b<id>` | 0011.3 | Joh 3 at the default level shows verse 16 with the border and the revised wording, all other verses unchanged; Vitest for border on/off and for the fallback when `localStorage` throws |
| [ ] | 0011.3f2 | New `LevelSlider` in the reader header next to the chapter picker: one stop per preset, no number shown, the level name as tooltip and `aria-valuetext`, keyboard operable, hidden when fewer than two presets exist. Changing it stores the level and refetches the chapter | 0011.3f | Dragging from "Revision" to "Elberfelder 1871" turns Joh 3:16 back into the Elb1871 wording and removes the border without a page reload; the choice survives a reload; usable at 375 px width |
| [x] | 0011.4 | Mapping carry-over. `RevisionCarryService` ("Design → Carry-over"): align the tokens of the parent (Elb1871 words, later a parent variant) with the new tokens using `SequenceAligner` from `Application/Features/Seed/Anchoring`, set `RevisionWords."BasePositionInVerse"` and `"Change"`, copy the parent's mapping rows into `RevisionMapping` with `Origin = 'carried'` (parent rows read through the existing `VerseMappingRepository.GetMappingRowsAsync`). Runs inside `SaveTextAsync`. `POST revision/verse` now returns `revisionVerseId` plus a carry report (`same`, `changed`, `new` German words; original words that lost their link). `GET api/revision/verseMapping/{revisionVerseId}` (anonymous) returns the existing `GetVerseMappingResponse` shape: `germanWords[].id` and `links[].elbWordId`/`parentElbWordId` hold `RevisionWords."Id"`, new fields `revisionVerseId`, `level`, `links[].origin`, `germanWords[].change` | 0011.3 | Unit tests per rule: unchanged verse (all `same`, row count equals the parent's), one word replaced, word inserted, word deleted, two words → one, one word → two, word moved, added word whose parent was deleted, compound parts kept. Scratch DB: revising Gen 1:1 with only a comma removed yields the same number of mapping rows as `Elb1871HebrewMapping` has for the verse |
| [x] | 0011.4f | After the text save the `RevisionEditor` panel switches to `MappingEditor` in a read-only pass first: it shows the revision's German words against the original words with the carried links; `changed` and `new` words and original words without a link carry a "prüfen" marker; the carry report is one line above ("12 übernommen, 2 geändert, 1 neu, 1 Originalwort ohne Zuordnung"). `MappingEditor` gets the data through props (no second editor component) | 0011.4 | After the Joh 3:16 save the mapping form appears without a further click, `einzigen` is linked to μονογενῆ and marked "prüfen", all other words are linked as in Elb1871; Vitest with a fixture for each marker |
| [x] | 0011.5 | `PUT api/revision/verseMapping/{revisionVerseId}`, `Roles(Admin, Maintainer)`: same link validation as `VerseMappingService.SaveAsync` (German ids belong to this variant, original ids belong to the verse, added-word rules), replace the variant's `RevisionMapping` rows in one transaction with `Origin = 'manual'` for rows that differ from the carried ones, set `MappingReviewedAt`/`MappingReviewedByUserId`; `409` when the variant is no longer active. Not behind `MappingEditing:Enabled` (roles are the gate) | 0011.4 | Service tests: foreign word id rejected, inactive variant 409, unchanged carried rows keep `Origin = 'carried'`; scratch DB: after the PUT `GET revision/verseMapping` returns exactly the saved links and `mappingReviewedAt` is set |
| [x] | 0011.5f | The mapping pass of 0011.4f becomes editable and saves through the PUT. The eye button on a revised verse shows `OriginalVerse` and the mapping from `revision/verseMapping` (hover highlighting between German and original words works as on base verses); for writers the eye's mapping step edits the revision mapping, the legacy editor is reachable only at level 100 | 0011.5 | The maintainer unlinks `einzigen`, links it again, saves, reloads: the link is there and the "prüfen" marker is gone. A visitor hovers a revised word: its original word lights up in the eye panel |
| [x] | 0011.6 | `GET api/revision/wordDetail/wordId/{revisionWordId}` (anonymous) returning the existing `GetWordDetailResponse`: readings from `RevisionMapping` → STEP (reuse the reading builders of `GetWordDetail`, parameterised by the mapping source), `germanLemma` from `Elb1871Morphology` only when `Change = 'same'`, `coMappedWords` within the variant, renderings/occurrence counts unchanged (they are Strong's stats of Elb1871), `mapping.source` from `Origin` | 0011.5 | Contract test against the Joh 3:16 fixture: `einzigen` returns the reading G3439 with gloss and renderings, an inserted word returns `readings: []`; a `same` word returns the same readings as its base word's `wordDetail` |
| [x] | 0011.6f | `WordDetailPopover` and `HeaderWordInfo` call the revision endpoint for words with `revisionWordId`; the popover shows a small "revidiert" note and, for `changed` words, the Elb1871 word it replaced | 0011.6 | Clicking `einzigen` at the default level opens the popover with μονογενής and "Elb1871: eingeborenen"; clicking the same position at level 100 opens the old popover unchanged |
| [x] | 0011.7 | Several levels and revision of a revision. `POST revision/verse` gets `basedOnLevel` (default 100): the text is not copied server-side (the client sends it) but the carry-over uses that variant as parent and `BasedOnRevisionVerseId` records it. Level management: `POST api/revision/levels`, `PUT api/revision/levels/{level}` (`Roles(Admin, Maintainer)`; code/level unique, level 100 not editable, level value immutable once a variant uses it, exactly one default) | 0011.4 | Tests: variant at 600 based on the 200 variant carries the 200 variant's manual links (not Elb1871's); a second default is rejected; changing the level value of a used preset is rejected |
| [x] | 0011.7f | `RevisionEditor`: the level `Select` lists the levels that already have a variant first ("Revision · v3"), then the others, plus "Neue Stufe…" (small `Dialog`: level 1–1000, code, names); a second `Select` "Ausgangstext" (Elberfelder 1871 or any existing variant of the verse) refills the text field after a confirm when the field was edited. `MaintainerPage`: section "Stufen" to rename presets and set the default | 0011.7 | The maintainer adds the preset 600 "Einfach", writes Joh 3:16 at 600 starting from the 200 variant; the slider now has three stops and each shows its own wording; Elb1871 text is still what level 100 shows |
| [x] | 0011.8 | History and withdrawal. `GET api/revision/verse/…/history?level=` (writers): all versions of the level, newest first, with text, author, note, `createdAt`, `endedAt`. `DELETE api/revision/verse/{revisionVerseId}` (writers): ends the active variant without a successor (the level falls back for that verse); rows are never deleted. Restoring an old version is a normal POST with that text | 0011.2 | Tests: delete sets `IsActive = false`/`EndedAt`/`EndedByUserId` and the read path at that level returns the next variant towards 100; a later POST continues with `Version` + 1 |
| [x] | 0011.8f | New `VersionHistory` inside the `RevisionEditor` panel (collapsed by default): versions with a word-level diff against the previous one (diff computed client-side from the two texts), "wiederherstellen" (prefills the field) and "Revision dieser Stufe entfernen" with a confirm `Dialog` | 0011.8 | After three saves of Joh 3:16 the history lists v3, v2, v1 with the changed words marked; restoring v1 creates v4 with v1's text; removing the variant brings the Elb1871 wording back at that level |
| [x] | 0011.9 | `GET api/revision/overview` (`Roles(Admin, Maintainer, Reviewer)`): per level the number of revised verses per book; lists (paged) of variants with `MappingReviewedAt IS NULL`, variants with German words or original words without a link, and variants whose `BaseTextHash` no longer matches the Elb1871 verse | 0011.5 | Repository tests with FakeDbReader for each list; scratch DB: one unreviewed and one stale-hash variant show up in the right list |
| [x] | 0011.9f | `MaintainerPage` section "Revision": `StatTile` per level (revised verses / total), `FindingsTable` for the three lists with a jump that opens the verse in the reader with the pen panel open (`?revise=<level>`) | 0011.9 | A click on an "ungeprüfte Zuordnung" row opens the chapter scrolled to the verse with the mapping form open; i18n de/en |
| [x] | 0011.10 | Search over revision text. `GET elberfelder/search/verses` gets `level` (default 100). `VerseSearchRepository`: a verse is matched against its effective text at that level (`RevisionVerses."SearchVector"`, partial GIN index on active rows) and against the Elb1871 text; one hit per verse, the effective variant preferred; hit fields `revised: true`, `level`. Lemma widening stays Elb1871-only (no lemmas for revision words) | 0011.3 | Tests: `einzigen` at level 200 finds Joh 3:16 flagged `revised`; at level 100 it does not; `eingeborenen` at level 200 still finds the verse (base text) and is not flagged; ranking of base-only chapters is unchanged at level 100 |
| [x] | 0011.10f | `SearchOverlay` / `VerseHitList` pass the reader's level; revised hits get the left-border marker and show the revised wording | 0011.10 | Searching `einzigen` in the overlay lists Joh 3:16 with the border; Enter opens the chapter at the verse |
| [~] | 0011.11 | Revision export. `export-seed --only revision` and kind `revision` in `Maintainer/ExportSeed` (also part of `all`): `assets/seed/revision/levels.csv`, `assets/seed/revision/verses/NN-Book.csv` (`Ref,Level,BasedOnLevel,BaseHash,Text,Note`; active variants only, sorted by `Ref,Level`), `assets/seed/revision/mapping/NN-Book.csv` (`Ref,Level,Pos,Form,Part,SrcRef,SrcPos,SrcHash,Added,ParentPos,ParentForm,Origin`, the mapping format 2 of 0010.8 plus `Level` and `Origin`); manifest entries with row counts and hashes. No ids, users, timestamps or STEP text | 0011.7 | Export on a scratch DB with variants at two levels writes only the books that have variants; `rg -n '[\p{Greek}\p{Hebrew}]' assets/seed/revision` is empty; a second export without changes is byte-identical; one changed word changes one line in `verses/` and the affected lines in `mapping/` |
| [~] | 0011.12 | Revision import. `RevisionSeedRunner` in SeedDb after `AiMappingImportRunner`: into empty `RevisionVerses` it re-creates each variant by running the same tokenizer and carry-over as the editor (words and `BasePositionInVerse` are derived, not stored in the seed), then replaces the carried mapping by the seed's mapping rows, resolving `SrcRef`/`SrcPos`/`SrcHash` with the 0010.9 rules (exact, n-th occurrence of the hash, aligner, else orphan); `CreatedByUserId` NULL = from seed; `SeedImports` row (`Kind = 'revision'`) and report `temp/seed-report/revision-<version>.csv`. Later starts: per `(verse, level)` replace only variants unchanged since the last import (`SeedVerseState`, `Kind = 'revision-<level>'`), keep and report the others; setting `AppSettings:DatabaseSeeding:UpdateRevisionSeed` (default `true`) | 0011.11 | Fresh compose stack with a committed test seed: the variants read as on the source DB, `SeedImports` shows the counts, orphans are in the report; start with a newer seed after editing one variant locally: the others update, the edited one is kept and reported; start again: nothing changes |
| [x] | 0011.12f | Data-quality overview seed line extended by the revision import/update counts (`seedImports` already carries `Kind`) | 0011.12 | Vitest with and without a revision import, with 0 and > 0 orphans; i18n de/en |
| [~] | 0011.13 | Rebase. The SeedDb verb `rebase` (0010.11) exports and re-imports the revision together with morphology and mappings; after the rebuild, variants whose `BaseTextHash` differs from the new Elb1871 verse are listed in the report (they stay active: the variant's own text did not change) | 0011.12 | Scratch copy with one variant: change a word of that verse in the theword export, run `rebase --confirm-backup`: the variant is still there with its mapping, and 0011.9's "Basistext geändert" list shows it |
| [~] | 0011.14 | Documents and final check. CLAUDE.md and README: level scale 1–1000 with Elb1871 = 100, "per-version mapping tables" → verse variants per level; 0005: mark the "Revision layer" decision, `GermanVersions`/`GermanEdits`/`GermanEditWords` (A.5, A.8, A.9) and the `Level` columns as superseded by 0011; 0010: key table row "Revision edits" → the seed format of 0011.11; README "Seed" section: revision files and update rule; `Components.md` lists the three new components | 0011.1–0011.13 | `rg -n '10000' CLAUDE.md README.md` returns nothing about levels; export from the copy → fresh compose stack → export again gives identical revision files; a reader on the fresh stack sees the revised Joh 3:16 with border, popup, original view and search hit |

Ordering: 0011.1 → 0011.2 → 0011.3 is the spine (write, then read). 0011.4–0011.6 make the mapping work; until 0011.4f
is done a revised verse has no links, so do not revise real verses on the maintainer's DB before 0011.5f. 0011.7–0011.10
are independent of each other. The seed strand (0011.11 → 0011.12 → 0011.13) can start after 0011.7 and must be done
before real revision work begins, otherwise that work exists only in one database.

## Design

### Model

```
RevisionLevels (Level PK: 100 = Elb1871 base, 200 = revision (default), later 600 …)
      │
      ▼
RevisionVerses (HebRefId, Level, Version, VerseText, IsActive, BasedOnRevisionVerseId ─┐ self: revision of a revision
      │         one ACTIVE row per (HebRefId, Level); older versions stay, IsActive = false)  ◄┘
      ▼
RevisionWords (RevisionVerseId, PositionInVerse, WordInContext, PlainWord,
      │        BasePositionInVerse → Elb1871Words by (HebRefId, PositionInVerse), Change same|changed|new)
      ▼
RevisionMapping (RevisionWordId, Lang G|H, StepWordId → StepGreekWords | StepHebrewWords,
                 IsAddedWord, ParentRevisionWordId, GermanWordPart, Origin carried|manual|seed)

Untouched: Elb1871Words, Elb1871Verses, Elb1871Morphology, Elb1871GreekMapping, Elb1871HebrewMapping, DqWordFacts, Stats*
```

The draft's "only the additional level makes `HebRefId` and `PositionInVerse` unique" holds for what readers see:
among active variants `(HebRefId, Level, PositionInVerse)` is unique. Because versions are kept, the stored key of a
word is `(RevisionVerseId, PositionInVerse)` and the level sits on the verse row.

Why whole-verse copies and not 0005's spans (`GermanEdits` From..To):

- A span model needs overlap rules between levels (0005 used a GiST exclusion constraint and a "must cover the lower
  span" rule) and a merge step on every read. A copy needs neither: the read is one `LATERAL` pick per verse.
- Word order changes, which modernising German does constantly, are not spans. A copy does not care.
- The cost is duplication: a full revision of all verses at one level is about one more `Elb1871Words` (722k rows) plus
  its mapping (~730k rows). That is small for Postgres and matches "scale stays small".

### One mapping table or two

| | Unified `RevisionMapping` (chosen) | `RevisionGreekMapping` + `RevisionHebrewMapping` |
|---|---|---|
| SQL | Each statement once, `Lang` as parameter | Each statement twice, as `VerseMappingSql` today (Greek/Hebrew twins for read, delete, insert) |
| Endpoints / repository | One | One with `hebrew ? a : b` switches |
| Seed format | One file layout | One, but two import paths |
| Integrity | `StepWordId` cannot be a foreign key to two tables; neither can it today (legacy tables have no FK because STEP tables are re-imported) | Same, no FK in practice |
| Id collision | `StepGreekWords` and `StepHebrewWords` both start at 1, so `Lang` must be in every join and in the index | Not an issue |
| Mixing languages in a verse | Impossible by data: `Lang` follows the book (1–39 `H`, 40–66 `G`); the service sets it, never the client | Impossible by table |
| Later (0005) | Already the shape of `MappingLinks` with `SourceTextId`; one table to migrate | Two |

Unifying makes things simpler. The only real cost is the discipline of always joining on `(Lang, StepWordId)`, which
the single repository hides. `StrongsNumber` (stale in 3,113 legacy Greek rows) and the copied `HebRefId` /
`PositionInVerse` columns are not carried into the new table; they are read through the joins.

### Migration `0020_BibleRevision.sql`

Schema only (0010 decision: migrations carry no data). Idempotent like the existing scripts.

```sql
-- Bible revision (roadmap 0011): verse variants per literalness level on top of the untouched Elb1871 text, their words and one unified
-- mapping table for Greek and Hebrew. Schema only: level presets come from assets/seed/revision/levels.csv (RevisionLevelSeedRunner),
-- variants from the editor or from assets/seed/revision (RevisionSeedRunner).
--
-- Levels: 1 = extremely wooden, 100 = Elb1871 (the base, never stored as a variant), 1000 = toddler language.
-- No foreign keys to Elb1871Words / Elb1871Verses / Step*Words: those tables are rebuilt by the seed (roadmap 0010 "rebase"), so the revision
-- refers to them by natural keys ("HebRefId", "PositionInVerse") and, like the legacy mapping tables, by "StepWordId".

CREATE TABLE IF NOT EXISTS "unshackled-word"."RevisionLevels"
(
    "Level"         int4         NOT NULL,
    "Code"          varchar(30)  NOT NULL,
    "NameDe"        varchar(100) NOT NULL,
    "NameEn"        varchar(100) NOT NULL,
    "DescriptionDe" text         NULL,
    "DescriptionEn" text         NULL,
    "IsBase"        boolean      NOT NULL DEFAULT false,
    "IsDefault"     boolean      NOT NULL DEFAULT false,
    CONSTRAINT "RevisionLevels_PK" PRIMARY KEY ("Level"),
    CONSTRAINT "RevisionLevels_Code_UQ" UNIQUE ("Code"),
    CONSTRAINT "RevisionLevels_Level_CK" CHECK ("Level" BETWEEN 1 AND 1000),
    CONSTRAINT "RevisionLevels_IsBase_CK" CHECK ("IsBase" = ("Level" = 100))
);

-- At most one preset is the level the public reader starts with.
CREATE UNIQUE INDEX IF NOT EXISTS "RevisionLevels_Default_UQ" ON "unshackled-word"."RevisionLevels" ("IsDefault") WHERE "IsDefault";

COMMENT ON TABLE "unshackled-word"."RevisionLevels" IS 'Literalness presets = stops of the reader slider. 100 is Elb1871 (IsBase); a variant can only be written at a level listed here.';


CREATE TABLE IF NOT EXISTS "unshackled-word"."RevisionVerses"
(
    "Id"                      serial4                  NOT NULL,
    "HebRefId"                int4                     NOT NULL,
    "Level"                   int4                     NOT NULL,
    "Version"                 int4                     NOT NULL,
    "VerseText"               text COLLATE "und-x-icu" NOT NULL,
    "BasedOnRevisionVerseId"  int4                     NULL,
    "BaseTextHash"            varchar(12)              NOT NULL,
    "Note"                    varchar(500)             NULL,
    "IsActive"                boolean                  NOT NULL DEFAULT true,
    "CreatedByUserId"         int4                     NULL,
    "CreatedAt"               timestamptz              NOT NULL DEFAULT now(),
    "EndedByUserId"           int4                     NULL,
    "EndedAt"                 timestamptz              NULL,
    "MappingReviewedByUserId" int4                     NULL,
    "MappingReviewedAt"       timestamptz              NULL,
    "SearchVector"            tsvector GENERATED ALWAYS AS (
        setweight(to_tsvector('"unshackled-word".uw_german'::regconfig, "VerseText"), 'A')
        ) STORED,
    CONSTRAINT "RevisionVerses_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "RevisionVerses_Version_UQ" UNIQUE ("HebRefId", "Level", "Version"),
    CONSTRAINT "RevisionVerses_Level_FK" FOREIGN KEY ("Level") REFERENCES "unshackled-word"."RevisionLevels" ("Level"),
    CONSTRAINT "RevisionVerses_BasedOn_FK" FOREIGN KEY ("BasedOnRevisionVerseId") REFERENCES "unshackled-word"."RevisionVerses" ("Id"),
    CONSTRAINT "RevisionVerses_CreatedBy_FK" FOREIGN KEY ("CreatedByUserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "RevisionVerses_EndedBy_FK" FOREIGN KEY ("EndedByUserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "RevisionVerses_ReviewedBy_FK" FOREIGN KEY ("MappingReviewedByUserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "RevisionVerses_Level_CK" CHECK ("Level" <> 100),
    CONSTRAINT "RevisionVerses_Version_CK" CHECK ("Version" >= 1),
    CONSTRAINT "RevisionVerses_Text_CK" CHECK (btrim("VerseText") <> ''),
    CONSTRAINT "RevisionVerses_Lifecycle_CK" CHECK ("IsActive" = ("EndedAt" IS NULL))
);

-- The invariant of the whole feature: one visible variant per verse and level.
CREATE UNIQUE INDEX IF NOT EXISTS "RevisionVerses_Active_UQ" ON "unshackled-word"."RevisionVerses" ("HebRefId", "Level") WHERE "IsActive";
CREATE INDEX IF NOT EXISTS "RevisionVerses_Search_idx" ON "unshackled-word"."RevisionVerses" USING gin ("SearchVector") WHERE "IsActive";
CREATE INDEX IF NOT EXISTS "RevisionVerses_Unreviewed_idx" ON "unshackled-word"."RevisionVerses" ("Level", "HebRefId") WHERE "IsActive" AND "MappingReviewedAt" IS NULL;

COMMENT ON TABLE "unshackled-word"."RevisionVerses" IS 'A full copy of one Elb1871 verse at one literalness level. Every text save inserts a new Version and ends the previous one; rows are never deleted.';
COMMENT ON COLUMN "unshackled-word"."RevisionVerses"."HebRefId" IS 'The Elb1871 verse (BBCCCVVV) this variant replaces. Natural key, no FK: Elb1871Verses is rebuilt by the seed.';
COMMENT ON COLUMN "unshackled-word"."RevisionVerses"."BasedOnRevisionVerseId" IS 'The variant this one was started from (revision of a revision); NULL = started from Elb1871.';
COMMENT ON COLUMN "unshackled-word"."RevisionVerses"."BaseTextHash" IS '12 hex of SHA-256 over the normalised Elb1871 tokens of the verse when the variant was written (same normalisation as the chapter fingerprint of roadmap 0010). Differs from the current verse = the base text was fixed afterwards.';
COMMENT ON COLUMN "unshackled-word"."RevisionVerses"."CreatedByUserId" IS 'NULL = imported from the repo seed.';
COMMENT ON COLUMN "unshackled-word"."RevisionVerses"."MappingReviewedAt" IS 'Set when a writer saved the mapping form of this version; NULL = the mapping is only what the carry-over algorithm produced.';


CREATE TABLE IF NOT EXISTS "unshackled-word"."RevisionWords"
(
    "Id"                  serial4                     NOT NULL,
    "RevisionVerseId"     int4                        NOT NULL,
    "PositionInVerse"     int4                        NOT NULL,
    "WordInContext"       varchar COLLATE "und-x-icu" NOT NULL,
    "PlainWord"           varchar COLLATE "und-x-icu" NULL,
    "BasePositionInVerse" int4                        NULL,
    "Change"              varchar(10)                 NOT NULL,
    CONSTRAINT "RevisionWords_PK" PRIMARY KEY ("Id"),
    CONSTRAINT "RevisionWords_Position_UQ" UNIQUE ("RevisionVerseId", "PositionInVerse"),
    CONSTRAINT "RevisionWords_Verse_FK" FOREIGN KEY ("RevisionVerseId") REFERENCES "unshackled-word"."RevisionVerses" ("Id"),
    CONSTRAINT "RevisionWords_Position_CK" CHECK ("PositionInVerse" >= 1),
    CONSTRAINT "RevisionWords_Change_CK" CHECK ("Change" IN ('same', 'changed', 'new')),
    CONSTRAINT "RevisionWords_Base_CK" CHECK (("Change" = 'new') = ("BasePositionInVerse" IS NULL))
);

COMMENT ON TABLE "unshackled-word"."RevisionWords" IS 'The words of one RevisionVerses row, split by spaces. Same columns as Elb1871Words where they mean the same.';
COMMENT ON COLUMN "unshackled-word"."RevisionWords"."PlainWord" IS 'Is the same as WordInContext but clean of all special characters (same rule as Elb1871Words.PlainWord).';
COMMENT ON COLUMN "unshackled-word"."RevisionWords"."BasePositionInVerse" IS 'PositionInVerse of the Elb1871 word (same HebRefId) this word continues; NULL for a new word. Derived by the carry-over algorithm, recomputable.';
COMMENT ON COLUMN "unshackled-word"."RevisionWords"."Change" IS 'same = identical PlainWord as the base word, changed = replaces the base word(s), new = no base word.';


CREATE TABLE IF NOT EXISTS "unshackled-word"."RevisionMapping"
(
    "Id"                   serial4     NOT NULL,
    "RevisionWordId"       int4        NOT NULL,
    "Lang"                 char(1)     NOT NULL,
    "StepWordId"           int4        NULL,
    "IsAddedWord"          boolean     NOT NULL DEFAULT false,
    "ParentRevisionWordId" int4        NULL,
    "GermanWordPart"       varchar(30) NULL,
    "Origin"               varchar(10) NOT NULL,
    "CreatedByUserId"      int4        NULL,
    "CreatedAt"            timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT "RevisionMapping_PK" PRIMARY KEY ("Id"),
    -- Same rule as the legacy tables: one German word with several original words needs one row per original word.
    CONSTRAINT "RevisionMapping_Word_Step_UQ" UNIQUE NULLS NOT DISTINCT ("RevisionWordId", "StepWordId"),
    CONSTRAINT "RevisionMapping_Word_FK" FOREIGN KEY ("RevisionWordId") REFERENCES "unshackled-word"."RevisionWords" ("Id"),
    CONSTRAINT "RevisionMapping_Parent_FK" FOREIGN KEY ("ParentRevisionWordId") REFERENCES "unshackled-word"."RevisionWords" ("Id"),
    CONSTRAINT "RevisionMapping_CreatedBy_FK" FOREIGN KEY ("CreatedByUserId") REFERENCES "unshackled-word"."Users" ("Id"),
    CONSTRAINT "RevisionMapping_Lang_CK" CHECK ("Lang" IN ('G', 'H')),
    CONSTRAINT "RevisionMapping_Origin_CK" CHECK ("Origin" IN ('carried', 'manual', 'seed')),
    -- A row without an original word is only meaningful as an added word.
    CONSTRAINT "RevisionMapping_Added_CK" CHECK ("StepWordId" IS NOT NULL OR "IsAddedWord"),
    CONSTRAINT "RevisionMapping_Parent_CK" CHECK ("ParentRevisionWordId" IS DISTINCT FROM "RevisionWordId")
);

CREATE INDEX IF NOT EXISTS "RevisionMapping_Step_idx" ON "unshackled-word"."RevisionMapping" ("Lang", "StepWordId") WHERE "StepWordId" IS NOT NULL;
CREATE INDEX IF NOT EXISTS "RevisionMapping_Parent_idx" ON "unshackled-word"."RevisionMapping" ("ParentRevisionWordId") WHERE "ParentRevisionWordId" IS NOT NULL;

COMMENT ON TABLE "unshackled-word"."RevisionMapping" IS 'Revision word x original word, Greek and Hebrew in one table. Lang G joins StepGreekWords, Lang H joins StepHebrewWords; both id ranges start at 1, so every join needs Lang.';
COMMENT ON COLUMN "unshackled-word"."RevisionMapping"."Lang" IS 'G for books 40-66, H for books 1-39 (Hebrew and Aramaic). Set by the service from the verse, never by the client.';
COMMENT ON COLUMN "unshackled-word"."RevisionMapping"."Origin" IS 'carried = copied from the parent text by the carry-over algorithm and not confirmed, manual = set in the mapping form, seed = imported from the repo seed.';
```

Notes on the choices:

- `IsAddedWord` is `NOT NULL` here (it is nullable in the legacy tables, 0014 has to `COALESCE` it).
- Ids are `serial4` like every other table. They are local: nothing outside the DB stores them, the seed uses keys.
- `RevisionVerses_Lifecycle_CK` plus the partial unique index make "end the old version, insert the new one" safe in one
  transaction; two concurrent saves of the same verse and level fail on the index, the endpoint turns that into `409`.
- A mapping save deletes and re-inserts the `RevisionMapping` rows of one variant. Rows of ended versions are never
  touched, so an old version can always be shown with the mapping it had.

### Effective variant (shared SQL fragment)

"Start at 100, walk towards the slider value, take the last variant passed" is one ordered pick. `RevisionSql.EffectiveVerses`:

```sql
SELECT v."HebRefId"
     , r."Id"    AS "RevisionVerseId"
     , r."Level" AS "Level"
FROM "unshackled-word"."Elb1871Verses" v
    LEFT JOIN LATERAL (SELECT rv."Id"
                            , rv."Level"
                       FROM "unshackled-word"."RevisionVerses" rv
                       WHERE rv."HebRefId" = v."HebRefId"
                           AND rv."IsActive"
                           AND rv."Level" BETWEEN least(@Level, 100) AND greatest(@Level, 100)
                       ORDER BY abs(rv."Level" - 100) DESC
                       LIMIT 1) r ON TRUE
WHERE v."HebRefId" BETWEEN @FromRefId AND @ToRefId
```

`@Level = 100` matches nothing (`Level <> 100` by constraint), so the old behaviour is the same query with no variant
found. Chapter read: this fragment as a CTE, then `UNION ALL` of `Elb1871Words` for verses with `RevisionVerseId IS NULL`
and `RevisionWords` for the others, ordered by `HebRefId`, `PositionInVerse`. The partial unique index
`RevisionVerses_Active_UQ ("HebRefId", "Level")` serves the lateral lookup.

### Carry-over (text → mapping)

Input: the parent's tokens with their mapping rows (Elb1871 words + legacy mapping, or a parent variant + its
`RevisionMapping`) and the new tokens. All comparisons on `PlainWord`, case-sensitive.

| Step | Case | Result |
|---|---|---|
| 1 | Token equal in the diff (Myers, `SequenceAligner`) | `Change = 'same'`, base position inherited, all mapping rows copied (`GermanWordPart`, `IsAddedWord` kept) |
| 2 | 1:1 replace between equal neighbours (`eingeborenen` → `einzigen`) | `Change = 'changed'`, rows copied, `GermanWordPart` cleared |
| 3 | n:1 replace (`in dem` → `im`) | The new word is `changed`, base position = first old word, and gets the union of the old words' original words |
| 4 | 1:m replace (`im` → `in dem`) | All m words are `changed` with the old word's base position and each gets the old word's original words (the legacy shape "several German words on one original word", 174,125 Hebrew cases) |
| 5 | n:m replace with n, m > 1 | New words are `new`, nothing copied; the old words' original words appear as "ohne Zuordnung" |
| 6 | Moved word: a deleted and an inserted token with the same `PlainWord`, each unique in its side after step 1 | Treated as step 1 (`same`) |
| 7 | Inserted token | `new`, no rows |
| 8 | Deleted token | Its original words lose the link unless another step carried them |
| 9 | Added-word rows (`ParentGermanWordId`) | `ParentRevisionWordId` = the carried parent; if the parent was not carried the row is kept with parent NULL |

Every copied row has `Origin = 'carried'`. The algorithm never invents a link between words that were not linked in the
parent; everything it could not place is shown, counted in the carry report and waits for the mapping form. When the
parent is a variant, `BasePositionInVerse` and `Change` are composed through the parent (a word that is `same` relative
to a `changed` parent word is `changed` relative to Elb1871).

### API

| Route | Auth | Purpose |
|---|---|---|
| `GET api/revision/levels` | anonymous | Slider stops |
| `POST api/revision/levels`, `PUT api/revision/levels/{level}` | admin, maintainer | Manage presets (0011.7) |
| `GET api/revision/verse/bookId/{b}/chapterId/{c}/verseId/{v}` | anonymous | Elb1871 text + active variants of one verse |
| `POST api/revision/verse/bookId/{b}/chapterId/{c}/verseId/{v}` | admin, maintainer | New version of a variant (`level`, `text`, `basedOnLevel`, `expectedVersion`, `note`) → `revisionVerseId` + carry report |
| `GET api/revision/verse/bookId/{b}/chapterId/{c}/verseId/{v}/history?level=` | admin, maintainer | All versions of a level |
| `DELETE api/revision/verse/{revisionVerseId}` | admin, maintainer | End the active variant without successor |
| `GET api/revision/verseMapping/{revisionVerseId}` | anonymous | `GetVerseMappingResponse` shape for a variant |
| `PUT api/revision/verseMapping/{revisionVerseId}` | admin, maintainer | Replace the variant's mapping, mark reviewed |
| `GET api/revision/wordDetail/wordId/{revisionWordId}` | anonymous | `GetWordDetailResponse` for a revision word |
| `GET api/revision/overview` | admin, maintainer, reviewer | Counts and work lists |
| `GET api/elberfelder/chapterWords/…`, `chapterText/…`, `search/verses` | anonymous | New optional `level` (default 100) |

The revision mapping responses reuse the existing contract and put `RevisionWords."Id"` into `germanWords[].id` and
`links[].elbWordId`. The field name is wrong for that content; it is kept so `MappingEditor` and `mappingEditor.ts` work
unchanged, and it disappears when 0005 replaces links by groups.

### Seed

> Status 2026-11-14: code, unit tests and docs for 0011.11-0011.14 are in the tree (`[~]`); the "Done when" runs on a scratch DB (export, fresh import, re-export, update, rebase) are still outstanding. Implementation notes: the verses file has one extra column `Reviewed` (without it the review state is lost in a round trip); `Origin` is kept as exported; `rebase` keeps the revision rows and recomputes the base links of active variants in place instead of clearing and re-importing (history of ended versions survives; STEP is not rebuilt by `rebase`).
>
> Seed round trip from the web UI (2026-11-14): the maintainer page ("Wartung" > "Seed-Rundlauf") exports the revision to `assets/seed`, imports it into empty tables, updates it (kept/replaced/new/removed lists), rebases and runs the round-trip check (export twice, byte-compare with each other and with the files on disk); it uses the same services as the CLI (`RevisionSeedImporter`, `SeedRebaseService`, `SeedFileExportService`). Verified on a scratch `postgres:18` with a synthetic Joh 3:16-17 base (the SeedDb CLI itself was not run): export wrote exactly the variant files, a second export changed nothing, a one-word DB edit made `matchesDisk` false and its restore true; import into emptied revision tables recreated the variants and one `SeedImports` row; a newer seed replaced the unchanged variant, kept the locally edited one, added the new one, and a repeat said "up-to-date"; `rebase` rebuilt 722,257 words, kept the variants and recomputed their base links. Not verified: a full real-data import, compose with writable mounts.

- `levels.csv`: the presets. `verses/NN-Book.csv`: one line per active variant, the text itself is the data.
  `mapping/NN-Book.csv`: mapping format 2 of 0010.8 plus `Level` and `Origin`.
- Not exported because derived: `RevisionWords` (tokenizer), `BasePositionInVerse`/`Change` (carry-over), `SearchVector`,
  versions and history, users, timestamps.
- A variant's key is `(Ref, Level)`; its German words are keyed `Pos` + `Form` inside the variant's own text, which the
  seed carries in full, so the German side never needs re-anchoring. The original side uses `SrcRef`/`SrcPos`/`SrcHash`
  and the 0010.9 rules.
- Elb1871 is public domain and the revision is CC0, so the text may be in the repo. STEP text never is (`SrcHash` only).

## Background

- Next free migration number: `0020` (0019 is `SeedVerseState`).
- `Elb1871Verses."HebRefId"` is unique and equals book/chapter/verse (`BBCCCVVV`) for every word (0010), so a chapter is
  the range `book * 1000000 + chapter * 1000 + 1 … + 999`.
- `Elb1871Morphology` is keyed by `(HebRefId, PositionInVerse)`; a `same` revision word reaches its lemma through
  `BasePositionInVerse` without any new table.
- The tokenizer check of 0011.2 puts its numbers here (verses whose `VerseText` does not split into the stored words).
  Checked read-only: Gen 1, Ps 119, Joh 1 = 258 verses, 0 mismatches (every `VerseText` splits into exactly the stored
  `WordInContext`/`PlainWord` rows). The shared rule is `Elb1871PlainWord.From` (Domain); an empty `PlainWord` is stored as `''`, not NULL.
  A 40-verse subset is the regression fixture `RevisionTokenizerParserParityTests`.

## Risks worth naming before starting

- **A revised verse can show a wrong original word, and nobody is told.** The carry-over copies links for replaced
  words (steps 2–4). `Sohn` → `Kind` keeps a correct link; a rewrite that swaps the meaning of two neighbouring words
  keeps two wrong ones. Readers see them immediately because saves are live. Mitigation: `Origin = 'carried'` and
  `MappingReviewedAt IS NULL` are stored, the editor marks `changed` words "prüfen", and 0011.9 lists unreviewed
  variants. Do not start real revision work before 0011.5f and 0011.9f exist.
- **Revision work that is only in one database is one `docker compose down -v` away from gone.** Until 0011.11/0011.12
  the variants are not in the repo seed. Finish the seed strand before revising in earnest, and export + commit after
  every session (the same routine as morphology).
- **`StepWordId` moves when STEP is re-imported.** `RevisionMapping` inherits this weakness from the legacy tables on
  purpose (0005 owns the fix). The only safe path across a STEP re-import or base-text fix is `rebase` (0011.13):
  export, rebuild, import. A manual re-import of STEP data without it silently points links at other words; the row
  counts stay the same, so check `SeedImports` and the report, not the counts.
- **The tokenizer must be the parser's tokenizer.** If `RevisionTokenizer` and `ElberfelderParser` disagree on
  `PlainWord` (dashes, apostrophes, brackets for added words), unchanged words stop matching and the carry-over degrades
  to "everything new" without any error. The Done-when of 0011.2 (re-tokenise existing verses and compare with
  `Elb1871Words`) is the guard; do not weaken it.
- **Level 100 must stay byte-identical.** The MCP server, Bruno and the stats assume Elb1871. The default of the new
  `level` parameter is 100 and 0011.3 tests identical JSON; only the SPA sends another level. Check whether the chapter
  endpoints or a proxy cache responses: the level must be part of any cache key, and a save must not serve a stale
  chapter to the writer.
- **Stats and search disagree with what the reader shows.** A reader at the default level sees `einzigen`, but word
  stats, Strong's renderings, the lemma editor and lemma-widened search still describe `eingeborenen`. This is decided
  ("stats stay on the original for now"); the popover says "Elb1871: …" for changed words so the numbers are not read
  as the revision's. Signal to solve it: more than a few hundred variants, or the first question why a count is off.
- **No mapping history inside a version.** A wrong mapping save cannot be undone except by re-mapping; only text
  versions are kept. Accepted because the legacy editor behaves the same and 0005 brings changesets.
- **Level presets are hard to take back.** Once variants exist at a level, changing its number would move them on the
  slider; the API refuses it. Pick the first presets with gaps (200, 400, 600 …) so levels can be inserted later.
- **The legacy mapping save is still anonymous.** `PUT elberfelder/verseMapping` is `AllowAnonymous` behind
  `MappingEditing:Enabled`. Revision endpoints use roles from the start; the legacy endpoint is not changed here, so the
  two editors have different gates until someone moves it to roles.

## Deferred

- Suggestions by any logged-in user, review queue, merge/reject (CLAUDE.md "Linux-style"); drafts that are not public.
- "Let AI map / redo this verse" for revised verses and AI-written variants.
- German lemma, part of speech and STTS for `changed` and `new` words; lemma-widened search on revision text.
- Stats, Strong's renderings and data-quality checks on revision text.
- Showing two levels side by side; a diff view against Elb1871 for readers.
- Verse splits/merges, headings, footnotes.
- Moving `RevisionMapping` (and the legacy tables) into 0005's `MappingLinks`; mapping history and confirmations.
- MCP tools that read a level.

## Open questions for the maintainer

The tasks follow the recommendation until answered.

1. **Presets or free numbers?** Recommendation: presets in `RevisionLevels` (assumption above). Free numbers 1–1000 per
   verse are possible with the same tables by dropping the FK, but the slider then has no meaningful stops.
2. **Names and numbers of the first presets.** Recommendation: `100 elb1871 "Elberfelder 1871"` and
   `200 revision "Revidierte Elberfelder"` (default). Which further levels are planned, and what are they called?
3. **Slider below 100.** The scale allows levels more literal than Elb1871 (1–99). Is an interlinear-like level planned?
   If not, the slider simply never shows a stop left of Elb1871.
4. **Search scope.** Recommendation (0011.10): match the effective text at the reader's level and the Elb1871 text, one
   hit per verse. Alternative: only what the reader sees.
5. **1:m carry-over (`im` → `in dem`).** Recommendation: link all new words to the old word's original words and mark
   them "prüfen". Alternative: link only the first and leave the rest for the form.
6. **Reviewer role.** May `reviewer` write variants, or only read the overview (as assumed)?
7. **Moving the legacy mapping save to roles** (see Risks): do it inside 0011.5 or separately?
