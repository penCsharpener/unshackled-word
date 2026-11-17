# 0016 · Goal

## What is wrong or missing today

- The only AI path is `Tooling.AiWorker`: Gemini only, a hard-coded system prompt per language (`GreekGeminiFlashClient`, `HebrewGeminiFlashClient`), a pipe-delimited answer (`ElbWordId|StepWordId|IsAddedWord|…`) parsed by `string.Split`, and results imported in bulk by `SeedDb/AiMappingImport`. It cannot be pointed at Claude, OpenAI or a local model.
- The mapping forms (`PUT api/verseMapping/…`, `MappingEditor`) and the lemma forms (`PUT api/lemmas/words/{id}`, `PUT api/lemmas/chapter`) are reachable only by a maintainer cookie. An agent has no login, and a direct write would skip human review.
- Mapping rows carry no author or version: `Elb1871GreekMapping` / `Elb1871HebrewMapping` have only `CreatedAt`. "AI mappings under their own user id with versioning" (CLAUDE.md) is not possible today.
- Lemmas: 721,552 `Elb1871Morphology` rows for 722,753 German words, produced by LemmaSharp in `SeedDb/Elb1871Lemmatizer`, with 789 human edits in `Elb1871MorphologyEdits`. LemmaSharp has no sentence context (separable verbs, `sein` vs. `seine`, homographs).
- Dev DB (`uw_0014_live`): 170,071 Greek and 556,418 Hebrew mapping rows (32,995 / 59,026 added words); 336 German words have no row at all. Compound splits (`GermanWordPart`) exist in 75 Greek rows, but the save path writes `NULL` for them.

## End state

- A maintainer selects a verse range (e.g. Mk 1–3) in the tagging UI, picks a provider profile, and starts "KI neu zuordnen". Or an agent (Claude Code, Codex, Gemini CLI, an open-source agent) does the same through MCP tools.
- The server builds a compact package for the range, fills everything it can deterministically, and asks the model only for the rest. One request covers many verses; the static instructions are a cached prefix.
- Every answer goes through the same server validator as human saves. Invalid verses are rejected with exact messages (an agent can retry them); valid verses become **proposals** under the AI user, with model, prompt version and run id.
- The maintainer reviews proposals as a diff per verse and merges, edits or discards them. Nothing an agent produces reaches the live mapping or lemma tables without that step.

## Out of scope

- Revision variants (`RevisionMapping`, roadmap 0011); the same core can serve them later.
- Agents writing German text, Greek/Hebrew data or STEP data.
- Fine-tuning or hosting a model.
- Reviewer hierarchy beyond maintainer merge.
