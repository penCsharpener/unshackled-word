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

    protected async Task<List<VerseDataList<string>>> SubmitAsync(string prompt, string systemInstructions, string modelName = "gemini-2.5-flash", CancellationToken token = default)
    {
        var response = await _apiErrorPolicies.ExecuteAsync(async () =>
        {
            var timeStamp = Stopwatch.GetTimestamp();
            var mappings = await _client.Models.GenerateContentAsync(
                model: modelName,
                contents: prompt,
                config: GetResponseSchema(systemInstructions),
                cancellationToken: token
            );
            var elapsed = Stopwatch.GetElapsedTime(timeStamp);
            var elapsedString = elapsed.ToString(@"mm\:ss");
            _logger.LogInformation("Prompt size: {promptSize}, SystemInstruction: {instructionSize} Request took {elapsed}", prompt.Length, systemInstructions.Length, elapsedString);

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

        if (_logger.IsEnabled(LogLevel.Trace))
        {
            _logger.LogTrace("Prompt: {prompt} - Response: {response}", prompt, text);
        }

        return JsonSerializer.Deserialize<List<VerseDataList<string>>>(text) ?? [];
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
                    ["RefId"] = new() { Type = GeminiType.Integer, Description = "Is combined Integer of (BookId * 1000000) + (Chapter * 1000) + Verse" },
                    ["Data"] = new()
                    {
                        Type = GeminiType.Array,
                        Items = new Schema
                        {
                            Type = GeminiType.String,
                            Description = "A pipe-delimited string (no spaces) representing these 6 fields: " +
                                          "ElbWordId|StepWordId|IsAddedWord|ParentElbWordId|PartOrder|GermanWordPart " +
                                          "Rules: 1=true, 0=false, '-'=null. " +
                                          "Examples: '123|456|0|-|-|-', '123|-|1|456|-|-', '123|456|0|-|1|Gersten', '123|876|0|-|2|ernte'"
                        }
                    }
                },
                Required = ["BookId", "Chapter", "Verse", "RefId", "Data"]
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
