using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Infrastructure.AgentModels;
using static UnshackledWord.Infrastructure.Tests.AgentModels.AgentModelTestData;

namespace UnshackledWord.Infrastructure.Tests.AgentModels;

public class AgentAdapterTests
{
    private static string Escaped(string json) => JsonSerializer.Serialize(json);

    // ---- Anthropic ----------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Anthropic_SendsCachedSystemPrefix_Schema_AndAuthHeaders()
    {
        var http = new FakeHttp().Ok($$$"""{"id":"msg_1","model":"claude-x","stop_reason":"end_turn","content":[{"type":"text","text":{{{Escaped(Answer)}}} }],"usage":{"input_tokens":20,"cache_read_input_tokens":1000,"cache_creation_input_tokens":0,"output_tokens":55}}""");
        var client = new AnthropicAgentClient(http.Client(), Profile("anthropic", "https://api.anthropic.com"), AgentRetryPolicy.Immediate);

        var result = await client.CompleteAsync(Request([new AgentModelTurn("assistant", "A1"), new AgentModelTurn("user", "U1")]), CancellationToken.None);

        var req = http.Requests.Single();
        req.Url.ShouldBe("https://api.anthropic.com/v1/messages");
        req.Headers["x-api-key"].ShouldBe("secret-key");
        req.Headers["anthropic-version"].ShouldBe("2023-06-01");
        var body = req.Body!;
        body["model"]!.GetValue<string>().ShouldBe("m1");
        body["max_tokens"]!.GetValue<int>().ShouldBe(1234);
        body["system"]![0]!["text"]!.GetValue<string>().ShouldBe("PREFIX");
        body["system"]![0]!["cache_control"]!["type"]!.GetValue<string>().ShouldBe("ephemeral");
        body["output_config"]!["format"]!["type"]!.GetValue<string>().ShouldBe("json_schema");
        body["output_config"]!["format"]!["schema"]!["additionalProperties"]!.GetValue<bool>().ShouldBeFalse("providers with strict structured output want closed objects");
        var messages = body["messages"]!.AsArray();
        messages.Count.ShouldBe(3);
        messages[0]!["role"]!.GetValue<string>().ShouldBe("user");
        messages[1]!["role"]!.GetValue<string>().ShouldBe("assistant");
        messages[2]!["content"]!.GetValue<string>().ShouldBe("U1");

        result.Json.ShouldBe(Answer);
        result.InputTokens.ShouldBe(1020, "input_tokens excludes cache reads: they are added back");
        result.CachedInputTokens.ShouldBe(1000);
        result.OutputTokens.ShouldBe(55);
        result.ProviderRequestId.ShouldBe("msg_1");
    }

    [Fact]
    public async Task Anthropic_MaxTokensStop_IsASchemaFailureWithTokens()
    {
        var http = new FakeHttp().Ok("""{"id":"m","stop_reason":"max_tokens","content":[{"type":"text","text":"{\"packageId\":\"p1\",\"verses\":[{\"v\":"}],"usage":{"input_tokens":10,"output_tokens":99}}""");
        var client = new AnthropicAgentClient(http.Client(), Profile("anthropic", "https://api.anthropic.com"), AgentRetryPolicy.Immediate);

        var error = await Should.ThrowAsync<AgentModelException>(() => client.CompleteAsync(Request(), CancellationToken.None));

        error.Kind.ShouldBe(AgentModelErrorKind.SchemaFailure);
        error.OutputTokens.ShouldBe(99, "a cut-off answer is billed and must be counted");
    }

