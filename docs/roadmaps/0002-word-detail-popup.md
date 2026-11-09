# Roadmap 0002: Word detail pop-up in the reader

## Goal

In the React reader (`src/UnshackledWord.Web`, `ChapterPage`) a click, tap or keyboard activation of an Elberfelder
1871 word opens a pop-up with the Greek or Hebrew word(s) behind it:

- original form (with diacritics, correct direction), transliteration, lemma,
- Strong's number (STEP disambiguated notation, e.g. `G3056`, `H7225G`),
- gloss (dictionary meaning) and contextual gloss (meaning in this verse),
- morphology code **and** its decoded, translated form ("Substantiv · Nominativ · Singular · maskulin"),
- Hebrew word parts (prefix / root / suffix),
- short lexicon definition,
- several readings when one German word covers several original words,
- clear empty states: "not linked yet" and "added by the translator".

The existing hover highlight (words sharing a Strong's number) keeps working, and so do ArrowLeft/ArrowRight chapter
navigation and the chapter picker.

Delivery order: **Phase 1** builds the whole frontend on mock data behind a provider seam (the next developer task),
**Phase 2** builds the API, **Phase 3** swaps the mock for the API, **Phase 4** adds extras (occurrences,
Elberfelder renderings, cross references, people/places, textual variants, MCP tool).

The pop-up also shows what the AI mapping data says about the word (provenance badge, German words translated
together, uncertainty hints) and a disabled "edit mapping" entry point (S28–S31).

Out of scope: editing or correcting mappings in the pop-up itself (tagging UI, priority 1, separate roadmap), login,
the vocab trainer, the translation-level slider.

## Decisions (design lead, 2026-11-09, no interview; maintainer to confirm, see open questions)

| Topic | Decision |
|---|---|
| Phasing | Frontend first on mock data (Phase 1), API second, swap third. This deviates on purpose from the "backend and frontend woven together" rule: the contract is fixed in Phase 1 and the mock is the stand-in backend, so every slice still runs. |
| Fetch strategy | **Lazy, one request per word**, cached by TanStack Query for the session (`staleTime: Infinity`), prefetched on hover intent in Phase 3. No chapter batch (payload too large: Ps 119 has ~2,400 words). |
| Route | `GET /api/elberfelder/wordDetail/wordId/{WordId:int}` (same style as `chapterWords/bookId/{}/chapterId/{}`). |
| Contract | One TypeScript type `GetWordDetailResponse` in `src/lib/bible/types.ts`, mirrored by C# classes in `Domain.WebApi/Elberfelder/GetWordDetail/`. camelCase, nulls omitted, so optional fields are `?:` in TS. |
| Morphology decoding | The API returns the raw code **plus** stable keys (`partOfSpeech: "noun"`, `case: "nominative"` …). The frontend translates keys via i18n (DE/EN). Keys come from **our own CC0 decoders** of the Robinson (Greek) and OSHB-style (Hebrew) codes; the STEP morphology tables only feed an English fallback `description`. |
| Pop-up element | One shared element per page with the **native Popover API** (`popover="manual"`, top layer), CSS anchor positioning on desktop, bottom sheet on small screens and as fallback. No new dependency. |
| New component | Exactly one: `WordDetailPopover` (presentational). Plus a new `inline` variant of `Button` for the clickable words (no raw `<button>`). |
| Clickable words | **Every** word becomes a `Button` (unmapped and added words open the pop-up with an explanatory empty state). Hover highlighting stays limited to words with senses. |
| STEP data | Shown live from the self-hosted database with attribution; never copied into the repo (mocks, fixtures, seeds), never stored by the browser beyond the in-memory query cache. See [Licence](#licence-what-may-be-shown-stored-or-derived). |
| MCP sharing | Repositories, decoders and the `WordDetailService` live in Application/Infrastructure/Domain so the MCP server (roadmap 0001 S13/S14/S27/S31/S33/S35) reuses them. |

## How to use this roadmap

- Work through the steps **in table order**, or in any order the **Depends on** column allows.
- One step = one small, self-contained change (one commit). After every step:
  - frontend: `docker compose run --rm --no-deps unshackledword.web bun run check` passes (typecheck, Biome,
    `check:components`, Vitest),
  - backend: `dotnet build UnshackledWord.slnx` and `dotnet test` pass.
- Tick `[ ]` → `[x]` when **Done when** is met. If a step needs files that are not listed, update this roadmap first.
- Read [Background knowledge](#background-knowledge) and [Conventions](#conventions-for-all-steps) once before the
  first step. The [Phase 1 spec](#phase-1-spec-frontend-on-mock-data) is self-contained.

## Steps

| ✔ | ID | Step | Depends on | Done when |
|---|---|---|---|---|
| | | **Phase 1 – frontend on mock data** | | |
| [x] | S01 | Word detail contract types in `types.ts` | – | Types from [Contract](#the-contract-typescript) compile; `bun run check` green |
| [x] | S02 | `morphology.ts` formatter + `bible.grammar` i18n keys | S01 | `formatMorphology` test table passes in DE and EN |
| [x] | S03 | Provider seam `wordDetail.ts` + mock `wordDetail.mock.ts` + `bibleApi.wordDetail` | S01 | `normalizeStrongs` and mock tests pass; source switch via `VITE_WORD_DETAIL_SOURCE` typed |
| [x] | S04 | `Button` variant `inline` | – | Button tests incl. new inline case pass; `Components.md` row updated |
| [x] | S05 | Pop-up CSS (anchor, sheet) + `--font-original` token in `index.css` | – | `bun run check` green; classes `word-anchor` / `word-popover` exist |
| [x] | S06 | Component `WordDetailPopover` + `bible.wordDetail` i18n keys | S02, S04, S05 | Component tests (states, readings, RTL, Escape, close, EN) pass; registered in `Components.md` |
| [x] | S07 | Wire pop-up into `ChapterPage` | S03, S06 | Page tests (click, Enter, Escape + focus return, outside click, arrow guard, highlight, chapter change) pass; manual check in dev server |
| [x] | S28 | AI mapping data in the pop-up (mock): `mapping`, `coMappedWords`, `hints`, `sharedWithWordIds`, `positionInVerse`; badge, phrase line, "added to „X“", hints, disabled "Zuordnung bearbeiten"; phrase underline in `ChapterPage` | S07 | Mock, popover, `phraseWordIds` and page tests pass; `bun run check` green |
| | | **Phase 2 – API** | | |
| [x] | S08 | `FakeDbReader` test wiring in `Infrastructure.Tests` (= roadmap 0001 S08; skip if done) | – | `FakeDbReader` records SQL + parameters; its tests pass |
| [x] | S09 | `GreekMorphologyDecoder` (Domain, pure) | – | Test table of Robinson codes → keys passes |
| [x] | S10 | `HebrewMorphologyDecoder` (Domain, pure) | – | Test table of OSHB-style codes (incl. `HR/Ncfsa`, Aramaic `A…`) passes |
| [x] | S11 | `LexiconTextCleaner` (Domain, pure) | – | Markup stripped, whitespace collapsed, truncation at word boundary; tests pass |
| [x] | S12 | Response contract classes in `Domain.WebApi/Elberfelder/GetWordDetail` | – | Classes compile; serialization test produces the JSON shape of `wordDetail.sample.json` |
| [x] | S13 | `IWordDetailRepository` (word + Greek/Hebrew readings) | S08 | Three methods implemented, registered in `AddRepositories()`, SQL/parameter tests pass |
| [x] | S14 | `IHebrewMorphemeRepository` (= roadmap 0001 S27) | S08 | Implemented, registered, unit-tested |
| [x] | S15 | `WordDetailService` (Application/Features/WordDetail) | S09–S14 | Service tests with substituted repositories pass (NT, OT, multi-reading, added word, no mapping, unknown id) |
| [x] | S16 | Endpoint `GetWordDetail` + Bruno request | S15 | `GET /api/elberfelder/wordDetail/wordId/{id}` returns JSON for Joh 1,1 "Wort" and 1Mo 1,1 "Anfang"; 404 for unknown id; `Cache-Control` set |
| [ ] | S29 | Migration `0007_MappingProvenance.sql`: on both mapping tables `"CreatedBy" INT NOT NULL DEFAULT <AI user id>`, `"Model" VARCHAR(64) NULL`, `"PartOrder" INT NULL`, `"ConfirmedBy" INT NULL`, `"ConfirmedAt" TIMESTAMPTZ NULL`; versioning per open question 13 | – | Migration runs on fresh and existing DB; existing rows become AI rows |
| [ ] | S30 | AiWorker + seed import write provenance (AI user id, model constant, `PartOrder`); mapping-seed CSV gains `Model`, `PartOrder`, `ConfirmedAt` columns (CC0, our data) | S29 | Worker insert and `AiMappingImportRunner` tests/manual run fill the columns; old CSVs still import |
| [ ] | S31 | `IWordDetailRepository.GetCoMappedWordsAsync` + provenance in `WordDetailService` (`mapping`, `coMappedWords`, `sharedWithWordIds`, `hints`, `GermanWordPart` cleanup) | S15 (S29 optional) | Service tests: phrase (Und die Erde), added word ↔ parent, over-assigned `Der` → `manyOriginals`, `*` part → `unclearWordPart`, whole-word part omitted |
| | | **Phase 3 – swap mock for API** | | |
| [x] | S17 | Default source `api`, compose env, 404 handling | S07, S16 | Real data in the dev server; mock still used by tests; `bun run check` green |
| [x] | S18 | Shared contract fixture test (C# and TS) | S12, S17 | Both test suites read `wordDetail.sample.json`; C# round-trip equal; TS `satisfies` compiles |
| [x] | S19 | Hover-intent prefetch | S17 | Test: hovering 200 ms prefetches once, quick pass-over does not |
| | | **Phase 4 – extras** | | |
| [x] | S20 | Migration `0006_WordDetailIndexes.sql` (Strong's indexes) | – | Migration runs on a fresh and an existing DB |
| [x] | S21 | `occurrenceCount` per reading | S16, S20 | Count shown ("137× im NT"); repository test passes; response < 50 ms locally |
| [x] | S22 | Elberfelder renderings (= roadmap 0001 S33) in the pop-up | S16, S20 | "übersetzt mit: Wort (…×), Rede (…×)" shown, top 5 |
| [x] | S23 | Occurrence list endpoint + concordance page `/bible/strongs/:strongs` (= 0001 S35) | S20 | Paged list; link "Alle Stellen" in the pop-up; tests pass |
| [x] | S24 | Verse cross references (TSK) section | S16 | Section lists TSK targets for the word's verse (existing `ITskRepository`) |
| [x] | S25 | People / places brief | S16 | Proper nouns show the `Brief` text of `StepPersonLexicon` / `StepPlaceLexicon` |
| [x] | S26 | NT textual-variant hint (SBL apparatus) | S16 | Note "Textvarianten in diesem Vers" when `SblApparatus` has rows |
| [ ] | S27 | MCP tool `get_word_detail` reusing `WordDetailService` | S15 | Tool returns formatted detail; MCP tests pass |

## Background knowledge

### What the database has for one original word

All tables are in schema `"unshackled-word"` (scripts in `src/UnshackledWord.Tooling.Postgres.Migration/scripts/`;
note there is no `0003_…`). Book 1–39 = OT (Hebrew/Aramaic), 40–66 = NT (Greek).

| Field (pop-up) | Greek (NT) | Hebrew/Aramaic (OT) | Notes |
|---|---|---|---|
| German word | `Elb1871Words.WordInContext` | same | CC0 |
| German lemma | `Elb1871Words.Lemma` | same | CC0, from `Elb1871Lemmatizer` |
| Link German → original | `Elb1871GreekMapping` (`ElbWordId` → `StepWordId`) | `Elb1871HebrewMapping` | CC0 mapping ids; several rows per `ElbWordId` = several readings |
| Word part | `…Mapping.GermanWordPart` | same | which part of a German compound belongs to which original word |
| Added by translator | `…Mapping.IsAddedWord` (`StepWordId` NULL, `ParentGermanWordId`) | same | traditionally italic |
| Original form | `StepGreekWords.Greek` | `StepHebrewWords.Hebrew` | with diacritics |
| Position in original verse | `StepGreekWords.PositionInVerse` | `StepHebrewWords.PositionInVerse` | order of readings |
| Transliteration | `StepGreekWords.Transliteration` | `StepHebrewWords.Transliteration` | |
| Lemma | `StepGreekWords.Lemma` | **missing in word row** → `StepStrongsLexicon.OriginalWord` of the root Strong's | |
| Strong's (disambiguated) | `StepGreekWords.DisambiguatedStrongs` (`G3056`) | `StepHebrewWords.RootDisambiguatedStrongsInstance` (root, `H7225G`); full word `DisambiguatedStrongs` (`H9003/{H7225G}`) | the chapter API already returns these as `strongs` |
| Dictionary gloss | `StepGreekWords.Gloss` | `StepStrongsLexicon.Gloss` (root) | English |
| Contextual gloss | `StepGreekWords.English` | `StepHebrewWords.Gloss` | English; `StepGreekWords.German` exists but the importer never fills it |
| Morphology code | `StepGreekWords.Morphology` (`N-NSM`) | `StepHebrewWords.Grammar` (`HR/Ncfsa`, whole word) | |
| Morphology decoded (STEP, English) | `StepGreekMorphology` by `Code` (PartOfSpeech, Tense, Voice, Mood, Person, Number, Case, Gender, Degree, Extras, NameType) | `StepHebrewMorphology` by `Code` (PartOfSpeech, Form, Tense, Mood, Person, Number, Gender, State, Stem, Action, Voice) | used as English fallback only |
| Hebrew word parts | – | `StepHebrewWordsNormalizedToHebrewWords` → `StepHebrewWordsNormalized` (`IsRoot`, `Hebrew`, `StrongsNumber`, `Grammar`, `SuffixCode`, `PositionInWord`) | query = roadmap 0001 S27 |
| Lexicon definition | `StepStrongsLexicon.Lexicon` (`LanguageId` 2, `Number`, `Extra`) | same (`LanguageId` 0 or 1) | long, contains markup; existing `IStrongsLexiconRepository.GetEntriesAsync` |
| Meaning/spelling variants | `StepGreekWords.MeaningVariants`, `SpellingVariants`, `SubMeaning` | `StepHebrewWords.MeaningVariants`, `SpellingVariants` | not shown in Phase 1–3 |
| Editions | `StepGreekWords.IsInNestleAland`, `IsInTextusReceptus`, `Editions` | – | candidate for Phase 4 |
| Occurrences of the Strong's | count over `StepGreekWords.DisambiguatedStrongs` (**no index**) or `StrongsNumbers` (`LanguageId`, `Number` indexed) or `Elb1871GreekMapping.StrongsNumber` (indexed) | `StepHebrewWords.RootDisambiguatedStrongsInstance` (**no index**); `Elb1871HebrewMapping` has **no** `StrongsNumber` column | Phase 4 adds indexes |
| Other verses with the Strong's | same tables + `Elb1871Verses` | same | like `Elberfelder/Fts/GetElbWordOccurrence` |
| Cross references | `Tsk` (**verse level**, `LxxRefId`) via `ITskRepository.GetOutgoingAsync` | same | not word level |
| SBL Greek text / apparatus | `SblText`, `SblApparatus` (**verse level**, `LxxRefId`) | – | not word level |
| People / places | `StepPersonLexicon`, `StepPlaceLexicon`, `StepOtherLexicon` by `Strongs` (`Brief`, `Short`, `Article`) | same | `LxxRefId` there is only the first occurrence |

**Missing / broken (become risks or Phase 4 steps):**

- No Hebrew lemma column; no index on `StepGreekWords.DisambiguatedStrongs` or
  `StepHebrewWords.RootDisambiguatedStrongsInstance`; no `StrongsNumber` in `Elb1871HebrewMapping`.
- `StepStrongsToVersesDbo` (`"StepStrongsToVerses"`) has a repository but **no `CREATE TABLE` in any migration**.
- No German glosses anywhere (STEP glosses are English; translating them would be a modification, see Licence).
- TSK and SBL data are verse level only.
- `Domain/Models/Grammar` enums (`PartOfSpeech`, `GreekTense`, `GreekVoice`, `GreekMood`, `GreekCase`, `Gender`,
  `GrammaticalNumber`, `GreekDegree`) serve the German/SR GNT grammar tagging, not STEP codes; `GreekMood.Infinitve`
  is misspelled. Their **names** are reused as key vocabulary (camelCased), the enums themselves are not changed.

### What exists to reuse

| Thing | Where | Reuse in |
|---|---|---|
| Chapter words with `strongs`, `original`, `grammaticalKey` | `Tooling.WebApi/Endpoints/Elberfelder/GetWordsForChapter/Endpoint.cs` | already drives the reader and hover |
| `groupWordsByVerse`, `buildHighlights`, `WordToken`, `WordSense` | `src/UnshackledWord.Web/src/lib/bible/words.ts`, `types.ts` | mock lookup by sense key, highlight while open |
| `StrongsNumber.Parse` (handles `{H7225G}`, `H9003/{H7225G}`) | `Domain/Models/Study/StrongsNumber.cs` | lexicon lookup in the service |
| `IStrongsLexiconRepository` + `LexiconEntry` | Application/Infrastructure | lexicon summary |
| MCP `lookup_strongs` (800-char shortening) | `Tooling.McpServer/Tools/LexiconTools.cs` | same idea as `LexiconTextCleaner`; S11 can be used by the tool later |
| `ITskRepository` (`HebRefId` ↔ `LxxRefId` translation) | Infrastructure | S24 |
| MCP roadmap queries S13/S14/S27/S33/S35 | `docs/roadmaps/0001-mcp-server.md` | S13/S14/S22/S23 implement them once for both |
| AstroWeb | `src/UnshackledWord.AstroWeb/src` | The "placeholder" is only an idea: `components/Study.astro` is **empty**, `pages/concordance.astro` only prints "Konkordanz", `ElbBibleWord.astro` puts `data-strongs`/`data-original`/`data-lemma` on each word and `pages/bible/book/[bookId]/chapter/[chapter].astro` highlights same Strong's (blue = same form, green = other form; already ported). `TaggingMenu.astro` is a click-to-open dropdown with outside-click close (pattern ported in S07). `types.ts` `PartOfSpeech` = the Domain enum. Ported idea: word → study details, concordance page (S23). |

### Alignment semantics (from `AiMappingImportRunner`, see roadmap 0001)

- One mapping row per (German word, original word). Several rows per `ElbWordId` → one German word covers several
  original words. Several German words can point to the same original word (`Im` and `Anfang` → בְּרֵאשִׁית).
- `IsAddedWord = true` → no original word (`StepWordId` NULL).
- Original words that appear in no mapping row are untranslated (Greek article, object marker את).

### AI mapping data: what really exists per mapping (investigated 2026-11-09)

How it is made: `Tooling.AiWorker` (`GreekGeminiFlashClient`, `HebrewGeminiFlashClient`, model hard-coded
`gemini-3.1-flash-lite-preview`; `GeminiModelType` also lists `gemini-2.5-flash` and `gemini-3-flash-preview`, so
older rows may come from those) sends verse batches of `(id,word)` pairs and gets back, per verse, pipe strings
`ElbWordId|StepWordId|IsAddedWord|ParentElbWordId|PartOrder|GermanWordPart`. `AiResponseStatsValidation` rejects a
whole batch on wrong ids (stats are only logged). `GreekMappingRepository.InsertMappingsAsync` writes
`ElbWordId, StepWordId, HebRefId, IsAddedWord, ParentGermanWordId, PositionInVerse, GermanWordPart`
(`ON CONFLICT DO NOTHING`). The rolling seed is `assets/Elb1871Mappings/NN-Book.csv` (66 files, ~725,000 rows,
columns `HebRefId, PositionInVerse, GermanWordPart, ElbWord, StepWord, StepPositionInVerse, IsAddedWord,
ParentPositionInVerse, ParentWord`), imported by `SeedDb/Services/AiMappingImport` by matching word text + position.

| Wanted for the pop-up | In the data? | Where / gap |
|---|---|---|
| German word → original word(s) | **yes** | one row per (`ElbWordId`, `StepWordId`); 2,485 German words have several originals (`150` → חֲמִשִּׁים + וּמְאַת) |
| Several German words → one original | **yes** | same `StepWordId` in several rows (`Und die Erde` → וְהָאָרֶץ; ~9,500 groups in Genesis alone) |
| Added word and the word it belongs to | **yes** | `IsAddedWord` + `ParentGermanWordId` (`Im` → `Anfang`) |
| Part of a German compound | partly | `GermanWordPart`; 43,414 of 47,564 non-empty values are just the whole word (noise, mostly OT), 7 are `*` |
| Order of the parts | **no** | Gemini returns `PartOrder`, the worker drops it on insert |
| Source AI vs human, user id | **no** | no `CreatedBy`/user column; every row today is AI (or a hand fix indistinguishable from it) |
| Model / prompt version | **no** | not stored (only the constant in code) |
| Timestamp | partly | `CreatedAt DEFAULT now()` = insert time; after a seed it is the **seed time**, not the AI run |
| Version / history | **no** | rows are inserted once, never versioned |
| Human confirmation | **no** | no column, no table |
| Confidence, alternative candidates, notes | **no** | Gemini is not asked for them; `AiResponseStats` (faulty/over-assigned rows) is only logged |
| Morphology added by Gemini | **no** | `ElbMorphology/ElberfelderMorphologyClient` is an empty class; German `PartOfSpeech`/`GrammaticalKey` on `Elb1871Words` come from other importers |
| `StrongsNumber` on `Elb1871GreekMapping` | rarely | only filled by the local-only `CopyBackMappingRepository`, not by the worker or the seed import |

Quality signals that **can** be derived: 50,086 seed rows say `IsAddedWord = True` and still name an original word
(the importer turns them into normal mappings, e.g. Joh 18,19 `Der` → Ὁ, τὸν, τῶν, τῆς: over-assignment), so
"≥ 3 originals for one German word" is a useful hint. `assets/Elb1871WordsSrWordsMapping.csv` (SR GNT lemma →
German words with counts, `Elb1871WordsSrGntWordsMapper` → table `Elb1871SrGntTagging`, which has no migration) is
a frequency table, not a per-word mapping; it is not used by the pop-up (candidate for "typical renderings", S22).

### Licence: what may be shown, stored or derived

The app and everything it creates are CC0. STEP Bible data (TAGNT, TAHOT, TBESG/TBESH lexicons, morphology files)
is downloaded on the fly by the seeder into the self-hoster's database and must **not be redistributed or modified**.
STEPBible-Data is published under CC BY 4.0 (verify the exact wording in the STEPBible-Data repo before Phase 3).

| Data | Status | Rule |
|---|---|---|
| Elberfelder text, German lemma, mapping ids, `GermanWordPart`, `IsAddedWord` | CC0 (ours) | free to store, seed, ship |
| Strong's numbers, morphology **codes** (Robinson, OSHB notation) | facts / public notation | free |
| Decoded grammar **keys** and their DE/EN labels | CC0 (our decoders, our i18n) | free |
| Original forms, transliteration, glosses, lexicon text, STEP morphology descriptions | STEP (CC BY) | **show live** from the DB with attribution; unmodified in substance (stripping markup and shortening with "more" is formatting, see open question); **never** copy into repo files (mocks, fixtures, snapshots, seeds, the rolling mapping seed), never write into CC0 tables, never persist in `localStorage`/IndexedDB |
| HTTP / in-memory caching | – | allowed (`Cache-Control: public, max-age=3600`, TanStack in-memory cache) |
| German translation of STEP glosses | would be a derived/modified work | not done; UI marks English glosses with `lang="en"` and "(engl.)" |

Consequences for this roadmap: the Phase 1 mock entries are **hand-written** (own one-word glosses, own placeholder
definition text), and the shared contract fixture (S18) is hand-written too. The pop-up footer always shows
"Grundtextdaten: STEPBible.org (Tyndale House), CC BY 4.0" when it shows any reading.

## API design

### Route

`GET /api/elberfelder/wordDetail/wordId/{WordId:int}` in `Tooling.WebApi/Endpoints/Elberfelder/GetWordDetail/Endpoint.cs`,
group `RouteGroupConfig` (`elberfelder`). Public, read-only.

- 200 with `GetWordDetailResponse`; `readings: []` when unmapped or added.
- 404 when no `Elb1871Words` row has this id.
- Header `Cache-Control: public, max-age=3600` (mappings change only through tagging or a mapping-seed update).

Rejected alternatives: a chapter batch (`wordDetails/bookId/{}/chapterId/{}`, megabytes for long chapters, most
words never opened); a Strong's-keyed endpoint (loses `GermanWordPart`, contextual gloss, morphology of this
occurrence). A light batch `?ids=1,2,3` can be added later if prefetch shows a need.

### The contract (TypeScript)

This is the single source of truth. It goes into `src/UnshackledWord.Web/src/lib/bible/types.ts` (S01); the C#
classes (S12) mirror it. Optional = omitted by the API when null. Booleans that are `false` are sent as null
(C# `bool?`) so they are omitted too.

```ts
export type OriginalLanguage = "greek" | "hebrew" | "aramaic";

export type PartOfSpeechKey =
  | "adjective" | "adverb" | "article" | "conjunction" | "indeclinable" | "interjection"
  | "noun" | "nounProper" | "nounProperPerson" | "nounProperPlace"
  | "particle" | "preposition" | "pronoun" | "suffix" | "verb";
export type TenseKey = "present" | "imperfect" | "future" | "aorist" | "perfect" | "pluperfect" | "futurePerfect";
export type VoiceKey = "active" | "middle" | "passive" | "middleOrPassive";
export type MoodKey = "indicative" | "subjunctive" | "optative" | "imperative" | "infinitive" | "participle";
export type PersonKey = "first" | "second" | "third";
export type CaseKey = "nominative" | "genitive" | "dative" | "accusative" | "vocative";
export type NumberKey = "singular" | "dual" | "plural";
export type GenderKey = "masculine" | "feminine" | "neuter" | "common";
export type DegreeKey = "comparative" | "superlative";
export type HebrewStemKey = "qal" | "niphal" | "piel" | "pual" | "hiphil" | "hophal" | "hithpael" | "other";
export type HebrewFormKey =
  | "perfect" | "imperfect" | "sequentialPerfect" | "sequentialImperfect"
  | "cohortative" | "jussive" | "imperative"
  | "infinitiveAbsolute" | "infinitiveConstruct" | "participleActive" | "participlePassive";
export type HebrewStateKey = "absolute" | "construct" | "determined";

/** A morphology code plus its decoded parts. Parts are stable keys, translated by i18n (`bible.grammar.*`). */
export interface Morphology {
  code: string;                 // "N-NSM", "V-IAI-3S", "HR/Ncfsa", "HNcmpa"
  partOfSpeech?: PartOfSpeechKey;
  tense?: TenseKey;             // Greek
  voice?: VoiceKey;             // Greek
  mood?: MoodKey;               // Greek
  person?: PersonKey;
  case?: CaseKey;               // Greek
  number?: NumberKey;
  gender?: GenderKey;
  degree?: DegreeKey;           // Greek
  stem?: HebrewStemKey;         // Hebrew/Aramaic verbs
  hebrewForm?: HebrewFormKey;   // Hebrew/Aramaic verbs
  state?: HebrewStateKey;       // Hebrew/Aramaic nominals
  /** English STEP description, only when the decoder could not map the code. */
  description?: string;
}

/** One part of a Hebrew/Aramaic word (prefix, root, suffix), in word order. */
export interface Morpheme {
  role: "prefix" | "root" | "suffix";
  original: string;
  strongs?: string;             // "H9003"
  gloss?: string;               // English
  morphology?: Morphology;
}

export interface LexiconSummary {
  strongs: string;
  lemma: string;
  transliteration?: string;
  gloss?: string;               // English
  definition?: string;          // English, markup stripped, max ~600 chars
  definitionTruncated?: boolean;
}

/** One original-language word behind the German word. */
export interface OriginalReading {
  stepWordId: number;
  language: OriginalLanguage;
  positionInVerse: number;      // position in the original verse; readings are sorted by it
  germanWordPart?: string;      // part of the German word this reading covers
  original: string;             // with diacritics
  transliteration?: string;
  lemma?: string;
  strongs: string;              // STEP disambiguated root, e.g. "G3056", "H7225G"
  gloss?: string;               // dictionary gloss, English
  contextGloss?: string;        // meaning in this verse, English
  morphology?: Morphology;      // whole word (Hebrew: root part)
  morphemes?: Morpheme[];       // Hebrew/Aramaic only
  lexicon?: LexiconSummary;
  occurrenceCount?: number;     // Phase 4 (S21)
  sharedWithWordIds?: number[]; // other German words of the verse mapped to this same original (ids in coMappedWords)
}

/** Who wrote the current version of the word's mapping rows. */
export type MappingSource = "ai" | "human";
export interface MappingProvenance {
  source: MappingSource;
  createdAt: string;            // ISO 8601, current version
  model?: string;               // AI only, e.g. "gemini-3.1-flash-lite-preview"
  confirmedAt?: string;         // ISO 8601, a human confirmed the AI mapping
}
/** Frontend-derived badge (`mappingStatus()` in wordDetail.ts). */
export type MappingStatus = "aiSuggested" | "confirmed" | "manual";

export type CoMappedRelation = "sameOriginal" | "addedWord" | "parent";
export interface CoMappedWord {
  wordId: number;
  wordInContext: string;
  positionInVerse: number;
  relation: CoMappedRelation;   // sameOriginal: shares a StepWordId; addedWord: its ParentGermanWordId is this word; parent: this word's ParentGermanWordId
}
/** Server-derived; there is no confidence value in the data. */
export type MappingHintKey = "manyOriginals" | "unclearWordPart";

export interface GetWordDetailResponse {
  wordId: number;
  wordInContext: string;
  positionInVerse: number;      // German position, orders the phrase line
  germanLemma?: string;
  isAddedWord?: boolean;        // omitted when false
  readings: OriginalReading[];  // [] = not linked (or added word)
  mapping?: MappingProvenance;  // omitted when the word has no mapping row
  coMappedWords?: CoMappedWord[]; // same verse, sorted by positionInVerse; omitted when empty
  hints?: MappingHintKey[];     // omitted when empty
}
```

Field → table:

| Field | Backed by | Until the provenance migration (S29) |
|---|---|---|
| `mapping.source` | `…Mapping."CreatedBy"` = AI user id → `"ai"`, else `"human"`; a word with any human row is `"human"` | always `"ai"` |
| `mapping.createdAt` | `MAX(…Mapping."CreatedAt")` over the word's rows | same column (= seed time on a fresh DB) |
| `mapping.model` | `…Mapping."Model"` | omitted |
| `mapping.confirmedAt` | `…Mapping."ConfirmedAt"` when **all** rows of the word are confirmed (`MIN`) | omitted |
| `coMappedWords` | self-join on `StepWordId` (sameOriginal), `ParentGermanWordId` both directions, joined to `Elb1871Words` | available now |
| `readings[].sharedWithWordIds` | the sameOriginal rows grouped by `StepWordId` | available now |
| `readings[].germanWordPart` | `GermanWordPart`, **omitted when equal to the whole word or `*`** | available now |
| `hints` | `manyOriginals`: ≥ 3 readings; `unclearWordPart`: a `GermanWordPart` of `*` | available now |

Example (abridged, hand-written; real values come from STEP at runtime):

```json
{
  "wordId": 26001,
  "wordInContext": "Wort",
  "positionInVerse": 5,
  "germanLemma": "Wort",
  "mapping": { "source": "ai", "createdAt": "2026-03-14T10:00:00Z", "model": "gemini-3.1-flash-lite-preview" },
  "coMappedWords": [{ "wordId": 26000, "wordInContext": "das", "positionInVerse": 4, "relation": "addedWord" }],
  "readings": [
    {
      "stepWordId": 140005,
      "language": "greek",
      "positionInVerse": 5,
      "original": "λόγος",
      "transliteration": "logos",
      "lemma": "λόγος",
      "strongs": "G3056",
      "gloss": "word",
      "contextGloss": "Word",
      "morphology": { "code": "N-NSM", "partOfSpeech": "noun", "case": "nominative", "number": "singular", "gender": "masculine" },
      "lexicon": { "strongs": "G3056", "lemma": "λόγος", "gloss": "word", "definition": "…", "definitionTruncated": true }
    }
  ]
}
```

### Backing queries (Dapper, repo SQL style)

`IWordDetailRepository` (Application/Repositories, impl `Infrastructure/Repositories/WordDetailRepository.cs`):

```csharp
Task<ElbWordRow?> GetWordAsync(int wordId, CancellationToken token = default);
Task<IReadOnlyList<GreekReadingRow>> GetGreekReadingsAsync(int wordId, CancellationToken token = default);
Task<IReadOnlyList<HebrewReadingRow>> GetHebrewReadingsAsync(int wordId, CancellationToken token = default);
```

```sql
-- GetWordAsync
SELECT ew."Id"
     , ew."BibleBookId"
     , ew."Chapter"
     , ew."Verse"
     , ew."HebRefId"
     , ew."WordInContext"
     , ew."Lemma" "GermanLemma"
FROM "unshackled-word"."Elb1871Words" ew
WHERE 1=1
    AND ew."Id" = @WordId
```

```sql
-- GetGreekReadingsAsync (rows with StepWordId NULL are added-word rows)
SELECT egm."IsAddedWord"
     , egm."GermanWordPart"
     , sgw."Id" "StepWordId"
     , sgw."PositionInVerse"
     , sgw."Greek" "Original"
     , sgw."Transliteration"
     , sgw."Lemma"
     , sgw."DisambiguatedStrongs" "Strongs"
     , sgw."Gloss"
     , sgw."English" "ContextGloss"
     , sgw."Morphology" "MorphologyCode"
     , sgm."PartOfSpeech" "StepPartOfSpeech"
     , sgm."Tense" "StepTense"
     , sgm."Voice" "StepVoice"
     , sgm."Mood" "StepMood"
     , sgm."Person" "StepPerson"
     , sgm."Case" "StepCase"
     , sgm."Number" "StepNumber"
     , sgm."Gender" "StepGender"
FROM "unshackled-word"."Elb1871GreekMapping" egm
    LEFT JOIN "unshackled-word"."StepGreekWords" sgw ON sgw."Id" = egm."StepWordId"
    LEFT JOIN "unshackled-word"."StepGreekMorphology" sgm ON sgm."Code" = sgw."Morphology"
WHERE 1=1
    AND egm."ElbWordId" = @WordId
ORDER BY sgw."PositionInVerse" NULLS LAST
```

```sql
-- GetHebrewReadingsAsync
SELECT ehm."IsAddedWord"
     , ehm."GermanWordPart"
     , shw."Id" "StepWordId"
     , shw."PositionInVerse"
     , shw."Hebrew" "Original"
     , shw."Transliteration"
     , shw."RootDisambiguatedStrongsInstance" "Strongs"
     , shw."DisambiguatedStrongs"
     , shw."Gloss" "ContextGloss"
     , shw."Grammar" "MorphologyCode"
FROM "unshackled-word"."Elb1871HebrewMapping" ehm
    LEFT JOIN "unshackled-word"."StepHebrewWords" shw ON shw."Id" = ehm."StepWordId"
WHERE 1=1
    AND ehm."ElbWordId" = @WordId
ORDER BY shw."PositionInVerse" NULLS LAST
```

```sql
-- GetCoMappedWordsAsync (Greek; Hebrew identical on "Elb1871HebrewMapping")
WITH own AS (
    SELECT egm."StepWordId"
         , egm."ParentGermanWordId"
    FROM "unshackled-word"."Elb1871GreekMapping" egm
    WHERE 1=1
        AND egm."ElbWordId" = @WordId
)
SELECT ew."Id" "WordId", ew."WordInContext", ew."PositionInVerse", 'sameOriginal' "Relation", egm."StepWordId"
FROM own
    JOIN "unshackled-word"."Elb1871GreekMapping" egm ON egm."StepWordId" = own."StepWordId"
    JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = egm."ElbWordId"
WHERE 1=1
    AND egm."ElbWordId" <> @WordId
UNION ALL
SELECT ew."Id", ew."WordInContext", ew."PositionInVerse", 'addedWord', NULL
FROM "unshackled-word"."Elb1871GreekMapping" egm
    JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = egm."ElbWordId"
WHERE 1=1
    AND egm."ParentGermanWordId" = @WordId
UNION ALL
SELECT ew."Id", ew."WordInContext", ew."PositionInVerse", 'parent', NULL
FROM own
    JOIN "unshackled-word"."Elb1871Words" ew ON ew."Id" = own."ParentGermanWordId"
```

The service de-duplicates by `WordId` (priority parent > addedWord > sameOriginal), sorts by `PositionInVerse`, and
fills `sharedWithWordIds` per reading from the `StepWordId` column. The provenance aggregate is one extra
`SELECT MAX("CreatedAt") …` (plus `"CreatedBy"`, `"Model"`, `"ConfirmedAt"` after S29) over the word's rows,
or folded into the readings query.

Morphemes: `IHebrewMorphemeRepository.GetByStepWordIdsAsync(int[])` with the SQL of roadmap 0001 S27 unchanged.
Lexicon: existing `IStrongsLexiconRepository.GetEntriesAsync(StrongsNumber)`, once per distinct root Strong's
(1–3 calls; scale is small). Choose the entry whose `Extra` equals the parsed `Extra`; with no `Extra` prefer the
entry with `Extra IS NULL`, else the first.

`WordDetailService.GetAsync(int wordId)` (Application/Features/WordDetail) composes: word → language by
`BibleBookId` (≤ 39 Hebrew, Aramaic when the code starts with `A`) → readings → morphemes (OT) → lexicon → decoders →
`LexiconTextCleaner`. `isAddedWord = rows.Count > 0 && rows.All(r => r.IsAddedWord)`; readings = rows with
`StepWordId` not null, sorted by `PositionInVerse`. Returns `null` for an unknown id (endpoint → 404).

### What the MCP server can share

| Shared piece | MCP use |
|---|---|
| `WordDetailService` | new tool `get_word_detail` (S27) |
| `GreekMorphologyDecoder`, `HebrewMorphologyDecoder` | readable morphology in `OriginalTextFormatter` (0001 S15) and `HebrewWordAnalysisFormatter` (0001 S28), in English via a key → English label map |
| `IHebrewMorphemeRepository` | 0001 S27–S30 (same class, implement once) |
| `LexiconTextCleaner` | `lookup_strongs` shortening |
| Occurrence / renderings repositories (S21–S23) | 0001 S33 `get_elberfelder_renderings`, S35 `find_occurrences` |

## Frontend design

### UX

- **Trigger**: every word is a `Button variant="inline"` with `aria-haspopup="dialog"`, `aria-expanded` and, while
  open, `aria-controls`. Click, tap, Enter and Space open it (native button behaviour). Clicking the open word again
  closes it. Clicking another word switches directly.
- **Desktop (≥ `md`)**: popover anchored below the word (CSS anchor positioning, flips above / to the left when
  there is no room), max width `24rem`, max height `60dvh`, scrolls inside.
- **Mobile (< `md`) and browsers without anchor positioning**: bottom sheet, full width, max height `70dvh`,
  rounded top corners, scrolls inside. Same element, only CSS differs.
- **Focus**: on open, focus moves to the dialog container (`tabIndex={-1}`, labelled by its heading). Tab moves
  through its controls and then on into the page (non-modal). Escape or the close button closes **and returns focus
  to the word**. A pointer press outside closes without moving focus.
- **Hover highlight**: unchanged while closed. While open, the highlight shows the hovered word if any, else the
  open word (`hoveredId ?? selectedId`), so the Strong's group of the open word stays marked after focus moved into
  the pop-up.
- **ArrowLeft/ArrowRight**: ignored while the pop-up is open (reading the pop-up must not flip chapters). Changing
  the chapter (links, picker) closes the pop-up.
- **Several readings**: a counter line ("2 Grundtextwörter"), then one section per reading in original order, each
  with the German word part when present.
- **Language**: German word `lang="de"`, Greek `lang="grc"`, Hebrew `lang="he" dir="rtl"`, Aramaic `lang="arc"
  dir="rtl"`, English glosses/definitions `lang="en"`. All UI labels via i18n (DE/EN).
- **Sample data**: while the mock source is active, a small "Beispieldaten" badge is shown in the header.
- **Mapping provenance (S28)**: a header badge "KI-Vorschlag" (AI, unconfirmed, muted) / "Bestätigt" / "Manuell"
  (accent outline); a footer line "Zuordnung von der KI am 14.03.2026 (model) · bestätigt am …". No badge for
  unmapped words.
- **Words translated together (S28)**: a line "Gemeinsam übersetzt: Und die **Erde**" (verse order, open word bold)
  from `coMappedWords`; added words say "… hat es zu „Anfang“ ergänzt". While the pop-up is open these words are
  **underlined** (`decoration-accent`) in the chapter text, so the underline combines with the Strong's background
  highlight; hover and ArrowLeft/Right behaviour are unchanged. With several readings each reading says
  "gemeinsam mit „Und, die“" from `sharedWithWordIds`.
- **German rendering vs original gloss**: the German word (header, `germanWordPart`) is shown next to the English
  `gloss`/`contextGloss`; no German gloss is generated (Licence).
- **Uncertainty**: hints as notes (`role="note"`), e.g. "Ungewöhnlich viele Grundtextwörter …". No percentage,
  because the data has no confidence.
- **Correction entry point**: "Zuordnung bearbeiten" button in the footer, disabled with "Kommt mit dem
  Tagging-Werkzeug." until the page passes `onEditMapping` (tagging UI roadmap; it can open the editor or the
  "let AI redo this verse range" action from there).

### Component

`WordDetailPopover` (presentational; the page owns state and data). See S06 for props and states.

## Conventions for all steps

### Frontend

- Only inside Docker: `docker compose run --rm --no-deps unshackledword.web bun run check`; `bun run format` to
  auto-fix. Dev server: `docker compose up unshackledword.web` (port 5173).
- Design tokens only (`src/styles/index.css`), no hardcoded colors/spacing/font sizes, no inline `style`, no raw
  `<button>` (Biome plugins in `biome-plugins/`), no arbitrary Tailwind values (`scripts/check-components.ts`).
- Every component in `src/components` is listed in `/Components.md` (repo root). **No new dependency.**
- Tests: Vitest + Testing Library, files next to the code (`*.test.ts(x)`), i18n set with
  `await i18n.changeLanguage("de")` in `beforeEach` like `ChapterPage.test.tsx`.

### Backend

- `dotnet build UnshackledWord.slnx`, `dotnet test`; single project `dotnet test --project tests/<Project>`.
- SQL: quoted schema-qualified identifiers, aliases, leading commas, `WHERE 1=1`, Dapper parameters, never
  interpolated input (the existing `GetWordsForChapter` interpolates ints; do not copy that).
- Interfaces in `Application`, implementations in `Infrastructure`, registration in
  `Infrastructure/Repositories/ServiceCollectionExtensions.AddRepositories()`. Pure logic in Domain with tests in
  `tests/UnshackledWord.Domain.Tests`. Tests use xunit v3, Shouldly, NSubstitute (central versions exist).

---

## Phase 1 spec: frontend on mock data

Everything in this section is in `src/UnshackledWord.Web/` unless the path starts with `/`.

### S01 – Contract types

Append the whole block from [The contract](#the-contract-typescript) to `src/lib/bible/types.ts` (exported, with
the doc comments). No other change. Done when `bun run check` is green.

### S02 – `src/lib/bible/morphology.ts` + grammar i18n

```ts
import type { TFunction } from "i18next";
import type { Morphology } from "./types";

/** Order in which parts are shown. */
export const MORPHOLOGY_PART_ORDER = [
  "partOfSpeech", "stem", "hebrewForm", "tense", "voice", "mood",
  "person", "case", "number", "gender", "state", "degree",
] as const;

/** "Substantiv · Nominativ · Singular · maskulin"; falls back to `description`, then to the code. */
export function formatMorphology(morphology: Morphology, t: TFunction): string;
```

Each present part `p` with value `v` is translated with `t(\`bible.grammar.${p}.${v}\`)`, joined with `" · "`.

Add `bible.grammar` to `src/i18n/de.ts` and `src/i18n/en.ts` (`Translation = typeof de`, so both need every key):

| Group | Key → DE / EN |
|---|---|
| `partOfSpeech` | adjective Adjektiv/adjective · adverb Adverb/adverb · article Artikel/article · conjunction Konjunktion/conjunction · indeclinable indeklinabel/indeclinable · interjection Interjektion/interjection · noun Substantiv/noun · nounProper Eigenname/proper noun · nounProperPerson Personenname/person name · nounProperPlace Ortsname/place name · particle Partikel/particle · preposition Präposition/preposition · pronoun Pronomen/pronoun · suffix Suffix/suffix · verb Verb/verb |
| `tense` | present Präsens/present · imperfect Imperfekt/imperfect · future Futur/future · aorist Aorist/aorist · perfect Perfekt/perfect · pluperfect Plusquamperfekt/pluperfect · futurePerfect Futur II/future perfect |
| `voice` | active Aktiv/active · middle Medium/middle · passive Passiv/passive · middleOrPassive Medium oder Passiv/middle or passive |
| `mood` | indicative Indikativ/indicative · subjunctive Konjunktiv/subjunctive · optative Optativ/optative · imperative Imperativ/imperative · infinitive Infinitiv/infinitive · participle Partizip/participle |
| `person` | first 1. Person/1st person · second 2. Person/2nd person · third 3. Person/3rd person |
| `case` | nominative Nominativ/nominative · genitive Genitiv/genitive · dative Dativ/dative · accusative Akkusativ/accusative · vocative Vokativ/vocative |
| `number` | singular Singular/singular · dual Dual/dual · plural Plural/plural |
| `gender` | masculine maskulin/masculine · feminine feminin/feminine · neuter neutrum/neuter · common beiderlei/common |
| `degree` | comparative Komparativ/comparative · superlative Superlativ/superlative |
| `stem` | qal Qal · niphal Nifal/Niphal · piel Piel · pual Pual · hiphil Hifil/Hiphil · hophal Hofal/Hophal · hithpael Hitpael/Hithpael · other anderer Stamm/other stem |
| `hebrewForm` | perfect Perfekt (Qatal)/perfect (qatal) · imperfect Imperfekt (Jiqtol)/imperfect (yiqtol) · sequentialPerfect Weqatal/sequential perfect (weqatal) · sequentialImperfect Wajjiqtol/sequential imperfect (wayyiqtol) · cohortative Kohortativ/cohortative · jussive Jussiv/jussive · imperative Imperativ/imperative · infinitiveAbsolute Infinitivus absolutus/infinitive absolute · infinitiveConstruct Infinitivus constructus/infinitive construct · participleActive Partizip aktiv/active participle · participlePassive Partizip passiv/passive participle |
| `state` | absolute Status absolutus/absolute · construct Status constructus/construct · determined determiniert/determined |

Tests `src/lib/bible/morphology.test.ts` (use `i18n.t` after `changeLanguage`):

| Input | DE | EN |
|---|---|---|
| `{code:"N-NSM", partOfSpeech:"noun", case:"nominative", number:"singular", gender:"masculine"}` | `Substantiv · Nominativ · Singular · maskulin` | `noun · nominative · singular · masculine` |
| `{code:"V-IAI-3S", partOfSpeech:"verb", tense:"imperfect", voice:"active", mood:"indicative", person:"third", number:"singular"}` | `Verb · Imperfekt · Aktiv · Indikativ · 3. Person · Singular` | `verb · imperfect · active · indicative · 3rd person · singular` |
| `{code:"HNcfsa", partOfSpeech:"noun", gender:"feminine", number:"singular", state:"absolute"}` | `Substantiv · Singular · feminin · Status absolutus` | `noun · singular · feminine · absolute` |
| `{code:"XYZ", description:"Unknown form"}` | `Unknown form` | `Unknown form` |
| `{code:"XYZ"}` | `XYZ` | `XYZ` |

### S03 – Provider seam and mock

**`src/vite-env.d.ts`**: add `readonly VITE_WORD_DETAIL_SOURCE?: "mock" | "api";` to `ImportMetaEnv`.

**`src/lib/bible/api.ts`**: add

```ts
wordDetail: (wordId: number, signal?: AbortSignal) =>
  api.get<GetWordDetailResponse>(`/elberfelder/wordDetail/wordId/${wordId}`, signal),
```

**`src/lib/bible/wordDetail.ts`**:

```ts
export type WordDetailSource = "mock" | "api";
/** Phase 1 default is "mock"; Phase 3 (S17) flips the default to "api". */
export const wordDetailSource: WordDetailSource =
  import.meta.env.VITE_WORD_DETAIL_SOURCE === "api" ? "api" : "mock";

export const wordDetailKeys = {
  detail: (wordId: number) => ["bible", "wordDetail", wordId] as const,
};

/** "G0444" → "G444", "{H7225G}" → "H7225G", "H9003/{H7225G}" → "H7225G", "h0430g_a" → "H430G"; undefined if not a Strong's number. */
export function normalizeStrongs(value: string): string | undefined;

export function fetchWordDetail(
  word: WordToken,
  signal?: AbortSignal,
  source: WordDetailSource = wordDetailSource,
): Promise<GetWordDetailResponse>; // "api" → bibleApi.wordDetail(word.id, signal); "mock" → mockWordDetail(word)

export const wordDetailQuery = (word: WordToken) =>
  queryOptions({
    queryKey: wordDetailKeys.detail(word.id),
    queryFn: ({ signal }) => fetchWordDetail(word, signal),
    staleTime: Number.POSITIVE_INFINITY,
  });

/** Detail of the open word; disabled while `word` is undefined. */
export function useWordDetail(word: WordToken | undefined) {
  return useQuery({ ...wordDetailQuery(word ?? { id: 0, text: "", senses: [] }), enabled: word !== undefined });
}
```

`normalizeStrongs`: take the braced part if any, else the part after the last `/`; cut at the first `_`; match
`^([GH])0*(\d{1,5})([A-Z]{0,3})$` case-insensitively; return upper-case `G|H` + number without leading zeros +
extra.

**`src/lib/bible/wordDetail.mock.ts`** (hand-written, no STEP text copied; see Licence):

```ts
export const MOCK_DELAY_MS = 120;
/** Mock readings keyed by normalized Strong's. */
export const MOCK_READINGS: Record<string, Omit<OriginalReading, "germanWordPart">>;
export function mockWordDetail(word: WordToken, delayMs = MOCK_DELAY_MS): Promise<GetWordDetailResponse>;
```

`mockWordDetail` algorithm:

1. `readings = []`; for each `sense` of `word.senses` (in order): `key = normalizeStrongs(sense.key)`; skip if
   undefined or already used.
2. If `MOCK_READINGS[key]` exists: copy it. If the normalized `entry.original` (letters only, NFC, lower case, same
   rule as `words.ts` `normalizeForm`) differs from `sense.form` and `sense.form !== ""`: set `original = sense.form`
   and drop `transliteration`, `contextGloss`, `morphology`, `morphemes` (same lexeme, other inflection).
3. Else build a fallback: `{ stepWordId: -1, language: key starts with "G" ? "greek" : "hebrew", positionInVerse:
   index + 1, original: sense.form || key, strongs: key }`.
4. Sort by `positionInVerse`; resolve after `delayMs` (`setTimeout`, `0` allowed) with
   `{ wordId: word.id, wordInContext: word.text, readings }`.

Mock entries (Strong's key → reading). Definitions are deliberately placeholder text:

| Key | language | pos | original | translit. | lemma | strongs | gloss | contextGloss | morphology | extra |
|---|---|---|---|---|---|---|---|---|---|---|
| `G3056` | greek | 5 | λόγος | logos | λόγος | G3056 | word | Word | `N-NSM` noun, nominative, singular, masculine | lexicon `{strongs:"G3056", lemma:"λόγος", transliteration:"logos", gloss:"word", definition:"Sample text: the real lexicon definition is loaded live from the STEP data.", definitionTruncated:true}` |
| `G1510` | greek | 3 | ἦν | ēn | εἰμί | G1510 | to be | was | `V-IAI-3S` verb, imperfect, active, indicative, third, singular | lexicon `{strongs:"G1510", lemma:"εἰμί", gloss:"to be"}` |
| `G4250` | greek | 14 | πρὶν | prin | πρίν | G4250 | before | before | `ADV` adverb | — |
| `G2228` | greek | 15 | ἢ | ē | ἤ | G2228 | or, than | – | `PRT` particle | — |
| `H7225G` | hebrew | 1 | בְּרֵאשִׁית | be·re·shit | רֵאשִׁית | H7225G | beginning | in the beginning | `HR/Ncfsa` noun, feminine, singular, absolute | morphemes: prefix `בְּ` H9003 "in" `{code:"HR", partOfSpeech:"preposition"}`; root `רֵאשִׁית` H7225G "beginning" `{code:"HNcfsa", noun, feminine, singular, absolute}` |
| `H430G` | hebrew | 3 | אֱלֹהִים | e·lo·him | אֱלֹהִים | H0430G | God | God | `HNcmpa` noun, masculine, plural, absolute | morphemes: root `אֱלֹהִים` H0430G "God" `{code:"HNcmpa", …}` |

The six mock cases the UI must handle: λόγος (Joh 1,1 "Wort"), ἦν (Joh 1,1 "war"; the fixture's "war" `G1510` hits
it, "ist" `ἐστιν` shows the form-differs path), "ehe" with **two readings** πρίν + ἤ (Mt 1,18 "ehe sie
zusammengekommen waren"; token senses `G4250`, `G2228`), בְּרֵאשִׁית with morphemes (1Mo 1,1 "Anfang"),
אֱלֹהִים (1Mo 1,1 "Gott"), and a word **without mapping** (token with no senses, e.g. "Es" → `readings: []`).

Tests `src/lib/bible/wordDetail.test.ts`:

- `normalizeStrongs` table: `G0444→G444`, `G3056→G3056`, `{H7225G}→H7225G`, `H9003/{H7225G}→H7225G`,
  `h0430g_a→H430G`, `H0430G→H430G`, `""→undefined`, `abc→undefined`.
- `mockWordDetail({id:2,text:"war",senses:[{key:"G1510",form:"ἦν"}]}, 0)` → one reading, `original` ἦν, morphology
  code `V-IAI-3S`.
- token "ist" `{key:"G1510", form:"ἐστιν"}` → `original` ἐστιν, lemma εἰμί, no `morphology`.
- token "ehe" with senses `G4250`, `G2228` → two readings ordered πρὶν, ἢ.
- token "Anfang" `{key:"H7225G", form:"בְּרֵאשִׁית"}` → `morphemes` length 2, first `role` prefix.
- token without senses → `readings: []`, `wordInContext` kept.
- unknown key `G9999` → fallback reading with `strongs:"G9999"`, `stepWordId:-1`.
- `fetchWordDetail(token, undefined, "api")` calls `fetch` with `/elberfelder/wordDetail/wordId/2` (stub `fetch`).

### S04 – `Button` variant `inline`

`src/components/Button.tsx`: add variant `inline` for text-flow buttons (scripture words). For `inline`,
`buttonClasses` returns **only** `"cursor-pointer rounded-sm px-0.5 transition-colors"` (no `inline-flex`,
`font-medium`, size padding or variant colors, so the word keeps the font, size and line-height of the text;
Tailwind preflight already sets `font: inherit` and transparent background on buttons). `size` is ignored for
`inline`. Update the `Button` row in `/Components.md`: "Variants `primary` / `secondary` / `ghost` / `inline` (text
flow, inherits font) …".

Test in `Button.test.tsx`: `buttonClasses({ variant: "inline" })` does not contain `inline-flex`, `font-medium`,
`px-4`; a rendered inline Button still has `type="button"`.

### S05 – CSS in `src/styles/index.css`

Add to `@theme inline`:

```css
--font-original: "SBL BibLit", "Noto Serif Hebrew", "Noto Serif", "Iowan Old Style", "Palatino Linotype", "Times New Roman", serif;
--container-popover: 24rem;
```

(gives `font-original`, `max-w-popover`). Add the positioning layer (layout only, colors stay Tailwind tokens in
the component):

```css
@layer components {
  /* The one open word is the anchor; only one word carries this class at a time. */
  .word-anchor {
    anchor-name: --uw-word;
  }

  /* Mobile first and fallback: bottom sheet. Overrides the UA [popover] centering. */
  .word-popover {
    position: fixed;
    inset: auto 0 0 0;
    margin: 0;
    width: 100%;
    max-width: none;
    max-height: 70dvh;
    overflow-y: auto;
    border-radius: var(--radius-lg) var(--radius-lg) 0 0;
  }

  @media (width >= 48rem) {
    @supports (anchor-name: --uw-word) {
      .word-popover {
        position-anchor: --uw-word;
        position-area: block-end span-inline-end;
        position-try-fallbacks: flip-block, flip-inline, flip-block flip-inline;
        inset: auto;
        margin: calc(var(--spacing) * 1) 0;
        width: max-content;
        max-width: min(var(--container-popover), calc(100vw - var(--spacing) * 8));
        max-height: 60dvh;
        border-radius: var(--radius-lg);
      }
    }
  }
}
```

Done when `bun run check` is green (no component uses the classes yet).

### S06 – `src/components/WordDetailPopover.tsx`

Propose it in chat first (CLAUDE.md), then add to `/Components.md`:
`| \`WordDetailPopover\` | Pop-up with the Greek/Hebrew readings of one Elberfelder word (native popover; anchored on desktop, bottom sheet on mobile); props \`word\`, \`status\`, \`detail\`, \`isSample\`, \`onRetry\`, \`onClose\`. |`

```ts
export type WordDetailCloseReason = "escape" | "button" | "outside";

export interface WordDetailPopoverProps {
  /** id for aria-controls on the trigger */
  id: string;
  /** the clicked Elberfelder word (header text while loading) */
  word: WordToken;
  status: "pending" | "error" | "success";
  detail?: GetWordDetailResponse;
  /** show the "sample data" badge (mock source) */
  isSample?: boolean;
  onRetry: () => void;
  onClose: (reason: WordDetailCloseReason) => void;
}
```

Structure and behaviour:

- Root: `<div id={id} popover="manual" role="dialog" aria-labelledby={headingId} tabIndex={-1} ref={…}
  className="word-popover border border-border bg-surface p-4 text-fg shadow-lg">`. In `useLayoutEffect` on mount:
  `if (typeof el.showPopover === "function") el.showPopover()`, then `el.focus()`. Unmount removes it (the page
  renders the component only while open). `headingId` comes from `useId()`.
- Header: `<h2 id={headingId} lang="de" className="font-scripture text-xl">{word.text}</h2>`, a badge
  `t("bible.wordDetail.sample")` when `isSample` (`rounded-sm bg-surface-muted px-1 text-xs text-fg-muted`), and
  `<Button variant="ghost" size="sm" aria-label={t("bible.wordDetail.close")} onClick={() => onClose("button")}>×</Button>`.
  The dialog's accessible name comes from `aria-labelledby`; add a visually hidden prefix
  `t("bible.wordDetail.dialogLabel", { word: word.text })` inside the heading via `sr-only`.
- `status === "pending"`: `<p aria-busy="true" className="text-fg-muted">{t("bible.wordDetail.loading")}</p>`.
- `status === "error"`: `<div role="alert">` with `t("bible.wordDetail.error")` and
  `<Button variant="secondary" size="sm" onClick={onRetry}>{t("bible.retry")}</Button>`.
- `status === "success"` and `detail.readings.length === 0`: `t(detail.isAddedWord ? "bible.wordDetail.addedWord" :
  "bible.wordDetail.noMapping")` in `text-fg-muted`.
- Readings: if `> 1`, a line `t("bible.wordDetail.readings", { count })`. Each reading is a `<section
  aria-labelledby>`; sections after the first get `mt-4 border-t border-border pt-4`. Inside:
  - `germanWordPart` → `t("bible.wordDetail.germanPart", { part })` (`text-sm text-fg-muted`),
  - original: `<p lang={grc|he|arc} dir={rtl for hebrew/aramaic} className="font-original text-2xl">`, then
    transliteration in `italic text-fg-muted`,
  - a `<dl className="mt-2 grid grid-cols-2 gap-x-3 gap-y-1 text-sm">` (no arbitrary grid templates) with `dt` in
    `text-fg-muted`; rows: Strong's, lemma (`lang`),
    grammar (`formatMorphology` + the code in `<code className="text-xs text-fg-muted">`), gloss (`lang="en"`),
    context gloss (`lang="en"`). Rows with no value are not rendered.
  - morphemes (if any): heading `t("bible.wordDetail.field.morphemes")`, `<ol>` in word order, each
    `t("bible.wordDetail.morpheme.<role>")`, original (`lang`, `dir`), Strong's, gloss, formatted morphology.
  - lexicon definition: native `<details>` with `<summary>{t("bible.wordDetail.field.definition")}</summary>` and
    `<p lang="en">`; append `t("bible.wordDetail.truncated")` in muted text when `definitionTruncated`.
- Footer when at least one reading: `<p className="mt-4 text-xs text-fg-muted">{t("bible.wordDetail.attribution")}</p>`.
- Escape: a `keydown` listener on `document` (capture) while mounted: `Escape` → `preventDefault`,
  `stopPropagation`, `onClose("escape")`.
- Outside press: a `pointerdown` listener on `document` while mounted: if the target is outside the root **and** not
  inside `[data-word-trigger]` → `onClose("outside")`. (Word triggers handle their own toggle/switch.)

i18n `bible.wordDetail` (DE / EN):

| Key | DE | EN |
|---|---|---|
| `dialogLabel` | Grundtext zu „{{word}}“ | Original text for “{{word}}” |
| `close` | Schließen | Close |
| `loading` | Grundtext wird geladen … | Loading original text … |
| `error` | Die Angaben zum Grundtext konnten nicht geladen werden. | The original-text details could not be loaded. |
| `noMapping` | Dieses Wort ist noch keinem Grundtextwort zugeordnet. | This word is not linked to the original text yet. |
| `addedWord` | Dieses Wort steht nicht im Grundtext, der Übersetzer hat es ergänzt. | This word is not in the original text; the translator added it. |
| `readings_one` | {{count}} Grundtextwort | {{count}} original word |
| `readings_other` | {{count}} Grundtextwörter | {{count}} original words |
| `germanPart` | Wortteil „{{part}}“ | Word part “{{part}}” |
| `sample` | Beispieldaten | Sample data |
| `truncated` | (gekürzt) | (shortened) |
| `attribution` | Grundtextdaten: STEPBible.org (Tyndale House), CC BY 4.0 | Original-language data: STEPBible.org (Tyndale House), CC BY 4.0 |
| `language.greek` / `.hebrew` / `.aramaic` | Griechisch / Hebräisch / Aramäisch | Greek / Hebrew / Aramaic |
| `field.strongs` | Strong-Nr. | Strong's |
| `field.lemma` | Grundform | Lemma |
| `field.transliteration` | Umschrift | Transliteration |
| `field.morphology` | Grammatik | Grammar |
| `field.gloss` | Bedeutung (engl.) | Meaning |
| `field.contextGloss` | Hier (engl.) | Here |
| `field.morphemes` | Wortbestandteile | Word parts |
| `field.definition` | Lexikon (engl.) | Lexicon |
| `morpheme.prefix` / `.root` / `.suffix` | Präfix / Wortstamm / Suffix | Prefix / Root / Suffix |
| `trigger` | Grundtext zu „{{word}}“ anzeigen | Show original text for “{{word}}” |

Tests `src/components/WordDetailPopover.test.tsx` (build `detail` objects inline; use `mockWordDetail` only where
noted). In `beforeEach`, polyfill `HTMLElement.prototype.showPopover ??= vi.fn()`:

1. pending → loading text, `aria-busy`; dialog named by the word.
2. error → `role="alert"`; clicking "Erneut versuchen" calls `onRetry`.
3. success, `readings: []` → noMapping text; with `isAddedWord: true` → addedWord text; no attribution.
4. λόγος detail (from `mockWordDetail`) → shows λόγος with `lang="grc"`, `G3056`, "Substantiv · Nominativ ·
   Singular · maskulin", `N-NSM`, attribution.
5. Two readings with `germanWordPart` "Menschen"/"sohn" (synthetic: υἱός G5207, ἄνθρωπος G444) → counter "2
   Grundtextwörter", both parts, order by `positionInVerse`.
6. בְּרֵאשִׁית → `dir="rtl"` `lang="he"`, morphemes list with "Präfix" and "Wortstamm".
7. Escape → `onClose("escape")`; close button → `onClose("button")`; pointerdown on `document.body` →
   `onClose("outside")`; pointerdown on an element with `data-word-trigger` → not called.
8. Focus is on the dialog after mount.
9. `isSample` shows "Beispieldaten".
10. English: after `changeLanguage("en")`, labels are English ("Grammar", "noun · nominative …").

### S07 – Wire into `src/pages/ChapterPage.tsx`

- State: `const [selectedId, setSelectedId] = useState<number>();`, a `triggerRef = useRef<HTMLElement | null>(null)`.
  `selectedWord = verses.data?.flatMap((v) => v.words).find((w) => w.id === selectedId)`.
- Data: `const detail = useWordDetail(selectedWord);`.
- Highlight: `const activeId = hoveredId ?? selectedId;` replaces `hoveredId` in the `buildHighlights` memo.
- Each word becomes:

  ```tsx
  <Button
    variant="inline"
    data-word-trigger=""
    aria-haspopup="dialog"
    aria-expanded={selectedId === word.id}
    aria-controls={selectedId === word.id ? popoverId : undefined}
    aria-label={t("bible.wordDetail.trigger", { word: word.text })}  // see open question on verbosity
    onClick={(e) => toggle(word, e.currentTarget)}
    onMouseEnter/onMouseLeave/onFocus/onBlur – as today, only when word.senses.length > 0
    className={`${kind ? HIGHLIGHT_CLASSES[kind] : ""} ${selectedId === word.id ? "word-anchor" : ""}`}
  >
    {word.text}
  </Button>{" "}
  ```

  The old `<span tabIndex>` and its `biome-ignore` comment go away.
- `toggle(word, el)`: if `selectedId === word.id` → close; else `triggerRef.current = el; setSelectedId(word.id)`.
- `close(reason)`: `setSelectedId(undefined)`; if `reason !== "outside"` → `triggerRef.current?.focus()`.
- Render once, after the `<article>`: `{selectedWord && <WordDetailPopover id={popoverId} word={selectedWord}
  status={detail.status} detail={detail.data} isSample={wordDetailSource === "mock"} onRetry={() =>
  detail.refetch()} onClose={close} />}` with `popoverId = useId()`.
- Arrow guard: in the existing keydown effect add `if (selectedId !== undefined) return;` before the arrow checks
  and add `selectedId` to the dependency array.
- Chapter change: `useEffect(() => setSelectedId(undefined), [ref?.bookId, ref?.chapter]);`.

Tests added to `src/pages/ChapterPage.test.tsx` (fixture `fixtureChapterWords`, mock source is the default; polyfill
`showPopover` as in S06; use `findBy…` because of `MOCK_DELAY_MS`):

1. Clicking "Mensch" (`G0444`, fallback reading) opens a dialog named "Grundtext zu „Mensch“" with `G444`.
2. Focus "war" (id 2) and press Enter → dialog shows ἦν and `V-IAI-3S`; the trigger has `aria-expanded="true"`.
3. Escape closes the dialog and focus is back on that "war".
4. Clicking the open word again closes; clicking another word switches the dialog content.
5. Pointerdown outside closes; focus does not jump to the word.
6. With the dialog open, ArrowRight does **not** navigate (`loc` unchanged); after closing it does.
7. With the dialog open for "war" (id 2), word id 10 still has `bg-mark-same` (selected group stays highlighted).
8. Clicking "Es" (no senses) opens the dialog with the noMapping text.
9. The existing hover test still passes unchanged.

Manual check (Done when): dev server against the real API, Joh 1 and 1Mo 1: click "Wort", "war", "Anfang",
"Gott" → anchored pop-up on desktop width, bottom sheet at 375 px, light and dark theme, DE and EN.

---

## Phase 2 details: API

### S08 – `FakeDbReader`

Exactly roadmap 0001 S08 (`tests/UnshackledWord.Infrastructure.Tests` references Infrastructure/Application;
`FakeDbReader : IDbReader` records SQL and parameters and returns queued rows). Skip if already done.

### S09 – `GreekMorphologyDecoder`

`src/UnshackledWord.Domain/Models/Grammar/GreekMorphologyDecoder.cs`:
`public static MorphologyKeys Decode(string code)` where `MorphologyKeys` is a Domain record with string properties
named like the TS `Morphology` keys (values are the TS key strings; constants in a static class `MorphologyKey`).
Robinson codes as used by TAGNT:

- word classes: `N` noun, `V` verb, `A` adjective, `T` article, `P`/`R`/`C`/`D`/`K`/`I`/`X`/`Q`/`F`/`S` pronoun,
  `ADV` adverb, `CONJ` conjunction, `PREP` preposition, `PRT` particle (`PRT-N` too), `INJ` interjection,
  `N-PRI`, `N-LI`, `N-OI`, `A-NUI` and other `…I` indeclinable forms → indeclinable (proper names `N-PRI` →
  nounProper).
- noun/adjective/article/pronoun: `-{case}{number}{gender}` (`N` nom, `G` gen, `D` dat, `A` acc, `V` voc; `S`/`P`;
  `M`/`F`/`N`), personal pronouns `P-1NS` (person + case + number), adjective suffix `-C` comparative, `-S`
  superlative.
- verbs: `V-{tense}{voice}{mood}[-{person}{number} | -{case}{number}{gender}]`, optional `2` before tense (second
  aorist etc., ignored). Tense `P I F A R L` (+ `X` → description); voice `A M P` active/middle/passive,
  `E N` → middleOrPassive, `D` → middle, `O` → passive; mood `I S O M N P`.
- anything not matched → empty keys (the service then fills `description` from `StepGreekMorphology`).

Test table (at least): `N-NSM`, `N-GPF`, `V-IAI-3S`, `V-AAP-NSM`, `V-PAN`, `V-2AAI-3S`, `T-NSM`, `P-1NS`, `R-ASN`,
`A-NSM-C`, `ADV`, `CONJ`, `PREP`, `PRT-N`, `N-PRI`, `XYZ` (empty).

### S10 – `HebrewMorphologyDecoder`

Same folder. `Decode(string code)` returns `MorphologyKeys` for the **root** segment plus
`IReadOnlyList<MorphologyKeys> Segments`. Format `HR/Ncfsa`: first letter = language (`H` Hebrew, `A` Aramaic), then
segments separated by `/`, the language letter is not repeated. Segment grammar (OSHB notation):

- `A` adjective, `C` conjunction, `D` adverb, `N` noun (`c` common, `g` gentilic, `p` proper → nounProper, `x`),
  `P` pronoun, `R` preposition, `S` suffix (`Sp` pronominal with person/gender/number, `Sd`, `Sh`, `Sn`), `T`
  particle (`Td` → article, others particle), `V` verb.
- nominal tail: gender `m f b c` (`b`/`c` → common), number `s p d`, state `a c d`.
- verb: stem letter (`q` qal, `N` niphal, `p` piel, `P` pual, `h` hiphil, `H` hophal, `t` hithpael, others →
  other), conjugation (`p` perfect, `q` sequentialPerfect, `i` imperfect, `w` sequentialImperfect, `h` cohortative,
  `j` jussive, `v` imperative, `r` participleActive, `s` participlePassive, `a` infinitiveAbsolute, `c`
  infinitiveConstruct), then person `1 2 3`, gender, number, state.
- root segment = last segment that is not `S…`.

Tests: `HR/Ncfsa`, `HNcmpa`, `HC/Vqw3ms`, `HVqp3ms`, `HTo`, `HNcmsc/Sp3ms`, `HTd/Ncmpa`, `ANcmsd` (Aramaic),
`HVhi3ms`, `XYZ`. **Verify against the DB first** (see open questions).

### S11 – `LexiconTextCleaner`

`Domain/Models/Study/LexiconTextCleaner.cs`: `public static (string Text, bool Truncated) Clean(string? raw, int
maxLength = 600)`: remove tags (`<…>`), decode basic entities, collapse whitespace, cut at the last space before
`maxLength` and append `…`. Tests: null/empty, plain text, `<b>x</b><br/>y` → `x y`, long text → truncated flag.

### S12 – Contract classes

`src/UnshackledWord.Domain.WebApi/Elberfelder/GetWordDetail/`: `GetWordDetailRequest { int WordId }`,
`GetWordDetailResponse`, `OriginalReadingResponse`, `MorphologyResponse`, `MorphemeResponse`,
`LexiconSummaryResponse` with properties matching the TS contract (PascalCase; serializer makes camelCase and omits
nulls; `bool? IsAddedWord`, `bool? DefinitionTruncated`; key values as `string?`). Test: serialize a hand-built
response with the same options as `Tooling.WebApi/Program.cs` and compare to the JSON fixture (moved into S18
once the fixture exists; in S12 compare to an inline string).

### S13 – `IWordDetailRepository`

Interface `src/UnshackledWord.Application/Repositories/IWordDetailRepository.cs`, read models
`ElbWordRow`, `GreekReadingRow`, `HebrewReadingRow` in `Domain/Models/Study/`, implementation
`src/UnshackledWord.Infrastructure/Repositories/WordDetailRepository.cs` with the SQL from
[Backing queries](#backing-queries-dapper-repo-sql-style); register in `AddRepositories()`. Tests with
`FakeDbReader`: SQL contains `"Elb1871GreekMapping"` / `"Elb1871HebrewMapping"` and `@WordId`, parameter value
passed.

### S14 – `IHebrewMorphemeRepository`

As roadmap 0001 S27 (read model `HebrewMorpheme`, `GetByStepWordIdsAsync(int[])`). Mark 0001 S27 done too.

### S15 – `WordDetailService`

`src/UnshackledWord.Application/Features/WordDetail/WordDetailService.cs` (+ `IWordDetailService`), registered next
to the repositories. Returns `GetWordDetailResponse?`. Rules in [Backing queries](#backing-queries-dapper-repo-sql-style);
Hebrew whole-word morphology = decoder result of the root segment; morphemes zipped with decoder segments by
position; `description` from `StepGreekMorphology` columns joined with ", " only when the decoder returns no
`partOfSpeech`. Add project references and NSubstitute/Shouldly to `tests/UnshackledWord.Application.Tests` (central
versions exist). Tests: NT word, OT word with morphemes, two readings sorted, added word, no rows, unknown id → null,
lexicon `Extra` selection, Strong's that fails to parse → reading without lexicon.

### S16 – Endpoint

`src/UnshackledWord.Tooling.WebApi/Endpoints/Elberfelder/GetWordDetail/Endpoint.cs`:
`Ep.Req<GetWordDetailRequest>.Res<GetWordDetailResponse>`, `Get("wordDetail/wordId/{WordId:int}")`,
`Group<RouteGroupConfig>()`, `AllowAnonymous()` if the group requires it; `null` → `await Send.NotFoundAsync(ct)`
(match the FastEndpoints version's API); set `HttpContext.Response.Headers.CacheControl = "public, max-age=3600"`.
Add `api/unshackled-word/Elberfelder/GetWordDetail.bru`. Done when the two manual calls from the table work.

---

## Phase 3 details: swap

### S17 – Default to the API

`wordDetail.ts`: default becomes `"api"` (`=== "mock" ? "mock" : "api"`). `compose.yml` service
`unshackledword.web`: add `VITE_WORD_DETAIL_SOURCE=api`. Vitest: set `VITE_WORD_DETAIL_SOURCE=mock` in
`vite.config.ts` `test.env` so page tests keep using the mock. `ApiError` 404 → render the noMapping state (word
vanished after a re-seed) instead of the error state; test it.

### S18 – Shared contract fixture

Hand-written `src/UnshackledWord.Web/src/test/fixtures/wordDetail.sample.json` (no STEP text; the λόγος and
בְּרֵאשִׁית mock entries in API shape). TS: `wordDetail.contract.test.ts` imports it with
`satisfies GetWordDetailResponse` and renders it through `WordDetailPopover`. C#: a test in
`tests/UnshackledWord.Domain.Tests` (or a new `Tooling.WebApi.Tests`) reads the file via a relative path,
deserializes into `GetWordDetailResponse`, re-serializes with the API options and compares JSON (order-insensitive).

### S19 – Hover-intent prefetch

`ChapterPage`: on `onMouseEnter`/`onFocus` of a word with senses start a 200 ms timer that calls
`queryClient.prefetchQuery(wordDetailQuery(word))`; clear it on leave/blur. Test with fake timers.

## Phase 4 details: extras (short)

- **S20** `0006_WordDetailIndexes.sql` (done; there is no `0003`): `CREATE INDEX IF NOT EXISTS` on
  `"StepGreekWords" ("DisambiguatedStrongs")`, an expression index on
  `split_part("RootDisambiguatedStrongsInstance", '_', 1)` of `"StepHebrewWords"` (root without the `_A` instance
  suffix, so the Hebrew count/renderings queries use the same expression) and on `"StepWordId"` of both mapping
  tables. The maintainer must apply it on the DB copy with a privileged user (the API user is read-only).
  `StepStrongsToVerses` decision still open (not used by the pop-up).
- **S21** `occurrenceCount` (done): `COUNT(*)` per root Strong's within the testament; label `"{{n}}× im NT"` / `im AT`.
- **S22** (done; `renderings: [{word, count}]` per reading, case-insensitive grouping, most common spelling shown,
  inflected forms are counted separately) Renderings: query of roadmap 0001 S33 behind `IWordStudyRepository`; pop-up section "Elberfelder übersetzt
  mit" (top 5, CC0 data).
- **S23** (done) Occurrences: query of 0001 S35, endpoint `GET /api/elberfelder/strongsOccurrences/strongs/{Strongs}?page=1&pageSize=50`
  (page 1-based, pageSize 1–200, 400 on a bad Strong's number or paging; accepts `g26`, `H1254A`, `H1254A_B`).
  Response `{strongs, language, page, pageSize, totalItems, totalPages, items:[{bibleBookId, chapter, verse, original, before, keyword, keywordSuffix?, after}]}`
  with token lists `{text, matched}` for the keyword-in-context (KWIC) layout of the concordance page: the keyword spans the first to
  the last mapped Elberfelder word (words in between: `matched=false`), `keywordSuffix` is punctuation trailing the keyword (drawn outside
  the keyword box), an unmapped word has an empty keyword and the whole verse in `after`. One item per original word
  (so `totalItems` equals `occurrenceCount`; a verse with three hits gives three rows), ordered by `LxxRefId`, position, id; Elberfelder
  verse numbering via `Elb1871Verses`. Draft: `docs/drafts/concordance-kwic-draft.html`.
  Open for later (defaults taken): no extra sort options (by German rendering or neighbouring word, classic KWIC); the inflected
  original form is shown, not the lemma; no line cutting on desktop, only the phone cut (3 lines per side with fade, below 40rem);
  Hebrew rows keep the German context LTR with the original above the keyword; other hits in the same verse are not marked. `GetOccurrencesAsync` on `IWordDetailRepository`, `StrongsOccurrenceService`.
  Frontend: page `/bible/strongs/:strongs?page=` (`StrongsPage`, the AstroWeb "Konkordanz" placeholder), pop-up link "Alle Stellen".
- **S24** (done) TSK: **inline** in `wordDetail` (decision for open question 8): `crossReferences[] {bibleBookId, chapter, verse, endVerse?}`
  plus `crossReferencesTruncated` (omitted when empty/false). `WordDetailService` calls `ITskRepository.GetOutgoingAsync(HebRefId, HebRefId, 21)`
  (no new SQL), lists the first 20 and flags the 21st row. Inline because the response is already cached 1 h per word, the list is
  small (~30 B per target) and a lazy endpoint would add a route plus a second loading state for no gain. Every word of a verse gets
  the same list (verse level). Data: `Tsk` has 378,744 rows over 29,055 verses, `Scope` is an English keyword of the source verse
  (not shown), order is `Tsk.Id`; up to 113 targets per verse; 44,059 targets are ranges, none crosses a chapter; 21 ranges run
  backwards (`58009021`→`58009015`) and 2 have start = end, the service swaps/collapses them. Source and targets are translated
  Lxx→Heb by the repository. Frontend: `CrossReferences` section in `WordDetailPopover` (`<details>`, open up to 5 targets, collapsed
  above), links `/bible/:bookId/:chapter#v{verse}`, book names via the new `books` prop (ChapterPage passes `useBibleStructure` data).
  TSK is public domain; no STEP data involved. A verse menu stays possible later (lazy endpoint) if the list should grow.
- **S25** (done) People/places: `readings[].entity {kind: person|place, name, brief}`, `IWordDetailRepository.GetNamedEntityAsync`
  (`UNION ALL` over `StepPersonLexicon` / `StepPlaceLexicon`, `UPPER("Strongs") = UPPER(@Strongs)`, `Brief <> ''`, lowest `Id`).
  Data check (DB copy): 1581 persons, 480 places, all with a `Strongs` of the form `G3972G` / `H0085` / `H1035G` (letter suffix
  = STEP disambiguation, equal to `StepGreekWords.DisambiguatedStrongs` and the root of `RootDisambiguatedStrongsInstance`
  without `_A`). Match is exact including the letter, Hebrew instance suffix stripped in the service; no base-number fallback
  (it would attach the wrong John). Gaps: `LxxRefId` of both tables is the entity's *first occurrence*, not a per-word link, so it
  is not used; unnamed persons carry `G0000`/`H0000` (never match); persons sharing one Strong's number (e.g. several
  Johns differ by letter, so fine) fall back to the lowest `Id`; names with `_` (`father_of_Lazarus`) are shown with spaces;
  `Brief` is English, shown with `lang="en"`, cleaned with `LexiconTextCleaner`. Table scan without an index on `Strongs`
  (about 2000 rows, no 0006 dependency). `Article`/`Short` are not shown.
- **S26** (done) SBL apparatus: NT only, hint key `textVariants` ("Textvarianten in diesem Vers") in `hints`.
  `IWordDetailRepository.HasTextVariantsAsync(HebRefId)` = `EXISTS` on `SblApparatus."LxxRefId"` (indexed). `Elb1871Words.HebRefId`
  of NT words equals the apparatus `LxxRefId` (`book*1000000 + chapter*1000 + verse`, checked for Matthew 1:5). 4441 of the NT
  verses have a row (about 60 % of the NT words are in such verses). The apparatus text itself (Greek, sigla like WH/NA28/RP) is
  not returned, only the hint, so no SBLGNT text is copied or translated.
- **S27** MCP `get_word_detail`: parameters `reference` + `word` (German word or position), resolves the
  `Elb1871Words.Id`, calls `WordDetailService`, formats in English via the decoder keys.

## Risks

| Risk | Impact | Mitigation |
|---|---|---|
| STEP text copied into the repo (mocks, fixtures, snapshots) or into CC0 seeds | Licence breach | Mocks/fixtures hand-written (S03, S18); review rule in Licence section; no snapshot tests of real API output |
| CC BY attribution missing | Licence breach | Attribution footer is part of S06 tests |
| `RootDisambiguatedStrongsInstance` format (instance suffix like `_A`, compound values) | Wrong lexicon lookup, broken highlight keys | `normalizeStrongs` cuts `_…`; `StrongsNumber.Parse` fails on `_` today → strip before parsing in S15; verify values (open question) |
| Morphology codes differ from the documented Robinson/OSHB notation in STEP files | Missing/wrong grammar labels | Decoders return empty keys on doubt; `description` fallback; verify with DISTINCT queries before S09/S10 |
| CSS anchor positioning not supported in a browser | Pop-up not anchored | `@supports` fallback is the bottom sheet, fully usable |
| Popover API missing in jsdom | Tests fail | Guarded `showPopover` call + prototype polyfill in tests |
| Every word a tab stop (hundreds per chapter) | Tedious keyboard navigation | Same as today for mapped words; roving tabindex as follow-up (open question) |
| Chapter-level highlight and pop-up both re-render all words | Jank on long chapters (Ps 119) | `buildHighlights` already memoized; measure in S07; memoize the word list if needed |
| Lexicon text contains markup / is long | Unsafe or huge pop-up | Server-side `LexiconTextCleaner`, rendered as text only (never `dangerouslySetInnerHTML`) |
| Existing `GetWordsForChapter` interpolates ints into SQL | Bad pattern spreads | New code uses parameters; fix as separate small change |
| `StepStrongsToVerses` table missing in migrations | Repository fails if used | Not used here; decide in S20 |
| Mapping rows change (AI redo, tagging) while the pop-up data is cached | Stale detail | 1 h HTTP cache, in-memory cache per session; acceptable at this scale |
| Aramaic detection by code prefix is an assumption | Wrong `lang` attribute | Verify; default to Hebrew |
| No WebApi test project | Endpoint only manually tested | Logic lives in the tested service; S18 covers the contract |
| Badge says "KI-Vorschlag" for everything until S29/S30 | Hand fixes look like AI | Honest default (all current rows are AI); S29 adds `CreatedBy` |
| `CreatedAt` is the seed time on a fresh DB | Misleading date | Label is "Zuordnung … am" (when it entered this DB); S30 can carry the AI run date in the seed CSV |
| Phrase via shared `StepWordId` over-groups when Gemini over-assigned (Joh 18,19 `Der`) | Long, odd phrase lines | `manyOriginals` hint; tagging UI fixes the rows |
| Mock phrase/added heuristics (same form, article before a mapped word) differ from real rows | Surprises after the swap | Mock is badged "Beispieldaten"; S31 tests use real-shaped rows |

## Open questions / to verify during implementation

1. **Interactive unmapped words:** should words without any mapping open the pop-up (current decision: yes, with
   "not linked yet") or stay plain text?
2. **Trigger label:** `aria-label` "Grundtext zu „war“ anzeigen" on every word makes screen-reader reading of the
   verse verbose. Alternative: no `aria-label` (button name = word) and `aria-description`. Maintainer preference?
3. **Shortening STEP lexicon text** (600 chars + "gekürzt") and stripping markup: acceptable as formatting under the
   "do not modify" rule? Otherwise show the full text in a scroll area.
4. **Attribution wording** and whether a link to stepbible.org is wanted; confirm CC BY 4.0 for all used files.
5. Verify formats with SQL before S09/S10/S15:
   `SELECT DISTINCT "Morphology" FROM "unshackled-word"."StepGreekWords" ORDER BY 1;`,
   `SELECT DISTINCT "Grammar" FROM "unshackled-word"."StepHebrewWords" ORDER BY 1 LIMIT 200;`,
   `SELECT "RootDisambiguatedStrongsInstance", "DisambiguatedStrongs" FROM "unshackled-word"."StepHebrewWords" WHERE "LxxRefId" = 1001001;`,
   `SELECT "Code" FROM "unshackled-word"."StepHebrewMorphology" LIMIT 50;` (does `Code` match a whole `Grammar`
   value or one segment?).
6. Are Aramaic words in `StepHebrewWords` marked by a leading `A` in `Grammar`?
7. Does `StepStrongsLexicon` contain entries for disambiguated extras like `H7225G` / `H0430G`, and Greek variants
   with `Extra`?
8. ~~Should TSK cross references live in the word pop-up (they are verse level) or in a later verse menu?~~ Decided in S24: inline in the word pop-up, first 20 targets, collapsed above 5; a verse menu can reuse the same data later.
9. Roving tabindex for words (one tab stop per verse, arrows inside) – conflicts with ArrowLeft/Right chapter
   navigation; worth a separate decision.
10. MCP access policy is undecided (CLAUDE.md); S27 only adds a read-only tool.
11. **AI user id and users table:** which id is the AI user (`CreatedBy`), and does it come from the vocab trainer's
    auth system (merged later) or a local `Users` table now? One id per model, or one AI user plus `Model`?
12. **Confirmation granularity:** per mapping row, per German word, or per verse? The pop-up aggregates per word
    (confirmed only when all rows are).
13. **Versioning model:** "diff-based, versioned with timestamps" (CLAUDE.md) – in-place columns plus a history
    table `Elb1871MappingHistory`, or append-only rows with `ValidFrom`/`ValidTo`? Decides S29 and whether the
    pop-up can show "Version 3".
14. Should Gemini be asked for a **confidence** or alternative candidates in the prompt (costs tokens), or are
    derived hints enough?
15. **`GermanWordPart` noise:** clean the 43,414 whole-word values and the 7 `*` values in the rolling seed once, or
    only filter them in the API?
16. Should the public reader show the model name, or only the badge and date (model only in the tagging UI)?
17. Should unconfirmed AI mappings be shown at all in the public reader (current decision: yes, badged)?
