using System.Text.Json;
using System.Text.Json.Nodes;
using UnshackledWord.Application.Features.AgentMapping;

namespace UnshackledWord.Infrastructure.AgentModels;

/// <summary>
/// Anthropic Messages API (<c>POST /v1/messages</c>). The static prefix is the system block with a <c>cache_control</c> breakpoint, the answer schema goes
/// into <c>output_config.format</c> (JSON outputs). Usage: <c>input_tokens</c> excludes cache reads and writes, which this adapter adds back.
/// </summary>
public sealed class AnthropicAgentClient : AgentHttpAdapter
{
    public const string ApiVersion = "2023-06-01";

    public AnthropicAgentClient(HttpClient http, AgentProfile profile, AgentRetryPolicy? retry = null) : base(http, profile, retry)
    {
    }

    protected override string Url(AgentModelRequest request) => $"{BaseUrl(Profile)}/v1/messages";

    protected override void Authorize(HttpRequestMessage message)
    {
        message.Headers.Add("x-api-key", Profile.ApiKey);
        message.Headers.Add("anthropic-version", ApiVersion);
    }

    protected override JsonObject Body(AgentModelRequest request)
    {
        var messages = new JsonArray();
        AddUserAndTurns(messages, request, "assistant");

        var body = new JsonObject
        {
            ["model"] = request.Model,
            ["max_tokens"] = request.MaxOutputTokens,
            ["temperature"] = 0,
            ["system"] = new JsonArray(new JsonObject
            {
                ["type"] = "text",
                ["text"] = request.StaticPrefix,
                ["cache_control"] = new JsonObject { ["type"] = "ephemeral" },
            }),
            ["messages"] = messages,
        };

        if (Profile.SchemaMode == AgentSchemaModes.Schema)
        {
            body["output_config"] = new JsonObject
            {
                ["format"] = new JsonObject { ["type"] = "json_schema", ["schema"] = SchemaNode(request.JsonSchema, closeObjects: true) },
            };
        }

        return body;
    }

    protected override AgentModelResult Read(JsonElement response, AgentModelRequest request)
    {
        var usage = TryGet(response, "usage", out var u) ? u : default;
        var cacheRead = Long(usage, "cache_read_input_tokens");
        var input = Long(usage, "input_tokens") + cacheRead + Long(usage, "cache_creation_input_tokens");
        var output = Long(usage, "output_tokens");
        var stop = String(response, "stop_reason") ?? string.Empty;

        var text = new System.Text.StringBuilder();
        if (TryGet(response, "content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in content.EnumerateArray().Where(b => String(b, "type") == "text"))
            {
                text.Append(String(block, "text"));
            }
        }

        var json = RequireJson(text.ToString(), stop is "max_tokens" or "refusal", stop, input, cacheRead, output);

        return new AgentModelResult
        {
            Json = json,
            InputTokens = input,
            CachedInputTokens = cacheRead,
            OutputTokens = output,
            Model = String(response, "model") ?? request.Model,
            ProviderRequestId = String(response, "id"),
            FinishReason = stop,
        };
    }
}