    // ---- Gemini -------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Gemini_SendsSystemInstruction_ResponseJsonSchema_AndReadsCachedAndThoughtTokens()
    {
        var http = new FakeHttp().Ok($$$"""{"candidates":[{"content":{"parts":[{"text":"thinking...","thought":true},{"text":{{{Escaped(Answer)}}} }]},"finishReason":"STOP"}],"usageMetadata":{"promptTokenCount":3000,"cachedContentTokenCount":2500,"candidatesTokenCount":70,"thoughtsTokenCount":30},"modelVersion":"gemini-x","responseId":"r1"}""");
        var client = new GeminiAgentClient(http.Client(), Profile("gemini", "https://generativelanguage.googleapis.com"), AgentRetryPolicy.Immediate);

        var result = await client.CompleteAsync(Request([new AgentModelTurn("assistant", "A1"), new AgentModelTurn("user", "U1")]), CancellationToken.None);

        var req = http.Requests.Single();
        req.Url.ShouldBe("https://generativelanguage.googleapis.com/v1beta/models/m1:generateContent");
        req.Headers["x-goog-api-key"].ShouldBe("secret-key");
        var body = req.Body!;
        body["systemInstruction"]!["parts"]![0]!["text"]!.GetValue<string>().ShouldBe("PREFIX");
        body["generationConfig"]!["responseMimeType"]!.GetValue<string>().ShouldBe("application/json");
        body["generationConfig"]!["responseJsonSchema"]!["type"]!.GetValue<string>().ShouldBe("object");
        body["generationConfig"]!["maxOutputTokens"]!.GetValue<int>().ShouldBe(1234);
        var contents = body["contents"]!.AsArray();
        contents.Count.ShouldBe(3);
        contents[1]!["role"]!.GetValue<string>().ShouldBe("model");

        result.Json.ShouldBe(Answer, "the thought part is not part of the answer");
        result.InputTokens.ShouldBe(3000);
        result.CachedInputTokens.ShouldBe(2500);
        result.OutputTokens.ShouldBe(100, "thinking tokens are billed as output");
        result.ProviderRequestId.ShouldBe("r1");
    }

    [Fact]
    public async Task Gemini_SchemaModeJson_SendsMimeTypeWithoutSchema()
    {
        var http = new FakeHttp().Ok($$$"""{"candidates":[{"content":{"parts":[{"text":{{{Escaped(Answer)}}} }]},"finishReason":"STOP"}],"usageMetadata":{"promptTokenCount":1,"candidatesTokenCount":1}}""");
        var client = new GeminiAgentClient(http.Client(), Profile("gemini", "https://g.example", schemaMode: AgentSchemaModes.Json), AgentRetryPolicy.Immediate);

        await client.CompleteAsync(Request(), CancellationToken.None);

        var config = http.Requests.Single().Body!["generationConfig"]!.AsObject();
        config.ContainsKey("responseMimeType").ShouldBeTrue();
        config.ContainsKey("responseJsonSchema").ShouldBeFalse();
    }

    // ---- OpenAI-compatible --------------------------------------------------------------------------------------------------

    [Fact]
    public async Task OpenAi_SendsChatCompletionsWithJsonSchema_AndReadsCachedTokens()
    {
        var http = new FakeHttp().Ok($$$"""{"id":"chatcmpl-1","model":"gpt-x","choices":[{"message":{"role":"assistant","content":{{{Escaped(Answer)}}} },"finish_reason":"stop"}],"usage":{"prompt_tokens":2048,"completion_tokens":80,"prompt_tokens_details":{"cached_tokens":1920} } }""");
        var client = new OpenAiAgentClient(http.Client(), Profile("openai", "https://api.openai.com/v1"), AgentRetryPolicy.Immediate);

        var result = await client.CompleteAsync(Request(), CancellationToken.None);

        var req = http.Requests.Single();
        req.Url.ShouldBe("https://api.openai.com/v1/chat/completions");
        req.Headers["Authorization"].ShouldBe("Bearer secret-key");
        var body = req.Body!;
        body["messages"]![0]!["role"]!.GetValue<string>().ShouldBe("system");
        body["messages"]![0]!["content"]!.GetValue<string>().ShouldBe("PREFIX");
        body["messages"]![1]!["role"]!.GetValue<string>().ShouldBe("user");
        body["response_format"]!["type"]!.GetValue<string>().ShouldBe("json_schema");
        body["response_format"]!["json_schema"]!["schema"]!["type"]!.GetValue<string>().ShouldBe("object");
        body["max_completion_tokens"]!.GetValue<int>().ShouldBe(1234);

        result.InputTokens.ShouldBe(2048);
        result.CachedInputTokens.ShouldBe(1920);
        result.OutputTokens.ShouldBe(80);
        result.ProviderRequestId.ShouldBe("chatcmpl-1");
    }

    [Fact]
    public async Task OpenAi_LocalServerWithoutKey_SendsNoAuthorizationAndMaxTokens()
    {
        var http = new FakeHttp().Ok($$$"""{"choices":[{"message":{"content":{{{Escaped(Answer)}}} },"finish_reason":"stop"}],"usage":{"prompt_tokens":5,"completion_tokens":6}}""");
        var profile = Profile("openai", "http://localhost:8080/v1", key: null);
        var client = new OpenAiAgentClient(http.Client(), profile, AgentRetryPolicy.Immediate);

        var result = await client.CompleteAsync(Request(), CancellationToken.None);

        profile.IsUsable.ShouldBeTrue("a local server needs no key");
        http.Requests.Single().Headers.ContainsKey("Authorization").ShouldBeFalse();
        http.Requests.Single().Body!["max_tokens"]!.GetValue<int>().ShouldBe(1234);
        result.CachedInputTokens.ShouldBe(0);
    }

