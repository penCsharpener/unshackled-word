using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using UnshackledWord.Application.Features.AgentMapping;

namespace UnshackledWord.Infrastructure.AgentModels;

/// <summary>Retries of 429/5xx/network errors: at most <see cref="MaxRetries"/> more attempts, waiting <see cref="Delay"/> (a <c>Retry-After</c> header wins, capped at one minute).</summary>
public sealed class AgentRetryPolicy
{
    public static readonly AgentRetryPolicy Default = new();

    /// <summary>No waiting between attempts (tests).</summary>
    public static readonly AgentRetryPolicy Immediate = new() { Delay = _ => TimeSpan.Zero };

    public int MaxRetries { get; init; } = 3;

    /// <summary>Wait before retry number <c>n</c> (1-based): 2 s, 4 s, 8 s by default.</summary>
    public Func<int, TimeSpan> Delay { get; init; } = n => TimeSpan.FromSeconds(Math.Pow(2, n));
}

/// <summary>
/// Shared part of the provider adapters (plain <see cref="HttpClient"/>, no SDK): posts the JSON body, retries 429/5xx with backoff, maps errors to
/// <see cref="AgentModelException"/> and turns a cut-off or non-JSON answer into a <see cref="AgentModelErrorKind.SchemaFailure"/>.
/// The API key goes only into the request headers and is never logged or put into an exception message.
/// </summary>
public abstract class AgentHttpAdapter : IAgentModelClient
{
    private static readonly HashSet<HttpStatusCode> RetryableStatus =
    [
        HttpStatusCode.TooManyRequests,
        HttpStatusCode.InternalServerError,
        HttpStatusCode.BadGateway,
        HttpStatusCode.ServiceUnavailable,
        HttpStatusCode.GatewayTimeout,
        (HttpStatusCode)529,
    ];

    protected AgentHttpAdapter(HttpClient http, AgentProfile profile, AgentRetryPolicy? retry)
    {
        Http = http;
        Profile = profile;
        Retry = retry ?? AgentRetryPolicy.Default;
    }

    protected HttpClient Http { get; }
    protected AgentProfile Profile { get; }
    protected AgentRetryPolicy Retry { get; }

    protected abstract string Url(AgentModelRequest request);

    protected abstract JsonObject Body(AgentModelRequest request);

    protected abstract void Authorize(HttpRequestMessage message);

    protected abstract AgentModelResult Read(JsonElement response, AgentModelRequest request);

    public async Task<AgentModelResult> CompleteAsync(AgentModelRequest request, CancellationToken token)
    {
        var body = Body(request).ToJsonString();
        var url = Url(request);

        for (var attempt = 0; ; attempt++)
        {
            string? failure = null;
            TimeSpan? wait = null;

            try
            {
                using var message = new HttpRequestMessage(HttpMethod.Post, url) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
                Authorize(message);
                using var response = await Http.SendAsync(message, token);
                var text = await response.Content.ReadAsStringAsync(token);

                if (response.IsSuccessStatusCode)
                {
                    return Parse(text, request);
                }

                if (RetryableStatus.Contains(response.StatusCode))
                {
                    failure = $"HTTP {(int)response.StatusCode}: {Snippet(text)}";
                    wait = RetryAfter(response.Headers.RetryAfter);
                }
                else if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    throw new AgentModelException(AgentModelErrorKind.Auth, $"HTTP {(int)response.StatusCode}: {Snippet(text)}");
                }
                else
                {
                    throw new AgentModelException(AgentModelErrorKind.BadRequest, $"HTTP {(int)response.StatusCode}: {Snippet(text)}");
                }
            }
            catch (HttpRequestException e)
            {
                failure = $"network error: {e.Message}";
            }
            catch (TaskCanceledException e) when (!token.IsCancellationRequested)
            {
                failure = $"timeout: {e.Message}";
            }

            if (attempt >= Retry.MaxRetries)
            {
                throw new AgentModelException(AgentModelErrorKind.Transient, $"{failure} (after {attempt + 1} attempts)");
            }

            var delay = wait ?? Retry.Delay(attempt + 1);
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, token);
            }
        }
    }

    private AgentModelResult Parse(string text, AgentModelRequest request)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException e)
        {
            throw new AgentModelException(AgentModelErrorKind.SchemaFailure, $"The provider response is not JSON: {e.Message}");
        }

        using (document)
        {
            return Read(document.RootElement, request);
        }
    }

    /// <summary>Returns the answer text as valid JSON (a markdown fence around it is removed) or throws a schema failure carrying the tokens the call used.</summary>
    protected static string RequireJson(string? text, bool truncated, string finishReason, long input, long cached, long output)
    {
        var json = StripFence(text);
        if (!truncated && json.Length > 0)
        {
            try
            {
                using var _ = JsonDocument.Parse(json);

                return json;
            }
            catch (JsonException)
            {
            }
        }

        throw new AgentModelException(
            AgentModelErrorKind.SchemaFailure,
            truncated ? $"The answer was cut off ({finishReason}) and is not complete JSON." : "The answer is not valid JSON.",
            inputTokens: input,
            cachedInputTokens: cached,
            outputTokens: output);
    }

    /// <summary>The schema without <c>additionalProperties</c> gaps: providers with strict structured output want it on every object.</summary>
    protected static JsonNode SchemaNode(string schema, bool closeObjects)
    {
        var node = JsonNode.Parse(schema)!;
        if (closeObjects)
        {
            Close(node);
        }

        return node;
    }

    private static void Close(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                if (obj["type"]?.GetValue<string>() == "object" && !obj.ContainsKey("additionalProperties"))
                {
                    obj["additionalProperties"] = false;
                }

                foreach (var child in obj.Select(p => p.Value).ToList())
                {
                    Close(child);
                }

                break;
            case JsonArray array:
                foreach (var child in array)
                {
                    Close(child);
                }

                break;
        }
    }

    protected static void AddUserAndTurns(JsonArray messages, AgentModelRequest request, string assistantRole)
    {
        messages.Add(new JsonObject { ["role"] = "user", ["content"] = request.PackageJson });
        foreach (var turn in request.Turns)
        {
            messages.Add(new JsonObject { ["role"] = turn.Role == AgentModelTurn.Assistant ? assistantRole : "user", ["content"] = turn.Text });
        }
    }

    protected static long Long(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0;

    protected static string? String(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    protected static bool TryGet(JsonElement parent, string name, out JsonElement value)
    {
        value = default;

        return parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out value);
    }

    protected static string BaseUrl(AgentProfile profile) => profile.BaseUrl.TrimEnd('/');

    private static string StripFence(string? text)
    {
        var t = (text ?? string.Empty).Trim();
        if (t.StartsWith("```", StringComparison.Ordinal))
        {
            var firstLine = t.IndexOf('\n');
            var end = t.LastIndexOf("```", StringComparison.Ordinal);
            if (firstLine > 0 && end > firstLine)
            {
                t = t[(firstLine + 1)..end].Trim();
            }
        }

        return t;
    }

    private static TimeSpan? RetryAfter(RetryConditionHeaderValue? header)
    {
        if (header?.Delta is { } delta)
        {
            return delta > TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : delta;
        }

        return null;
    }

    private static string Snippet(string text)
    {
        var t = text.Replace('\n', ' ').Replace('\r', ' ').Trim();

        return t.Length <= 300 ? t : t[..300] + "...";
    }
}
