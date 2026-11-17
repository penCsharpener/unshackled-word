using System.Text.Json;
using System.Text.Json.Nodes;
using UnshackledWord.Application.Features.AgentMapping;

namespace UnshackledWord.Infrastructure.AgentModels;

/// <summary>
/// Ollama's native chat API (<c>POST {BaseUrl}/api/chat</c>, local server or Ollama Cloud at <c>https://ollama.com</c>, <c>Authorization: Bearer</c>).
/// The schema goes into <c>format</c> (a JSON schema object; <see cref="AgentSchemaModes.Json"/> sends <c>"json"</c>), thinking is switched off so
/// reasoning models spend their output on the answer. Usage: <c>prompt_eval_count</c> (prompt tokens), <c>prompt_eval_cached_count</c> (read from the
/// cache) and <c>eval_count</c> (generated). Note: Ollama's docs state that cloud models do not support structured outputs; if a cloud model rejects or
/// ignores <c>format</c> use <c>SchemaMode</c> json (the schema is in the prompt anyway).
/// </summary>
public sealed class OllamaAgentClient : AgentHttpAdapter
{
    public OllamaAgentClient(HttpClient http, AgentProfile profile, AgentRetryPolicy? retry = null) : base(http, profile, retry)
    {
    }

    protected override string Url(AgentModelRequest request) => $"{BaseUrl(Profile)}/api/chat";

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

        var body = new JsonObject
        {
            ["model"] = request.Model,
            ["messages"] = messages,
            ["stream"] = false,
            ["think"] = false,
            ["options"] = new JsonObject { ["temperature"] = 0, ["num_predict"] = request.MaxOutputTokens },
        };

        switch (Profile.SchemaMode)
        {
            case AgentSchemaModes.Schema:
                body["format"] = SchemaNode(request.JsonSchema, closeObjects: false);
                break;
            case AgentSchemaModes.Json:
                body["format"] = "json";
                break;
        }

        return body;
    }

    protected override AgentModelResult Read(JsonElement response, AgentModelRequest request)
    {
        var input = Long(response, "prompt_eval_count");
        var cached = Long(response, "prompt_eval_cached_count");
        var output = Long(response, "eval_count");
        var finish = String(response, "done_reason") ?? string.Empty;
        var text = TryGet(response, "message", out var message) ? String(message, "content") : null;

        var json = RequireJson(text, finish == "length", finish, input, cached, output);

        return new AgentModelResult
        {
            Json = json,
            InputTokens = input,
            CachedInputTokens = cached,
            OutputTokens = output,
            Model = String(response, "model") ?? request.Model,
            ProviderRequestId = null,
            FinishReason = finish,
        };
    }
}
