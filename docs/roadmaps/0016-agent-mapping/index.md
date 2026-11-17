# Roadmap 0016: AI agents map and lemmatise verse ranges

Folder roadmap (see the `roadmap-folder` skill): open only the file you need.

| File | Content |
|---|---|
| [01-goal.md](01-goal.md) | What is missing today (with dev DB numbers), end state, out of scope |
| [02-decisions.md](02-decisions.md) | Proposed and open decisions, code findings the plan rests on |
| [03-architecture.md](03-architecture.md) | One server core, two entry points (batch worker, MCP agent), data flow |
| [04-work-package.md](04-work-package.md) | Verse-range package the model reads, deterministic pre-pass, answer format |
| [05-lemmatisation.md](05-lemmatisation.md) | Lemma candidates, what the model decides, lemma checks |
| [06-validation.md](06-validation.md) | Hard rules (reject) and soft rules (flag) for mappings and lemmas |
| [07-providers-and-caching.md](07-providers-and-caching.md) | Provider-neutral client, structured output, prompt caching, batch APIs |
| [08-proposals-and-review.md](08-proposals-and-review.md) | Proposal tables, AI user, review and merge UI |
| [09-tasks.md](09-tasks.md) | Task table (backend and frontend woven) |
| [10-risks.md](10-risks.md) | Risks worth naming before starting |

## Status (2026-11-16)

**Implemented (uncommitted):** 0016.2 and 0016.10 — migrations 0025 (agent tables, mapping `CreatedByUserId`), 0026 (`PartOrder`), 0027 (junk-part cleanup: Hebrew 47,599 → 545, Greek 75 → 0, seed verse hashes moved for unedited verses); author and parts through the live save path, `clearParts` guard. Verified on DB copy `uw_0016_test` and a live WebApi run; tests green. Deviations: the mapping seed format is unchanged (part text only, order derived on import, junk parts dropped, no author); the revision save rejects parts; legacy parts on added-word rows force `clearParts` on the first editor save of such a verse; old `AiMappingImport`/`CopyBackMappingRepository` still write parts without order (read path derives it).

**Implemented (uncommitted):** 0016.3 and 0016.4 — `AgentPackageBuilder`, deterministic pass, lemma lexicon, `AgentProposalValidator`/`Store`/`Service` in Application; `GET api/maintainer/agent/package`, `POST api/maintainer/agent/proposal`. STTS rules moved from WebApi to Application/Infrastructure for reuse. Verified live on `uw_0016_test` (cross-verse rejection message, proposals stored and superseded, stale 409, expired 410); 75 new tests. Finding: with no confirmed (human-authored) links the deterministic pass is nearly silent — Mk 1:1–8 redo 0 % fixed / 0 % hint / 100 % open (5,488 est. tokens, 1 package); Ruth 1:1–8 2.6 / 0.9 / 96.5 %. Additions: answer `unmapped`, optional part of speech on lemmas, `notAnswered` in the submit response; separable check is verse-scoped; debug runs use `Profile='manual'`.

**Implemented (uncommitted):** 0016.1 (agent part: 10 example verses, unconfirmed, see [02-decisions.md](02-decisions.md)), 0016.5, 0016.6 backend. Deviations:
- The run loop is a hosted background job in the WebApi (`AgentRunManager`, `api/maintainer/agent-runs`: estimate, start, status, list, cancel, resume, profiles) plus a thin CLI `Tooling.AgentWorker`; `Tooling.AiWorker` is out of the `.slnx` (folder kept). Shared loop `AgentRunExecutor` in Application.
- `IAgentModelClient` lives in Application (fakeable); four adapters (anthropic, gemini, openai-compatible, native ollama) in `Infrastructure/AgentModels`. Profiles gained `SchemaMode` (schema/json/none) and `MaxParallel`.
- Ollama Cloud documents no structured-output support; `gemma4:31b` is the only Gemma tag; no per-token prices published (profile prices 0).
- Prompts `assets/agent-prompts/greek-v1.md`, `hebrew-v1.md`; answers are diffs against hints ("silence keeps a hint").
- Migration 0028 (package cost and call count). Batch-API mode deferred.
- Seed links as hints: Mk 1:1–8 redo 0 % fixed / 100 % hint / 0 % open; Ruth 1:1–22 redo 0 / 95.5 / 4.5 %.
- Run loop verified with a fake Ollama server (budget stop + resume, cancel, retry). `compose.yml` now passes the key name used in `.env`.
- **Live Gemma 4 (`gemma4:31b`, Ollama Cloud) check done:** Ollama Cloud accepts `format` (schema mode), no schema failures. Mk 1:1–8 redo: 3 calls, 38k input (14k cached) / 2k output tokens, all 8 verses proposed after one resubmission; Ruth 1:1–4: 42k / 18k cached / 3k. The model corrects seed links (e.g. Mk 1:1 `des` → τοῦ, Ruth 1:1 broken added words fixed) but also drops some correct ones (Mk 1:4 `und` → καὶ) — review stays mandatory. Estimate (25k) was ~35 % low; replace the factors with measured values.

