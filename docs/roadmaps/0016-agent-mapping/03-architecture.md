# 0016 · Architecture

## One core, two doors

```
                 ┌──────────────── Application/Features/AgentMapping ────────────────┐
 Batch worker ─► │ PackageBuilder ─► DeterministicPass ─► (model) ─► ProposalValidator│ ─► Proposals
 (server side)   │        ▲                                   ▲            │          │
 MCP agent ────► │  get_mapping_package              submit_mapping_proposal ┘          │
 (client side)   └─────────────────────────────────────────────────────────────────────┘
                                                                     Maintainer review ─► live tables
```

- **Batch worker** (`Tooling.AgentWorker`, replaces `Tooling.AiWorker`): started by a maintainer job (`api/maintainer/agent-runs`, same background-job pattern as the seed jobs) or from the CLI. It owns the model call, so it can use caching and batch APIs. Cheapest path for whole books.
- **MCP agent**: an external agent (Claude Code, Codex, Gemini CLI, an open-source agent with MCP support) calls tools on `Tooling.McpServer`. The agent pays its own tokens; the server only builds packages and validates. Good for "redo these 5 verses" and for trying models without a new adapter.

Both doors reach the same three services. Neither door writes live data.

## Services (Application, no provider code)

| Service | Does |
|---|---|
| `AgentPackageBuilder` | Reads German words, lemmas, original words, existing links for a verse range; assigns short local ids; runs the deterministic pass ([04-work-package.md](04-work-package.md)) |
| `AgentProposalValidator` | Maps local ids back, runs the hard and soft rules per verse ([06-validation.md](06-validation.md)) |
| `AgentProposalStore` | Writes valid verses as proposals with run metadata ([08-proposals-and-review.md](08-proposals-and-review.md)) |

`IAgentModelClient` (Infrastructure) is the only provider-aware part and is used only by the batch worker ([07-providers-and-caching.md](07-providers-and-caching.md)).

## MCP tools (submit needs a `propose` token, see [02-decisions.md](02-decisions.md))

| Tool | Input | Output |
|---|---|---|
| `get_agent_instructions` | `language` (greek/hebrew) | Rules text and answer schema, same text the batch worker caches |
| `get_mapping_package` | book, chapter, verse range (max N verses) | Package with `packageId`, local ids, pre-filled links, open slots |
| `lemma_candidates` | German form, optional verse | Candidates from the lemma lexicon ([05-lemmatisation.md](05-lemmatisation.md)) |
| `submit_mapping_proposal` | `packageId`, answer JSON | Per verse: `accepted` / `rejected` with messages / `flagged`; the agent may resubmit rejected verses |

`packageId` binds a submission to the ids it was given, so an agent cannot invent ids or reuse a stale package after the text changed (package stores a hash of the verse word ids).

## Retry loop

The validator answer is designed for a model to read: one line per problem, with local ids (`v3 g7: source word s12 belongs to v4`). The batch worker resubmits only rejected verses, at most twice, with those messages appended. MCP agents do the same on their own.