    [Fact]
    public async Task OpenAi_LengthFinish_IsASchemaFailure()
    {
        var http = new FakeHttp().Ok("""{"choices":[{"message":{"content":"{\"packageId\":"},"finish_reason":"length"}],"usage":{"prompt_tokens":5,"completion_tokens":6}}""");
        var client = new OpenAiAgentClient(http.Client(), Profile("openai", "https://x.example/v1"), AgentRetryPolicy.Immediate);

        var error = await Should.ThrowAsync<AgentModelException>(() => client.CompleteAsync(Request(), CancellationToken.None));

        error.Kind.ShouldBe(AgentModelErrorKind.SchemaFailure);
        error.InputTokens.ShouldBe(5);
        error.OutputTokens.ShouldBe(6);
    }

    /// <summary>The first paid LXX run over Ruth (0.13.18): gemma4 closed a valid answer and added a stray quote; the object is kept.</summary>
    [Fact]
    public async Task Ollama_TrailingJunkAfterACompleteObject_KeepsTheObject()
    {
        var http = new FakeHttp().Ok($$$"""{"model":"gemma4:31b","message":{"role":"assistant","content":{{{Escaped(Answer + "\"")}}} },"done":true,"done_reason":"stop","prompt_eval_count":9,"eval_count":12}""");
        var client = new OllamaAgentClient(http.Client(), Profile("ollama", "https://ollama.com"), AgentRetryPolicy.Immediate);

        var result = await client.CompleteAsync(Request(), CancellationToken.None);

        result.Json.ShouldBe(Answer);
    }

    /// <summary>The second error of that run: one closer too many at the end (<c>]]}]]}</c>); the closers are rebuilt, the content is untouched.</summary>
    [Theory]
    [InlineData("""{"packageId":"p1","verses":[{"v":"v1","words":[[1,"καί","G2532","CONJ",[1],"high"]]}]]}""")]
    [InlineData("""{"packageId":"p1","verses":[{"v":"v1","words":[[1,"καί","G2532","CONJ",[1],"high"]]}""")]
    [InlineData("""{"packageId":"p1","verses":[{"v":"v1","words":[[1,"a]}",  "G2532","CONJ",[1],"high"]]}]}}""")]
    public void WrongClosersAtTheEnd_AreRebuiltFromTheBracketStack(string answer)
    {
        var repaired = AgentHttpAdapter.RebalancedClosers(answer);

        repaired.ShouldNotBeNull();
        repaired.ShouldEndWith("\"high\"]]}]}");
        repaired.ShouldStartWith(answer[..answer.IndexOf("\"high\"", StringComparison.Ordinal)]);
    }

    [Theory]
    [InlineData("""{"packageId":"p1","verses":[{"v":"v1","words":[[1,"καί","G2532","CONJ",[1],"high"]]},"note":"x"}]}""")]
    [InlineData("""{"packageId":"p1","verses":[{"v":"v1","words":[[1,"καί""")]
    [InlineData("""{"packageId":"p1","verses":[{"v":"v1"],"words":[]}]}""")]
    public void OtherErrors_AreNotRepaired(string answer) => AgentHttpAdapter.RebalancedClosers(answer).ShouldBeNull();

    /// <summary>A structural error (gemma4 closed the verse before a trailing <c>note</c>) stays a schema failure, with the parse position and an excerpt.</summary>
    [Fact]
    public async Task Ollama_BrokenStructure_IsASchemaFailure_WithAnExcerptOfTheError()
    {
        const string broken = """{"packageId":"p1","verses":[{"v":"v1","words":[[1,"καί","G2532","CONJ",[1],"high"]]},"note":"Νωεμεὶν"}]}""";
        var http = new FakeHttp().Ok($$$"""{"model":"gemma4:31b","message":{"role":"assistant","content":{{{Escaped(broken)}}} },"done":true,"done_reason":"stop","prompt_eval_count":9,"eval_count":12}""");
        var client = new OllamaAgentClient(http.Client(), Profile("ollama", "https://ollama.com"), AgentRetryPolicy.Immediate);

        var error = await Should.ThrowAsync<AgentModelException>(() => client.CompleteAsync(Request(), CancellationToken.None));

        error.Kind.ShouldBe(AgentModelErrorKind.SchemaFailure);
        error.Message.ShouldContain("not valid JSON");
        error.Message.ShouldContain("\"high\"]]},\"note\"");
        error.OutputTokens.ShouldBe(12);
    }

