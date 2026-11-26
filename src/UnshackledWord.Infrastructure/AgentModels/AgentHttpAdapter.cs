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
        var detail = json.Length == 0 ? " (empty answer)" : string.Empty;
        if (!truncated && json.Length > 0)
        {
            try
            {
                using var _ = JsonDocument.Parse(json);

                return json;
            }
            catch (JsonException e)
            {
                if ((LeadingObject(json) ?? RebalancedClosers(json)) is { } repaired)
                {
                    return repaired;
                }

                detail = $" ({e.Message} Length {json.Length}, near: {Excerpt(json, e.BytePositionInLine, e.LineNumber)})";
            }
        }

        throw new AgentModelException(
            AgentModelErrorKind.SchemaFailure,
            truncated ? $"The answer was cut off ({finishReason}) and is not complete JSON." : $"The answer is not valid JSON{detail}.",
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

    /// <summary>
    /// A complete JSON object at the start of an answer that has trailing junk after it (gemma4 once closed a valid answer and added a stray
    /// <c>"</c>); null when the answer does not start with a complete object. Only what follows the object is dropped.
    /// </summary>
    public static string? LeadingObject(string json)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { AllowMultipleValues = true });
        try
        {
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject || !reader.TrySkip())
            {
                return null;
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return System.Text.Encoding.UTF8.GetString(bytes, 0, (int)reader.BytesConsumed);
    }

    /// <summary>
    /// The answer with its final run of <c>]</c>/<c>}</c> rewritten from the bracket stack (gemma4 on Ollama Cloud ended a valid answer with
    /// <c>]]}]]}</c> instead of <c>]]}]}</c>), or null when the text before that run is not a valid unclosed prefix or the result does not parse.
    /// Content is never changed: the validator still flags every word the answer lacks.
    /// </summary>
    public static string? RebalancedClosers(string json)
    {
        var tail = json.Length;
        while (tail > 0 && json[tail - 1] is ']' or '}' or ' ' or '\n' or '\r' or '\t')
        {
            tail--;
        }

        var stack = new Stack<char>();
        var inString = false;
        for (var i = 0; i < tail; i++)
        {
            var c = json[i];
            if (inString)
            {
                if (c == '\\')
                {
                    i++;
                }
                else if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            switch (c)
            {
                case '"': inString = true; break;
                case '{': stack.Push('}'); break;
                case '[': stack.Push(']'); break;
                case '}' or ']':
                    if (stack.Count == 0 || stack.Pop() != c)
                    {
                        return null;
                    }

                    break;
            }
        }

        if (inString || stack.Count == 0)
        {
            return null;
        }

        var repaired = json[..tail] + string.Concat(stack);
        try
        {
            using var _ = JsonDocument.Parse(repaired);

            return repaired;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>About 120 characters of the answer around the parse error (line and byte position from <see cref="JsonException"/>), on one line.</summary>
    private static string Excerpt(string json, long? bytePositionInLine, long? lineNumber)
    {
        var lines = json.Split('\n');
        var line = (int)Math.Clamp(lineNumber ?? 0, 0, lines.Length - 1);
        var lineBytes = System.Text.Encoding.UTF8.GetBytes(lines[line]);
        var inLine = System.Text.Encoding.UTF8.GetCharCount(lineBytes, 0, (int)Math.Min(bytePositionInLine ?? 0, lineBytes.Length));
        var offset = lines.Take(line).Sum(l => l.Length + 1) + inLine;
        var start = Math.Max(0, offset - 80);
        var excerpt = json.Substring(start, Math.Min(json.Length - start, 120)).ReplaceLineEndings(" ");

        return $"'…{excerpt}…'";
    }

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
