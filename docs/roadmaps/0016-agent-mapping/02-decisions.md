# 0016 · Decisions

## Given by the maintainer (2026-11-15)

- The agent may only link German words to source words **of the same verse**; the server validates every link.
- The agent must lemmatise German correctly (context-aware: separable verbs, homographs).
- Whatever an algorithm can decide, an algorithm decides. The model only gets the remainder.
- A verse range is processed per request so the system prompt is paid once per range, not per verse.
- Must work with Claude, Gemini, OpenAI and open-source models.

## Proposed (need a yes before 0016.1)

1. **Agents propose, never write.** All AI output lands in proposal tables ([08-proposals-and-review.md](08-proposals-and-review.md)); merge is maintainer-only. Follows the Linux-style editing model.
2. **One core, two doors.** The batch worker and the MCP tools call the same package builder and validator in `Application` ([03-architecture.md](03-architecture.md)). No provider-specific logic outside the client adapters.
3. **Provider adapters, no framework.** Three hand-written HTTP adapters: Anthropic Messages, Gemini, OpenAI-compatible Chat Completions (covers OpenAI, OpenRouter, Ollama, vLLM, llama.cpp). Each new adapter is a dependency question; prefer plain `HttpClient` ([07-providers-and-caching.md](07-providers-and-caching.md)).
4. **JSON with JSON Schema replaces the pipe format.** Short local ids per package keep it as cheap as the pipe format.
5. **One AI user per model** (e.g. `ai:gemma4`, `ai:claude-sonnet`), created on first use, role `Ai`, can only create proposals. The provider profile (account + key) is stored on the run, not in the user.
6. `Tooling.AiWorker` is replaced by the new worker; the old Gemini prompts become the first prompt version and are kept as reference until 0016.6 is done.

## Decided (2026-11-15)

- **MCP write access is token-gated.** Read tools stay public; `submit_mapping_proposal` needs an API token with scope `propose`, issued by a maintainer for a user with role `Ai` or `Maintainer`, revocable, with an expiry. Submissions still only create proposals. Tasks 0016.8 and 0016.8f are in scope.
- **Superseded 2026-11-17 by [0018](../0018-german-lemmas/02-decisions.md): the particle keeps its own lemma.** Old rule: **Separable verbs: both tokens get the full lemma.** Mk 1:29 `gingen … aus` → `ausgehen` on `gingen` and on `aus`. Today 27,354 tokens are tagged `PTKVZ` and carry the particle as lemma (`aus/aus`); they all become candidates for the model ([05-lemmatisation.md](05-lemmatisation.md)).

- **Compound splits are part of the agent format (2026-11-16).** `Gerstenernte` (Ruth 1:22) → `Gersten` + barley, `ernte` + harvest. The editor and the save path learn parts (0016.10, 0016.10f); proposals with parts merge only after that. Existing parts that are not a real substring of their word (`und`, `1`, `null`, the whole word) are cleared once; the ~545 real ones are kept.
- **Budget: estimate and hard cap (2026-11-16).** The start dialog shows estimated tokens and cost, the maintainer confirms; the provider profile has `MaxTokensPerRun` and `MaxCostPerRun`, the worker stops before the package that would exceed either, and the run is resumable ([07-providers-and-caching.md](07-providers-and-caching.md)).

- **Seed links are hints (2026-11-16).** In `redo` runs the existing seed/AI links of a verse are offered as `hint`s, so the model mostly confirms instead of deciding from scratch; only human-authored links can be `fixed`. Reason: without it Mk 1:1–8 was 100 % `open` ([04-work-package.md](04-work-package.md)).

- **Frontend (2026-11-16).** New components `AgentRunDialog`, `StatusBadge`, `ProgressBar`, `WordSplitter`; compare/edit-merge and parts are props of `MappingEditor` (`mode`, `compareLinks`, `initialLinks`, `footer`, `saveLabel`, `partsEditable`, `onAiRedo`, `onSave(links, { clearParts })`) and `MappingToken` (`part`). UX: split by clicking letter gaps, any number of parts; review keys j/k, m/e/d, discard asks for confirmation, edit-merge may edit lemmas; bulk merge = checkbox + count, unflagged includes lemma changes and parts; runs start from the verse button and the chapter header, the dialog shows progress and can go to background; tokens are issued for `ai:<model>` users (needs a user list endpoint), 30 days default, 365 max, dialog shows an MCP config snippet. Backend additions: `openProposals` on runs, `GET api/maintainer/api-tokens/users`.

No open decisions left.

## Findings the plan rests on

- `SaveVerseMappingValidator` (Application) already checks verse membership, self-parent and added-word rules: it becomes the hard core of [06-validation.md](06-validation.md).
- `LemmaSttsGuard` already checks STTS ↔ part of speech for lemma edits.
- `IntShrinkDictionary` in the old worker already shortens ids per request.
- The MCP server (`Tooling.McpServer`) runs stateless HTTP and reuses repositories; it has no auth today.
- `RevisionRepository.ReplaceMappingAsync` stores `CreatedByUserId`; the Elb1871 mapping tables do not.

## Example verses (confirmed 2026-11-17)

Proposed by the 0016.1 agent, **confirmed by the maintainer on 2026-11-17**. They are the worked examples in `assets/agent-prompts/greek-v1.md` and `hebrew-v1.md`; the expected answers were derived from the current seed mapping and corrected by the agent (every answer passes the 0016.4 validator on the DB copy). Confirm or replace them before 0016.9; a change means a new prompt version.

| Language | Verse | What it teaches |
|---|---|---|
| Greek | Mk 1:1 | articles that translate a Greek article are links, not added words; `Sohnes` retagged `NE` → `NN` |
| Greek | Mk 1:6 | untranslated `ὁ` joins its name, untranslated `Καὶ` stays unlinked, `δέ` = `aber`, `er aß` on one verb, added article `einem`, lemma fixes |
| Greek | Mk 1:8 | `μέν`/`δέ` = `zwar`/`aber`, auxiliary + participle on one verb (`habe … getauft`), `Heiligem` → `heilig` |
| Greek | Mk 1:29 | `aus der Synagoge`: `aus` is wrongly `PTKVZ` (preposition, not a separable particle); pronoun `sie` on the verb |
| Greek | Mk 1:35 | separable verbs `stand … auf`, `ging … hinaus`, `ging hin` (full lemma on both tokens), added `als`/`es`/`war`/`einen`, `Frühmorgens` retagged |
| Hebrew | Gen 1:1 | untranslated object marker `אֵת` joins the noun it marks, article inside the word |
| Hebrew | Gen 1:3 | `es werde` / `es ward` on one Hebrew verb, waw-consecutive |
| Hebrew | Rt 1:4 | open tokens, German genitive articles as added words, nominal clause `war` |
| Hebrew | Rt 1:5 | separable verb `blieb … übrig`, suffix pronouns (`ihren Söhnen`), `Söhn` → `Sohn` |
| Hebrew | Rt 1:22 | compound `Gerstenernte` = `Gersten` + `ernte` (parts), separable `kehrte … zurück`, `aus` retagged, `welche` retagged |

The answers are written as diffs against the hints (silence keeps a hint, see the prompt): that is how the worker prompt asks models to answer.