    // ---- Ollama native ------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Ollama_SendsFormatSchema_ThinkFalse_AndReadsCachedCount()
    {
        var http = new FakeHttp().Ok($$$"""{"model":"gemma4:31b","message":{"role":"assistant","content":{{{Escaped(Answer)}}} },"done":true,"done_reason":"stop","prompt_eval_count":900,"prompt_eval_cached_count":700,"eval_count":120}""");
        var client = new OllamaAgentClient(http.Client(), Profile("ollama", "https://ollama.com"), AgentRetryPolicy.Immediate);

        var result = await client.CompleteAsync(Request(), CancellationToken.None);

        var req = http.Requests.Single();
        req.Url.ShouldBe("https://ollama.com/api/chat");
        req.Headers["Authorization"].ShouldBe("Bearer secret-key");
        var body = req.Body!;
        body["stream"]!.GetValue<bool>().ShouldBeFalse();
        body["think"]!.GetValue<bool>().ShouldBeFalse();
        body["format"]!["type"]!.GetValue<string>().ShouldBe("object");
        body["options"]!["num_predict"]!.GetValue<int>().ShouldBe(1234);
        body["messages"]![0]!["role"]!.GetValue<string>().ShouldBe("system");

        result.InputTokens.ShouldBe(900);
        result.CachedInputTokens.ShouldBe(700);
        result.OutputTokens.ShouldBe(120);
        result.Model.ShouldBe("gemma4:31b");
    }

    [Fact]
    public async Task Ollama_SchemaModeJson_SendsFormatJsonString()
    {
        var http = new FakeHttp().Ok($$$"""{"message":{"content":{{{Escaped(Answer)}}} },"done_reason":"stop","prompt_eval_count":1,"eval_count":1}""");
        var client = new OllamaAgentClient(http.Client(), Profile("ollama", "https://ollama.com", schemaMode: AgentSchemaModes.Json), AgentRetryPolicy.Immediate);

        await client.CompleteAsync(Request(), CancellationToken.None);

        http.Requests.Single().Body!["format"]!.GetValue<string>().ShouldBe("json");
    }

    [Fact]
    public async Task Ollama_DoneReasonLength_IsASchemaFailure()
    {
        var http = new FakeHttp().Ok("""{"message":{"content":"{\"packageId\":\"p1\",\"verses\":[{"},"done":true,"done_reason":"length","prompt_eval_count":900,"eval_count":8192}""");
        var client = new OllamaAgentClient(http.Client(), Profile("ollama", "https://ollama.com"), AgentRetryPolicy.Immediate);

        var error = await Should.ThrowAsync<AgentModelException>(() => client.CompleteAsync(Request(), CancellationToken.None));

        error.Kind.ShouldBe(AgentModelErrorKind.SchemaFailure);
        error.OutputTokens.ShouldBe(8192);
    }

    // ---- shared behaviour (OpenAI adapter as the example) ---------------------------------------------------------------------

    private static OpenAiAgentClient OpenAi(FakeHttp http, int maxRetries = 3) =>
        new(http.Client(), Profile("openai", "https://x.example/v1"), new AgentRetryPolicy { MaxRetries = maxRetries, Delay = _ => TimeSpan.Zero });

    private static string Good => $$$"""{"choices":[{"message":{"content":{{{Escaped(Answer)}}} },"finish_reason":"stop"}],"usage":{"prompt_tokens":1,"completion_tokens":1}}""";

    [Fact]
    public async Task TooManyRequests_ThenOk_IsRetried()
    {
        var http = new FakeHttp().Add(HttpStatusCode.TooManyRequests, "slow down").Add(HttpStatusCode.ServiceUnavailable, "down").Ok(Good);

        var result = await OpenAi(http).CompleteAsync(Request(), CancellationToken.None);

        http.Requests.Count.ShouldBe(3);
        result.Json.ShouldBe(Answer);
    }

