using System.Diagnostics;
using System.Text.Json;
using Google.GenAI;
using Google.GenAI.Types;
using Polly;
using Polly.Retry;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Tooling.AiWorker.Models;
using GeminiClient = Google.GenAI.Client;
using GeminiType = Google.GenAI.Types.Type;

namespace UnshackledWord.Tooling.AiWorker;

public abstract class GeminiFlashAbstractClient
{
    protected AsyncRetryPolicy _apiErrorPolicies;
    protected readonly GeminiClient _client;
    protected readonly ILogger _logger;
    protected const string ModelName = "gemini-2.5-flash";
    // protected const string ModelName = "gemini-3-flash-preview";

    public GeminiFlashAbstractClient(GeminiClient client, ILogger<GreekGeminiFlashClient> logger)
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

    protected async Task<List<VerseDataList<ElbStepAiMapping>>> SubmitAsync(string prompt, string systemInstructions, CancellationToken token = default)
    {
        var response = await _apiErrorPolicies.ExecuteAsync(async () =>
        {
            var timeStamp = Stopwatch.GetTimestamp();
            var mappings = await _client.Models.GenerateContentAsync(
                model: ModelName,
                contents: prompt,
                config: GetResponseSchema(systemInstructions),
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

    private GenerateContentConfig GetResponseSchema(string systemInstructions)
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
                        Text = systemInstructions
                    }
                }
            },
        };

        return config;
    }
}
