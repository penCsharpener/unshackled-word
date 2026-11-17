using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using UnshackledWord.Application.Features.AgentMapping;

namespace UnshackledWord.Infrastructure.Tests.AgentModels;

/// <summary>Returns canned responses in order and records every request (url, headers, JSON body).</summary>
public sealed class FakeHttp : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _responses = new();

    public List<RecordedRequest> Requests { get; } = [];

    public FakeHttp Ok(string json) => Add(HttpStatusCode.OK, json);

    public FakeHttp Add(HttpStatusCode status, string body, Action<HttpResponseMessage>? configure = null)
    {
        _responses.Enqueue(() =>
        {
            var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            configure?.Invoke(response);

            return response;
        });

        return this;
    }

    public HttpClient Client() => new(this);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new RecordedRequest(request.RequestUri!.ToString(), request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase), body is null ? null : JsonNode.Parse(body)!.AsObject()));

        return _responses.Count > 0 ? _responses.Dequeue()() : new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("no more canned responses") };
    }
}

public sealed record RecordedRequest(string Url, Dictionary<string, string> Headers, JsonObject? Body);

public static class AgentModelTestData
{
    public const string Answer = """{"packageId":"p1","verses":[{"v":"v1","links":[["g1","s1"]],"added":[],"lemmas":[],"parts":[],"unmapped":[]}]}""";

    public static AgentProfile Profile(string kind, string baseUrl, string? key = "secret-key", string schemaMode = AgentSchemaModes.Schema) => new()
    {
        Name = "test",
        Kind = kind,
        BaseUrl = baseUrl,
        ApiKey = key,
        Model = "m1",
        SchemaMode = schemaMode,
    };

    public static AgentModelRequest Request(IReadOnlyList<AgentModelTurn>? turns = null) => new()
    {
        Model = "m1",
        StaticPrefix = "PREFIX",
        PackageJson = """{"packageId":"p1"}""",
        JsonSchema = AgentAnswerSchema.Json,
        MaxOutputTokens = 1234,
        Turns = turns ?? [],
    };
}
