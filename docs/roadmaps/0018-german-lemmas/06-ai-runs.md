# 0018 · AI lemma runs (Gemma 4)

Builds on the 0016 pipeline (package builder, validator, proposals, review, cost cap). Nothing here bypasses the proposal step.

## New run mode `lemma`

- Links are **fixed**; the model may only change lemma, STTS, role, Hebrew morpheme, part lemma, and turn a linked subject pronoun into an added word.
- Package per verse: German words with current lemma/STTS, links with current role/morpheme, source words with morphology and the morpheme list (Hebrew), and the lexicon candidates of `LemmaLexicon`.
- Only verses with work are sent: any open `R1`–`R6` check, a `PTKVZ` token, a homograph from the list in [03-lemma-rules.md](03-lemma-rules.md), a form whose lemma is only from LemmaSharp, or a lemma not in the lexicon. Everything else is decided by the deterministic pass and never costs tokens.
- Answer = diff against the package (silence keeps), same as the 0016 prompts: `lemmas`, `roles` (`{link, role, strongs?}`), `added` (pronoun ids), `partLemmas`.

### As built (0018.8)

- Mode `lemma` in `AgentModes` (migration `0031_AgentRunModeLemma.sql` widens the `AgentRuns.Mode` check). Prompt language `lemma-greek`/`lemma-hebrew` (`AgentPromptProvider.LanguageFor`); the files come with 0018.9, until then a lemma run fails at start with "no prompt file". `{{ANSWER_SCHEMA}}` in a `lemma-*` file becomes `AgentAnswerSchema.LemmaJson`.
- Package (`AgentLemmaPass`): every stored link is fixed. Model sees real links in `links` as `[g, s, role, morpheme?, "open"?, partOrder?, part?, partLemma?]`, added words in `fixed`, Hebrew `src` rows get the morpheme list (main in braces), the verse gets `work` (reason codes). `LinkRoleRules.Decide` fills in role/morpheme of links that still have the defaults (head, no morpheme); rule-5 decisions (pronoun → added word) are not applied; undecided links are marked `open`.
- Work filter, as codes: `roles` (an open link, or the sent links break R1–R4), `ptkvz`, `homograph` (`AgentLemmaRules.HomographForms`; the article `das` tagged `ART` is left out, it is in nearly every verse), `lemmasharp` (no human edit and no single trusted lexicon lemma), `lexicon` (empty lemma, 1871 spelling, or the lexicon's trusted lemma differs from the stored one; "not in the lexicon" read this way because every stored lemma is its own lexicon entry). Note: `lemmasharp` sends almost every verse until lemmas are reviewed. Skipped verses count in `Stats.SkippedVerses` and in the run's status note.
- Answer: `lemmas` (as 0016), `roles` `[g, s, role, morpheme?]` with role `head`/`unit`/`multi-head`, `added` `[g, parent]` (only a linked `PPER`), `partLemmas` `[g, partOrder, lemma]`. `links`/`parts`/`unmapped` reject the verse.
- Validator: a human-edited lemma change is a soft flag in lemma mode (hard in redo/fill-open). Flag codes `lemma-human-edited`, `lemma-unknown`, `homograph`, `pronoun-added`, `multi-head`. Invariants via the role-aware `SaveVerseMappingValidator` (one head enforced).
- Proposal: `Links` carry roles (so the merge is a role-aware save), `Lemmas` as before, `Roles` = `AgentRoleChange` list (source `model` or `rules`). A merge is stale when the verse's links changed since the package. The review diff has `roleChanges`.
- Redo/fill-open still ignore `roles`/`partLemmas` in an answer; letting them carry roles is 0018.9 (done, see "As built (0018.9)").
- Review UI (0018.8f): the diff response also carries `CurrentLinks` (the live links with role fields), because `Unchanged`/`Removed` drop roles; the edit-merge editor uses it as its base, so a proposal that only resets roles still saves role-aware. The run DTO has no skipped count: the dialog reads it from the status note (`AgentRunExecutor.SkippedNote`).

## Mapping runs learn roles too

`redo` and `fill-open` (0016) return roles and morphemes with their links from prompt version 2 on, so new mappings arrive with correct roles. The separable-verb instruction is rewritten to the 0018 rule (particle keeps its lemma).

## Prompts

`assets/agent-prompts/greek-v2.md`, `hebrew-v2.md` (mapping, with roles) and `lemma-greek-v1.md`, `lemma-hebrew-v1.md` (lemma mode). The lemma rules of [03-lemma-rules.md](03-lemma-rules.md) and the role table of [04-roles-and-counting.md](04-roles-and-counting.md) are pasted once into a shared block. The 10 confirmed example verses of 0016 get role answers; a change to them is a new prompt version.

### As built (0018.9)

- Prompt files in `assets/agent-prompts`: `lemma-greek-v1.md`, `lemma-hebrew-v1.md`, `greek-v2.md`, `hebrew-v2.md`. `AgentPromptProvider` has no include mechanism, so the shared block (lemma rules of 03 + role table of 04, headings `# Lemma rules (shared block, roadmap 0018)` and `# Link roles (shared block, roadmap 0018)`) is pasted into all four files; `AgentPromptExampleTests` checks it is byte-identical in each. A change to it means a new version of all four. `greek-v1`/`hebrew-v1` are unchanged.
- v2 is the newest file and so the default for new runs (unless `AgentMapping:PromptVersions` pins one). v1 runs are not broken: a run keeps its stored `PromptVersion`, and `AgentPromptProvider.CarriesRoles(version)` (lemma prompts, mapping prompts ≥ v2) decides the format. `AgentBuildRequest.PromptVersion` is set by the run service (plan), the executor (start, resume, split) and the MCP `get_mapping_package` (the version `get_agent_instructions` serves). The debug endpoint `GET maintainer/agent/package` still builds v1 packages.
- v2 package (`AgentPackageMap.Roles = true`): `AgentLemmaPass.WithRoles` adds to the deterministic draft the stored role/morpheme/part lemma/multi-head of each link and fills default links (stored or guessed) by `LinkRoleRules.Decide`; Hebrew `src` rows get the morpheme list. `fixed`/`hints` rows of real links are `[g, s, role, morpheme?]`, added words stay `[g, parent]`.
- v2 answer: `links` `[g, s]`, `[g, s, role]` or `[g, s, role, morpheme]` (no role = head of the main morpheme), `parts` with an optional fifth `partLemma`. The answer schema (`AgentAnswerSchema.Json`) is unchanged (tuples of strings already allow it). Validator: role/morpheme checked like lemma mode (shared `ApplyRole`), part lemma like lemma mode, link set saved role-aware (`SaveVerseMappingValidator` with `EnforceSingleHead`, Hebrew morphemes, Greek Strong's), `multi-head` flag; proposal links carry roles. A role or part lemma in an answer to a v1 package is rejected ("this prompt version has no roles").
- Deviation in v2 linking rule 6 and rule 3: a subject pronoun supplied by the verb ending is an added word (04), not linked with the verb; an untranslated article "prefer no link" (a linked article would count as rendered by the noun). The examples follow that.
- Known gap: v1 prompts still teach "particle carries the full verb lemma", which the 0018.5 validator now rejects; v1 runs of verses with separable verbs get those verses rejected. Use v2.

### Example answers (0018.9) — needs maintainer confirmation

The worked examples of the four prompt files, derived from uw_0018_test after the 0018.4 backfill (fixture `tests/.../AgentMapping/Fixtures/lemma-prompt-examples.json`, test `AgentPromptExampleTests`: package equals the built one, answer passes the validator). Lemma mode can not fix links; where a link is wrong it says so in `note`, the v2 (redo) example fixes it. They count as confirmed only after the maintainer has checked them; a change is a new prompt version.

| Verse | Lemma mode (`lemma-*-v1`): roles and lemmas | Redo (`*-v2`): additionally |
|---|---|---|
| Mk 1:1 | silence (all right) | silence |
| Mk 1:6 | `bekleidet` VVPP, `Kamelhaaren` → `Kamelhaar`, `ledernen` ADJA, `Lenden` → `Lende`, `aß` → `essen` | `Kamelhaaren` as parts `Kamel` (s8) + `haaren`/`Haar` (s7); articles s4, s13 unlinked |
| Mk 1:8 | `habe` → `haben` (not `anhaben`), `euch` → `ihr` ×2; `habe`/`wird` stay `unit` | same |
| Mk 1:29 | `gingen` → `gehen`, `aus` → `APPR`, `kamen` → `kommen`, `Simons` → `Simon` | same |
| Mk 1:35 | `ging` → `hinausgehen` / `hinaus` PTKVZ unit; `ging` → `hingehen` / `hin` PTKVZ, `hin` → `unit`; `an` APPR; `betete` → `beten`; `frühmorgens` ADV; `als` KOUS; `noch` ADV | same, `hin` as link `[g17,s8,unit]` |
| 1Mo 1:1 | silence (`Im` head of H9003) | silence |
| 1Mo 1:3 | `sprach` → `sprechen` | same |
| Rt 1:4 | `bei` → head H9004; `einen` → head H0259; `war` → `unit` H0259 (note: should be added; four `der` unlinked); `nahmen` → `nehmen`, `moabitische` ADJA `moabitisch`, `Weiber` → `Weib` | `der` → H9009 (s6, s9), `einen` → H0259, `bei` → H9004; added `der`×2, `war` (parent `Orpa`) |
| Rt 1:5 | `Da` ADV, head H9001; `allein` → `unit` (note: should be added); `Söhnen` → `Sohn`, `Manne` → `Mann` | `das` → H9009; added `die`, `allein` |
| Rt 1:22 | `so` → `unit`, `welche` → head H9009, `den` → `unit`; `ihr` → `sie` PPER; `aus` APPR; `Moabs` → `Moab`; `kamen` → `kommen`; `Gerstenernte` (note: parts, `nach`/`der` added) | parts `Gersten`/`Gerste` (s16) + `ernte`/`Ernte` (s15); added `so`, `den`, `nach`, `der`; `Bethlehem` on s12 + s13 |

Flags the answers raise on the fixture: `homograph` (Mk 1:35 `als`, Rt 1:5 `Da`, lemma mode only), `many-to-one` (Rt 1:5 s10, redo), `lemma-unknown` (only because the fixture lexicon holds just the forms of these verses).

## Validator additions

Hard (reject the verse): invariants 1–3 of [04-roles-and-counting.md](04-roles-and-counting.md) unless flagged `multi-head`; morpheme not in the source word; role on an added word; lemma rule violations from 03.
Soft (flag, blocks bulk merge): lemma changed where a human edit exists, new lemma not in the lexicon, homograph re-decided, subject pronoun turned into an added word.

## Measuring and rollout

1. The 0016.9 reference set (≈ 50 verses, maintainer-confirmed, without the 10 prompt examples) gets role and morpheme answers.
2. A scorer compares a run with the reference: lemma accuracy, role/morpheme accuracy, flags per verse, tokens and cost per verse. Output on the run page and as JSON.
3. Gemma 4 on Ollama Cloud runs on the reference set. Below the bar in [02-decisions.md](02-decisions.md): change prompt, rerun, compare. Other profiles (Claude, Gemini) can run the same set for comparison.
4. At or above the bar: book-by-book `lemma` runs; "Alle ohne Markierung übernehmen" bulk-merges, flagged verses go to review. Stats refresh after each book.

### As built (0018.10): reference file, freeze, scorer

Code: `Application/Features/AgentMapping/Scoring` (pure `AgentScorer`, `AgentProposalState`, `AgentVerseSnapshots`, `AgentReferenceFreezer`, `AgentScoreService`), `Infrastructure/Repositories/AgentScoreRepository`, migration `0034_AgentRunScore.sql` (`AgentRuns.Score jsonb`, `ScoredAt`). The real reference data does **not** exist yet; the maintainer reviews the verses and freezes them (no real file is committed by the mechanism).

**File** `assets/agent-reference/reference-<version>.json` (`v1`, `v2`, ...; the newest is the default, `AgentMapping:ReferencePath` overrides the folder). Keys are positions, not database ids, so it survives a re-seed:

```json
{ "format": 1, "version": "v1", "frozenAt": "2026-11-17T12:00:00+00:00",
  "verses": [ {
    "book": 41, "chapter": 1, "verse": 17, "language": "greek",
    "text": "Und Jesus sprach ...",
    "words": [ { "pos": 3, "text": "sprach", "lemma": "sprechen", "stts": "VVFIN" } ],
    "links": [ { "g": 3, "sRef": 41001017, "s": 5, "sText": "εἶπεν", "role": "head",
                 "morpheme": "H0559", "part": 1, "partText": "Gersten", "partLemma": "Gerste" } ],
    "added": [ { "g": 4, "parent": 3 } ] } ] }
```

`words` = every German word with letters (`pos` = `PositionInVerse`); `links` = real links (`g` German position, `sRef`/`s` original verse and word position); `morpheme` is set for Hebrew only (the main morpheme spelled out when the link has none); `part`/`partText`/`partLemma` only for compound parts; `added` = added words with their parent. `text`, `sText`, `multiHead` are review aids and not compared. Validation (on freeze and on load): no duplicates, roles `head`/`unit`, positions exist, and **none of the 10 prompt example verses** (Gen 1:1, 1:3, Rt 1:4, 1:5, 1:22, Mk 1:1, 1:6, 1:8, 1:29, 1:35) -- `ReferenceSetFile.PromptExampleVerses`.

**Freeze workflow**

1. Review the verses by hand in the mapping editor and the lemma editor (links, roles, morphemes, part lemmas, lemma + STTS, added words).
2. `POST /api/maintainer/agent-reference/freeze` with `{ "version": "v1", "verses": [ { "bibleBookId": 41, "chapter": 1, "verse": 17 } ] }` (maintainer). 400 for example verses, duplicates, unknown verses. The response carries `reference` (the file content), `fileName` and `warnings` (words without lemma, words without link).
3. Save `reference` as `assets/agent-reference/<fileName>` and commit. A change of the reference is a new version file; old files stay for comparing runs.

**Score** `GET /api/maintainer/agent-runs/{runId}/score?reference=v1` (maintainer; computed on every call and stored in `AgentRuns.Score`; 404 unknown run, 409 no reference file, 422 invalid file). Response `AgentScoreResponse` (`Domain.WebApi/Maintainer/AgentScoreContracts.cs`):

- State of a run = stored state of the verse with the proposal applied, because proposals are diffs ("silence keeps"): `Links` is the full link set (links without role keep the stored role/morpheme/part lemma of the same pair), `Lemmas` and `Roles` list only changes. A reference verse of the run without proposal keeps the stored state. The stored state is read live when scoring, so score a run before merging its proposals (after a merge the stored state already equals the proposal, which is harmless but makes silence look right).
- `lemmas`, `stts`: per reference word with a value. `partLemmas`: per reference part link. `roles`: per reference link (a missing link is wrong). `morphemes`: per Hebrew reference link. `roleMorphemes`: role and morpheme both right (the bar figure). `links`, `addedWords`: precision/recall of real links (key = word, source word, part) and of added words; only informative for `redo`/`fill-open`, a `lemma` run keeps the links.
- `verses`: `scored` (reference verses the run covers: in a package, with a proposal, or inside its range), `notInRun`, `withProposal`, `silent` (inside the range, no package: no work, stored state), `hardRejections` (in a package but no proposal: rejected by the validator after the retries, or not answered; the two are not distinguishable because rejected verses are not stored), `extraRunVerses`.
- `flags`: total, verses flagged, flags per proposed verse, count per code. `cost`: tokens and USD of the packages that hold scored verses, split evenly over the package's verses, plus per-verse averages. `perVerse`: lemma, role and link errors with expected/actual, flags, tokens, cost.
- `bulkMerge`: `meets` is true when the checks `lemmas` (≥ 98 %), `roleMorphemes` (≥ 97 %), `hardRejections` (0) and `scored-verses` (> 0) all hold; exact fractions, not the rounded figures; `reasons` lists the failed ones.