    [Fact]
    public async Task ServerErrorsThatStay_AreTransientAfterTheRetries()
    {
        var http = new FakeHttp();
        for (var i = 0; i < 5; i++)
        {
            http.Add(HttpStatusCode.BadGateway, "bad gateway");
        }

        var error = await Should.ThrowAsync<AgentModelException>(() => OpenAi(http, maxRetries: 2).CompleteAsync(Request(), CancellationToken.None));

        error.Kind.ShouldBe(AgentModelErrorKind.Transient);
        http.Requests.Count.ShouldBe(3, "the first call and two retries");
    }

    [Fact]
    public async Task Unauthorized_IsAuth_AndNotRetried_AndTheKeyIsNotInTheMessage()
    {
        var http = new FakeHttp().Add(HttpStatusCode.Unauthorized, "invalid key");

        var error = await Should.ThrowAsync<AgentModelException>(() => OpenAi(http).CompleteAsync(Request(), CancellationToken.None));

        error.Kind.ShouldBe(AgentModelErrorKind.Auth);
        error.Message.ShouldNotContain("secret-key");
        http.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task OtherClientError_IsBadRequest_AndNotRetried()
    {
        var http = new FakeHttp().Add(HttpStatusCode.BadRequest, "unknown model");

        var error = await Should.ThrowAsync<AgentModelException>(() => OpenAi(http).CompleteAsync(Request(), CancellationToken.None));

        error.Kind.ShouldBe(AgentModelErrorKind.BadRequest);
        error.Message.ShouldContain("unknown model");
        http.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AnswerThatIsNotJson_IsASchemaFailure()
    {
        var http = new FakeHttp().Ok("""{"choices":[{"message":{"content":"Sure! Here is the mapping."},"finish_reason":"stop"}],"usage":{"prompt_tokens":3,"completion_tokens":4}}""");

        var error = await Should.ThrowAsync<AgentModelException>(() => OpenAi(http).CompleteAsync(Request(), CancellationToken.None));

        error.Kind.ShouldBe(AgentModelErrorKind.SchemaFailure);
        error.InputTokens.ShouldBe(3);
    }

    [Fact]
    public async Task MarkdownFenceAroundTheJson_IsRemoved()
    {
        var http = new FakeHttp().Ok($$$"""{"choices":[{"message":{"content":{{{Escaped("```json\n" + Answer + "\n```")}}}},"finish_reason":"stop"}],"usage":{"prompt_tokens":1,"completion_tokens":1}}""");

        var result = await OpenAi(http).CompleteAsync(Request(), CancellationToken.None);

        result.Json.ShouldBe(Answer);
    }

    [Fact]
    public async Task ResponseThatIsNotJson_IsASchemaFailure()
    {
        var http = new FakeHttp().Ok("<html>gateway page</html>");

        var error = await Should.ThrowAsync<AgentModelException>(() => OpenAi(http).CompleteAsync(Request(), CancellationToken.None));

        error.Kind.ShouldBe(AgentModelErrorKind.SchemaFailure);
    }

    [Fact]
    public async Task Cancellation_IsNotRetried()
    {
        var http = new FakeHttp().Ok(Good);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var error = await Record.ExceptionAsync(() => OpenAi(http).CompleteAsync(Request(), cts.Token));

        error.ShouldBeAssignableTo<OperationCanceledException>();
        http.Requests.Count.ShouldBeLessThanOrEqualTo(1);
    }

    [Fact]
    public void Factory_PicksTheAdapterOfTheKind()
    {
        var factory = new AgentModelClientFactory(new HttpClient(), AgentRetryPolicy.Immediate);

        factory.Create(Profile("anthropic", "https://a.example")).ShouldBeOfType<AnthropicAgentClient>();
        factory.Create(Profile("gemini", "https://a.example")).ShouldBeOfType<GeminiAgentClient>();
        factory.Create(Profile("openai", "https://a.example")).ShouldBeOfType<OpenAiAgentClient>();
        factory.Create(Profile("ollama", "https://a.example")).ShouldBeOfType<OllamaAgentClient>();
        Should.Throw<NotSupportedException>(() => factory.Create(Profile("nope", "https://a.example")));
    }

    [Fact]
    public void Schema_IsValidJson_AndKeepsTheAnswerShape()
    {
        var schema = JsonNode.Parse(AgentAnswerSchema.Json)!;

        schema["properties"]!["verses"]!["items"]!["properties"]!.AsObject().Select(p => p.Key)
            .ShouldBe(["v", "links", "added", "lemmas", "parts", "unmapped", "note"]);
    }
}
