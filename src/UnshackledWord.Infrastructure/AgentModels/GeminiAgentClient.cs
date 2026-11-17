using System.Text.Json;
using System.Text.Json.Nodes;
using UnshackledWord.Application.Features.AgentMapping;

namespace UnshackledWord.Infrastructure.AgentModels;

/// <summary>
/// Gemini <c>generateContent</c> (<c>POST /v1beta/models/{model}:generateContent</c>, header <c>x-goog-api-key</c>). The prefix is the
/// <c>systemInstruction</c>; the schema goes into <c>generationConfig.responseJsonSchema</c> with <c>responseMimeType</c> application/json.
/// Caching is implicit (the provider reuses an identical prefix); cached tokens come back in <c>usageMetadata.cachedContentTokenCount</c>, which is a
/// subset of <c>promptTokenCount</c>. Thinking tokens (<c>thoughtsTokenCount</c>) are billed as output and added to it.
/// </summary>
public sealed class GeminiAgentClient : AgentHttpAdapter
{
    public GeminiAgentClient(HttpClient http, AgentProfile profile, AgentRetryPolicy? retry = null) : base(http, profile, retry)
    {
    }

    protected override string Url(AgentModelRequest request) => $"{BaseUrl(Profile)}/v1beta/models/{Uri.EscapeDataString(request.Model)}:generateContent";

    protected override void Authorize(HttpRequestMessage message) => message.Headers.Add("x-goog-api-key", Profile.ApiKey);

    protected override JsonObject Body(AgentModelRequest request)
    {
        var contents = new JsonArray(Content("user", request.PackageJson));
        foreach (var turn in request.Turns)
        {
            contents.Add(Content(turn.Role == AgentModelTurn.Assistant ? "model" : "user", turn.Text));
        }

        var config = new JsonObject { ["temperature"] = 0, ["maxOutputTokens"] = request.MaxOutputTokens };
        if (Profile.SchemaMode != AgentSchemaModes.None)
        {
            config["responseMimeType"] = "application/json";
        }

        if (Profile.SchemaMode == AgentSchemaModes.Schema)
        {
            config["responseJsonSchema"] = SchemaNode(request.JsonSchema, closeObjects: false);
        }

        return new JsonObject
        {
            ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray(new JsonObject { ["text"] = request.StaticPrefix }) },
            ["contents"] = contents,
            ["generationConfig"] = config,
        };
    }

    private static JsonObject Content(string role, string text) =>
        new() { ["role"] = role, ["parts"] = new JsonArray(new JsonObject { ["text"] = text }) };

    protected override AgentModelResult Read(JsonElement response, AgentModelRequest request)
    {
        var usage = TryGet(response, "usageMetadata", out var u) ? u : default;
        var input = Long(usage, "promptTokenCount");
        var cached = Long(usage, "cachedContentTokenCount");
        var output = Long(usage, "candidatesTokenCount") + Long(usage, "thoughtsTokenCount");

        var text = new System.Text.StringBuilder();
        var finish = string.Empty;
        if (TryGet(response, "candidates", out var candidates) && candidates.ValueKind == JsonValueKind.Array && candidates.GetArrayLength() > 0)
        {
            var first = candidates[0];
            finish = String(first, "finishReason") ?? string.Empty;
            if (TryGet(first, "content", out var content) && TryGet(content, "parts", out var parts) && parts.ValueKind == JsonValueKind.Array)
            {
                foreach (var part in parts.EnumerateArray().Where(p => !(TryGet(p, "thought", out var t) && t.ValueKind == JsonValueKind.True)))
                {
                    text.Append(String(part, "text"));
                }
            }
        }

        var json = RequireJson(text.ToString(), finish is "MAX_TOKENS" or "SAFETY" or "RECITATION", finish, input, cached, output);

        return new AgentModelResult
        {
            Json = json,
            InputTokens = input,
            CachedInputTokens = cached,
            OutputTokens = output,
            Model = String(response, "modelVersion") ?? request.Model,
            ProviderRequestId = String(response, "responseId"),
            FinishReason = finish,
        };
    }
}