**Implemented (uncommitted):** 0016.7 and 0016.8 backend. Review endpoints under `api/maintainer/agent-proposals` and `…/agent-runs/{id}/proposals` (list, diff, merge, edit-merge, discard, bulk merge with confirm); plain merge: author = AI user, reviewer = maintainer (`AgentVerseProposals.ReviewedByUserId`, new `Elb1871MorphologyEdits.ReviewedByUserId`); edit-merge: maintainer is author; parts merge. Migration 0029: `ApiTokens` (`uwt_` tokens, SHA-256 hash only, 30-day default expiry), `AgentRuns.ApiTokenId`. Maintainer endpoints `api/maintainer/api-tokens` (create once-shown, list, revoke). MCP tools `get_agent_instructions`, `get_mapping_package` (without token: non-submittable preview), `lemma_candidates`, `submit_mapping_proposal` (token, package bound to token, daily cap `AgentMapping:McpMaxProposalsPerTokenPerDay`); no rate limiter on the MCP server (0015 out of scope). All verified live via curl JSON-RPC on `uw_0016_test`. Not verified live: edit-merge with lemmas, editing-disabled 403, daily cap, Claude Code as client. Frontend part 2 (uncommitted): 0016.7f ("KI-Vorschläge" page `edit/proposals/:runId?/:proposalId?`, `MappingEditor` compare mode, edit-merge with lemma editing, keys j/k ←/→ m/e/d, bulk merge dialog), 0016.10f part B (`WordSplitter`, "Wort teilen"). Checked live on `uw_0016_test`: 10 verses of Mk 1:17–23 reviewed by keyboard (8 merged, 1 edited, 1 discarded), `Weizenernte` (Gen 30:14) split, saved, reloaded. Not live: bulk merge, mobile stepper, part hover in the public view, an AI proposal with parts. Deviations: proposal list filtered client-side; plain merge disabled when a lemma changed live. Totals 2026-11-16: `bun run check` 667 tests, .NET Application 623 / Domain 292 / Infrastructure 122 (+6 skipped) / WebApi 133 / McpServer 52 / SeedDb 19, build 0 errors. CLAUDE.md updated (access, architecture). **Open: 0016.9** (needs confirmed example verses and 50 human-reviewed reference verses), README section.

Frontend part 1 (uncommitted): 0016.6f (`AgentRunDialog`, "KI-Läufe" section), 0016.8f ("KI-Zugriffstoken", token for `ai:<model>` users, `GET api/maintainer/api-tokens/users`), 0016.10f part A (parts round-trip, unsplit, `clearParts` note), `StatusBadge`, `ProgressBar` (native `<progress>`), `openProposals` on runs; `bun run check` 602 tests. Run progress UI tested only (no live model run from the UI). Pre-existing: McpServer fails to start in Development (`IStatsRefreshStatus` not registered); the migration runner ignores the secrets file.

Evaluation only, nothing implemented. All decisions taken ([02-decisions.md](02-decisions.md)): token-gated MCP writes, full lemma on both separable-verb tokens, compound parts in the agent format with editor support (0016.10/0016.10f) and a one-time cleanup of junk parts, budget estimate plus hard cap. Next: 0016.1 (example verses).

Implementation postponed (2026-11-16). Agreed for when it starts: Sonnet agents (high effort) work one slice after another in the main checkout and **do not commit**; the maintainer reviews and commits. For 0016.1 an agent proposes the example verses, marked unconfirmed, and the maintainer confirms before 0016.9. Live provider checks use **Gemma 4 on Ollama Cloud** with the maintainer's Ollama API key from the out-of-repo secrets file ([07-providers-and-caching.md](07-providers-and-caching.md)).
