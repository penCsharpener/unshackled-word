using System.Diagnostics;
using System.Text.Json;
using Google.GenAI;
using Google.GenAI.Types;
using Polly;
using Polly.Retry;
using UnshackledWord.Application.Extensions;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Tooling.AiWorker.Models;
using UnshackledWord.Tooling.AiWorker.Models.Greek;
using GeminiClient = Google.GenAI.Client;
using GeminiType = Google.GenAI.Types.Type;

namespace UnshackledWord.Tooling.AiWorker;

public class GreekGeminiFlashClient
{
    private readonly GeminiClient _client;
    private readonly ILogger _logger;
    private const string ModelName = "gemini-2.5-flash";
    // private const string ModelName = "gemini-3-flash-preview";
    // Skip thinking and provide only the direct mapping in JSON format.
    private AsyncRetryPolicy _apiErrorPolicies;

    private const string GreekSystemInstruction = """
                                             You are a linguistic mapping tool mapping the Elberfelder 1871 German NT to STEP Bible Greek data.
                                             RULES:
                                             1. OUTPUT: Return a JSON object matching the provided schema.
                                             2. SPLIT VERBS: Map split German verb parts (e.g., 'aus' in 'geht...aus') to the same Greek 'StepWordId' and 'Strongs'.
                                             3. ADDED WORDS: If a German word has no Greek source, set 'IsAddedWord': true and 'StepWordId': null.
                                             4. PARENT MAPPING: For German words where IsAddedWord is true (e.g., articles like 'der' or particles), set ParentElbWordId to the ElbWordId of the semantic head of the phrase. For articles and adjectives, this is the Noun. For auxiliary verbs or split particles, this is the Main Verb. If 'der' refers to 'Tisch' in 'der kleine Tisch', map 'der' to the ID of 'Tisch', even if 'kleine' is in between.
                                             5. VERSE INTEGRITY: Never map a German word ID to a Greek word from a different verse.
                                             6. NO MARKDOWN: Return only raw JSON.
                                             """;

    public GreekGeminiFlashClient(GeminiClient client, ILogger<GreekGeminiFlashClient> logger)
    {
        _client = client;
        _logger = logger;

        _apiErrorPolicies = Policy.Handle<ServerError>().Or<HttpRequestException>().Or<TimeoutException>()
            .WaitAndRetryAsync(
                retryCount: 15,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(1.7, retryAttempt) + 10), onRetry:
                (ex, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning("Retry {retryCount} after {delay} delay. {exMessage}", retryCount, timeSpan.ToString(@"mm\:ss"), ex.Message);
                });
    }

    public async Task<List<VerseDataList<ElbStepAiMapping>>> GetElbStepMappings(List<VerseDataList<ElbVerseData>> elbWords,
        List<VerseDataList<StepGreekVerseData>> stepWords, CancellationToken token = default)
    {
        var germanVerseJson = elbWords.ToNonIndentedJson();
        var greekVerseJson = stepWords.ToNonIndentedJson();

        var prompt = $"""
                      German Words: {germanVerseJson}
                      Greek Words: {greekVerseJson}
                      """;

        var response = await _apiErrorPolicies.ExecuteAsync(async () =>
        {
            var timeStamp = Stopwatch.GetTimestamp();
            var mappings = await _client.Models.GenerateContentAsync(
                model: ModelName,
                contents: prompt,
                config: GetResponseSchema(),
                cancellationToken: token
            );
            var elapsed = Stopwatch.GetElapsedTime(timeStamp);
            var elapsedString = elapsed.ToString(@"mm\:ss");
            _logger.LogInformation("Request took {elapsed}", elapsedString);

            return mappings;
        });

        if (response.Candidates is null || response.Candidates.Count == 0)
        {
            _logger.LogDebug($@"No Candidates for verse ");
            return [];
        }

        if (response.Candidates[0].Content is null)
        {
            _logger.LogDebug($@"No Content for verse ");
            return [];
        }

        if (response.Candidates[0].Content!.Parts is null || response.Candidates[0].Content!.Parts!.Count == 0)
        {
            _logger.LogDebug($@"No parts for verse ");
            return [];
        }

        var text = response.Candidates[0].Content!.Parts![0].Text;

        if (text.IsNullOrWhiteSpace())
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<VerseDataList<ElbStepAiMapping>>>(text) ?? [];
    }

    private GenerateContentConfig GetResponseSchema()
    {
        // Define the schema as an object (OpenAPI 3.0 compatible)
        var responseSchema = new Schema
        {
            Type = GeminiType.Array,
            Items = new Schema
            {
                Type = GeminiType.Object,
                Properties = new Dictionary<string, Schema>
                {
                    ["BookId"] = new() { Type = GeminiType.Integer },
                    ["Chapter"] = new() { Type = GeminiType.Integer },
                    ["Verse"] = new() { Type = GeminiType.Integer },
                    ["Data"] = new()
                    {
                        Type = GeminiType.Array,
                        Items = new Schema
                        {
                            Type = GeminiType.Object,
                            Properties = new Dictionary<string, Schema>
                            {
                                ["ElbWordId"] = new() { Type = GeminiType.Integer },
                                ["StepWordId"] = new() { Type = GeminiType.Integer, Nullable = true },
                                ["Strongs"] = new() { Type = GeminiType.String, Nullable = true },
                                ["IsAddedWord"] = new() { Type = GeminiType.Boolean },
                                ["ParentElbWordId"] = new() { Type = GeminiType.Integer, Nullable = true }
                            },
                            Required = ["ElbWordId", "IsAddedWord"]
                        }
                    }
                },
                Required = ["BookId", "Chapter", "Verse", "Data"]

            }
        };

        var config = new GenerateContentConfig
        {
            ResponseMimeType = "application/json",
            ResponseSchema = responseSchema,
            // Temperature = 0.2,
            // TopP = 0.1f,
            // TopK = 1,
            // ThinkingConfig = new ThinkingConfig
            // {
            //     ThinkingLevel = ThinkingLevel.Medium
            // },
            SystemInstruction = new Content
            {
                Parts = new List<Part>
                {
                    new()
                    {
                        Text = GreekSystemInstruction
                    }
                }
            },
        };

        return config;
    }
}
