using System.Diagnostics;
using System.Text.Json;
using Google.GenAI;
using Google.GenAI.Types;
using Polly;
using Polly.Retry;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Tooling.AiWorker.Mapping.Models;
using GeminiClient = Google.GenAI.Client;

namespace UnshackledWord.Tooling.AiWorker;

public abstract class GeminiFlashAbstractClient
{
    protected AsyncRetryPolicy _apiErrorPolicies;
    protected readonly GeminiClient _client;
    protected readonly ILogger _logger;

    public GeminiFlashAbstractClient(GeminiClient client, ILogger<GeminiFlashAbstractClient> logger)
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

    protected abstract GenerateContentConfig GetResponseSchema(string systemInstructions);
}
