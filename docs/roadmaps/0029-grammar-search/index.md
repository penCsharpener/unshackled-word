# Roadmap 0029: Grammar search

Folder roadmap (see the `roadmap-folder` skill): open only the file you need.

| File | Content |
|---|---|
| [01-goal.md](01-goal.md) | What is missing (codes, counts), end state, deferred items |
| [02-decisions.md](02-decisions.md) | Maintainer decisions from the interview, plan decisions, code findings |
| [03-query-language.md](03-query-language.md) | Tokens, slots and binding, word reading, keys and values, errors |
| [04-search-model.md](04-search-model.md) | Code layout, `MorphologyCodeIndex`, SQL per slot, endpoints |
| [05-ui.md](05-ui.md) | Grammar mode in the overlay, autocomplete, persistent query and reader highlighting, cheat sheet, Strong's page facets |
| [06-tasks.md](06-tasks.md) | Task table (backend and frontend woven) |
| [07-risks.md](07-risks.md) | Risks worth naming before starting |

## Status (2026-11-26)

Implementation started 2026-11-26 on `feature/morphology-search`. Numbering continues from `feature/lxx` (0.13.18, migrations up to 0053): this roadmap is version 0.14.0 with 0029.1, then a build number per row; migrations `0054_GrammarSearchGreekIndex.sql` (0029.1) and `0055` (Hebrew index, 0029.6). `ExpectedMigration` follows.

**0029.1 (Greek grammar search, backend) done, version 0.14.0, migration 0054.** Measured: `agape Kasus:nom` 36 words / 32 verses (Matt 24:12 first), `Tempus:aor Diathese:pass` 1,706 / 1,451, `Kasus:nom` without lemma 46.6 ms (bitmap scan on `IX_StepGreekWords_Morphology`, no further index needed). Deviations:

- Routes live under the `elberfelder` group like the other searches: `api/elberfelder/search/grammar…` (design files updated).
- `StrongsLexiconIndex`/`SourceFormIndex` are WebApi types, so word reading sits behind `IGrammarWordReader` (Application) with `GrammarWordReader` in the endpoint folder; transliteration must match exactly (the lexicon search is prefix-based).
- DI in the existing `ServiceCollectionExtensions`, not per-slice `EndpointServices.cs`.
- `LxxRefId` → `HebRefId` uses a left join with fallback to the Greek key (as `StrongsStatsRepository`); an inner join lost one verse.
- `Code:` without `*` is an exact match; `books` counts verses; `original.form` is the printed Greek (may carry punctuation).
- `dt.` keys are typed but disabled (`GermanKeysEnabled = false`) until 0029.7.
- Open: two Application test failures in the agent-prompt tests (`AgentPromptProviderTests`, `AgentPromptExampleTests`) are outside this change and not yet investigated.

**0029.2 (chapter matches and neighbours, backend) done, 0.14.1.** `chapter/40/24?q=agape Kasus:nom` → verse 12, position 9, ~30 ms warm; `neighbours` for Matt 24 → no previous, next John 17. Decisions: `total` counts matched original words in the chapter; in a hit verse every candidate word of every slot is marked (the distinct-word rule only decides whether the verse is a hit); `originalWordIds` are `StepGreekWords.Id` as used by `OriginalVerse`. Frontend row 0029.2f open.

**0029.3 (vocabulary endpoint) done, 0.14.2.** `api/elberfelder/search/grammar/vocabulary` is built from `GrammarVocabulary` (no second list); `GrammarVocabularyLabelTests` compares every value label with `de.ts` (exact) and `en.ts` (case-insensitive) and fails naming category and key. Languages include `aramaic`; `dt.` keys are not listed while `germanKeysEnabled` is false; `Code`/`Strong`/`Lemma` are `freeValue`.

**0029.5 (facets endpoint) done, 0.14.3.** `api/elberfelder/search/grammar/facets` counts words per value with one `GROUP BY "Morphology"` per request, folded in C#; the slot's own filter on the key is ignored. `agape Kasus:` → Nominativ 36, Akkusativ 33, Dativ 29, Genitiv 18; no lemma ~50 ms. Empty values (`Kasus:`) are accepted only by this endpoint (`allowEmptyValue`). Counts are words in `StepGreekWords`, without the verse intersection of other slots. `Sprache`/`Code` are not facetable. Frontend row 0029.5f open.

**0029.1f (Grundtext grammar mode) done, 0.14.4.** Verified in the browser: `agape Kasus:nom` → chip "ἀγάπη G26 · Nominativ", "36 Vorkommen in 32 Versen", Matt 24:12 first with "Liebe" marked; `Tempus:p` marks the token and offers the three tenses. Notes: on mobile the Verse-tab hint offers a button, no automatic tab switch; grammar searches are not tracked by `useTrackSearch`; the lemma is shown only when it differs from the printed form. Open: multi-word labels (Futur II, Medium oder Passiv) cannot be typed as a value (tokens split at whitespace) — the parser needs space-less aliases (`FuturII`, `Futur2`, `MediumPassiv`), to do with 0029.3f. Six web tests in `CrossReferenceList.test.tsx` and `ChapterPage.tskProposal.test.tsx` fail without this change as well.
