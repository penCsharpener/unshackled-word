# 0016 · Providers and caching

## Client interface (Infrastructure)

```csharp
public interface IAgentModelClient
{
    Task<AgentModelResult> CompleteAsync(AgentModelRequest request, CancellationToken token);
}
// AgentModelRequest: StaticPrefix (instructions + schema + examples), Package JSON, JsonSchema, MaxOutputTokens
// AgentModelResult:  Json, InputTokens, CachedInputTokens, OutputTokens, Model, ProviderRequestId
```

Adapters, all plain `HttpClient`, selected by a provider profile in config (`AgentMapping:Profiles:<name>`: kind, base URL, model, API key from secrets, price per 1M input / cached input / output tokens, `MaxTokensPerRun`, `MaxCostPerRun`):

| Kind | Covers | Structured output | Prefix caching |
|---|---|---|---|
| `anthropic` | Claude | JSON schema output / forced tool call | explicit `cache_control` breakpoint after the static prefix |
| `gemini` | Gemini | `responseSchema` (as today) | implicit caching; explicit cached content for long runs |
| `openai` | OpenAI, OpenRouter, Ollama, vLLM, llama.cpp server, LM Studio | `response_format: json_schema` (local servers: grammar/guided decoding, support varies) | OpenAI automatic prefix caching; local servers reuse the KV cache of an identical prefix |

The exact parameter names and minimum cacheable prefix length per provider are checked against current docs in 0016.5, not taken from this table.

**First live profile (decided 2026-11-16): Gemma 4 on Ollama Cloud.** A profile is one provider account (connection + key), not one model: profile `ollama`, kind `openai` if Ollama Cloud's OpenAI-compatible endpoint supports JSON-schema output, otherwise a small `ollama` kind against its native chat API (`format` = JSON schema). The model is the profile's default `Model` (Gemma 4) and can be overridden per run; prices and caps are per model (`AgentMapping:Profiles:ollama:Models:<tag>`). API key only via `AgentMapping:Profiles:ollama:ApiKey` in the secrets JSON or `UNSHACKLEDWORD_AgentMapping__Profiles__ollama__ApiKey` (set by the maintainer), never in `appsettings.json`. Base URL, exact model tag, structured-output support, prefix caching and pricing are checked against Ollama's docs in 0016.5. Anthropic and Gemini adapters stay in scope but get unit tests against recorded responses until keys exist.

## Prompt layout for caching

Order never changes, so the prefix is byte-identical across all packages of a run:

1. Instructions for the language (rules from the old Gemini prompts, rewritten for the new format), versioned as `prompt-greek-v1` etc.
2. Column definitions and the JSON schema.
3. 3–5 worked examples (real verses, maintainer-confirmed).
4. — cache boundary —
5. The package.

Rules: no date, run id or range in the prefix; the prefix is a file under `assets/agent-prompts/` so its version is visible in git and stored with every proposal. The same text is what `get_agent_instructions` returns to MCP agents, whose own client handles caching.

## Range processing and batch APIs

- A run splits the range into packages ([04-work-package.md](04-work-package.md)) and sends them sequentially first (cache warm-up), then in parallel up to a profile limit.
- For whole books, the profile may use the provider's batch mode (Anthropic Message Batches, OpenAI Batch, Gemini batch): cheaper, results within hours. The run then has status `waiting-for-provider` and is polled by the worker job.
- Token counts and cached-token counts are stored per package so cost per verse can be compared between models.

## Budget

- **Estimate before start**: German and source word count of the range × per-word token factor (measured in 0016.3, refined from finished runs), packages × prefix size as cached input, plus a 30 % retry margin; cost from the profile prices. Shown in the start dialog, the maintainer confirms.
- **Hard cap during the run**: before each package the worker adds the package's estimate to the tokens and cost spent so far; if `MaxTokensPerRun` or `MaxCostPerRun` would be exceeded, the run stops with status `budget-exceeded`. Proposals already stored stay. "Fortsetzen" with a raised cap continues with the next unprocessed package.
- Rough orders of magnitude (to be replaced by measured numbers): Mk 1 ≈ 17k input + 5k output tokens; New Testament ≈ 3.3M input + 0.9M output; Old Testament ≈ 11M input + 3M output, each plus the cached prefix per package.
- MCP agents pay their own tokens; the server only caps proposals per token and day.

## Open-source models

Small local models often break the schema on long packages. The profile has `MaxVersesPerPackage` and the worker shrinks packages after a schema failure. Grammar-constrained decoding (llama.cpp, vLLM) is preferred over "please answer JSON".
