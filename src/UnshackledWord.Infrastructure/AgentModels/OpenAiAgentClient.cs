using System.Text.Json;
using System.Text.Json.Nodes;
using UnshackledWord.Application.Features.AgentMapping;

namespace UnshackledWord.Infrastructure.AgentModels;

/// <summary>
/// OpenAI-compatible chat completions (<c>POST {BaseUrl}/chat/completions</c>, <c>Authorization: Bearer</c>): OpenAI, OpenRouter, vLLM, llama.cpp,
/// LM Studio, Ollama's <c>/v1</c>. The prefix is the system message (OpenAI caches an identical prefix of 1024+ tokens automatically; local servers reuse
/// their KV cache). The schema goes into <c>response_format: json_schema</c> (non-strict); <see cref="AgentSchemaModes.Json"/> sends
/// <c>json_object</c>. Usage: <c>prompt_tokens</c> includes <c>prompt_tokens_details.cached_tokens</c>.
/// </summary>
public sealed class OpenAiAgentClient : AgentHttpAdapter
{
    public OpenAiAgentClient(HttpClient http, AgentProfile profile, AgentRetryPolicy? retry = null) : base(http, profile, retry)
    {
    }

    protected override string Url(AgentModelRequest request) => $"{BaseUrl(Profile)}/chat/completions";

    protected override void Authorize(HttpRequestMessage message)
    {
        if (Profile.HasApiKey)
        {
            message.Headers.TryAddWithoutValidation("Authorization", $"Bearer {Profile.ApiKey}");
        }
    }

    protected override JsonObject Body(AgentModelRequest request)
    {
        var messages = new JsonArray(new JsonObject { ["role"] = "system", ["content"] = request.StaticPrefix });
        AddUserAndTurns(messages, request, "assistant");

        // OpenAI's newer models reject max_tokens; every other server knows only max_tokens.
        var limitName = Uri.TryCreate(Profile.BaseUrl, UriKind.Absolute, out var uri) && uri.Host == "api.openai.com" ? "max_completion_tokens" : "max_tokens";
        var body = new JsonObject
        {
            ["model"] = request.Model,
            ["messages"] = messages,
            ["temperature"] = 0,
            [limitName] = request.MaxOutputTokens,
        };

        switch (Profile.SchemaMode)
        {
            case AgentSchemaModes.Schema:
                body["response_format"] = new JsonObject
                {
                    ["type"] = "json_schema",
                    ["json_schema"] = new JsonObject { ["name"] = "agent_answer", ["strict"] = false, ["schema"] = SchemaNode(request.JsonSchema, closeObjects: false) },
                };
                break;
            case AgentSchemaModes.Json:
                body["response_format"] = new JsonObject { ["type"] = "json_object" };
                break;
        }

        return body;
    }

    protected override AgentModelResult Read(JsonElement response, AgentModelRequest request)
    {
        var usage = TryGet(response, "usage", out var u) ? u : default;
        var input = Long(usage, "prompt_tokens");
        var cached = TryGet(usage, "prompt_tokens_details", out var details) ? Long(details, "cached_tokens") : 0;
        var output = Long(usage, "completion_tokens");

        string? text = null;
        var finish = string.Empty;
        if (TryGet(response, "choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
        {
            var first = choices[0];
            finish = String(first, "finish_reason") ?? string.Empty;
            text = TryGet(first, "message", out var message) ? String(message, "content") : null;
        }

        var json = RequireJson(text, finish is "length" or "content_filter", finish, input, cached, output);

        return new AgentModelResult
        {
            Json = json,
            InputTokens = input,
            CachedInputTokens = cached,
            OutputTokens = output,
            Model = String(response, "model") ?? request.Model,
            ProviderRequestId = String(response, "id"),
            FinishReason = finish,
        };
    }
}
