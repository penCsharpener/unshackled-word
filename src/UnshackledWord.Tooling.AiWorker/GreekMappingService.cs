using Microsoft.Extensions.Options;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Tooling.AiWorker.Models;
using UnshackledWord.Tooling.AiWorker.Models.Greek;

namespace UnshackledWord.Tooling.AiWorker;

public class GreekMappingService
{
    private readonly GreekMappingRepository _repo;
    private readonly GreekGeminiFlashClient _client;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GreekMappingService> _logger;

    public GreekMappingService(GreekMappingRepository repo, GreekGeminiFlashClient client, IServiceScopeFactory scopeFactory, ILogger<GreekMappingService> logger)
    {
        _repo = repo;
        _client = client;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken token = default)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<GoogleAiOptions>>()
                    .CurrentValue;

                var versesPerTask = options.VersesPerTask;
                var parallelTasks = options.MaxParallelTasks;

                var missingVerses = await _repo.GetMissingVersesAsync();

                if (missingVerses.IsNullOrEmpty())
                {
                    _logger.LogWarning("no more verses left to map.");
                    break;
                }

                foreach (var requestBatches in missingVerses.Take(versesPerTask * parallelTasks).Chunk(versesPerTask).Chunk(parallelTasks))
                {
                    var tasks = requestBatches.Select(async verses =>
                    {
                        try
                        {
                            var elbWords = await _repo.GetElbVerseDataAsync(verses.Select(x => x.HebRefId).ToArray());
                            var stepWords = await _repo.GetStepGreekVerseDataAsync(verses.Select(x => x.LxxRefId).ToArray());
                            var first = verses.GetFirstReference();
                            var last = verses.GetLastReference();

                            _logger.LogInformation("Submitting {firstRef}-{lastRef} of a total of {totalVerses} verses with {totalWords} words", first, last, verses.Length, elbWords.Select(x => x.Data.Count()).Sum());

                            var response = await _client.GetElbStepMappings(elbWords, stepWords, token);
                            var validator = new AiResponseStatsValidation<StepGreekVerseData>();
                            var stats = new AiResponseStats<StepGreekVerseData>(response, elbWords, stepWords);
                            var validationResult = await validator.ValidateAsync(stats, token);

                            if (!validationResult.IsValid)
                            {
                                var errorSummary = validationResult.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}");
                                _logger.LogWarning("Validation failed: {Errors}", errorSummary.JoinStrings("||"));
                                return null;
                            }

                            _logger.LogInformation("\"HebRefId\" IN ({ids})", elbWords.Select(x => x.RefId).JoinStrings(","));

                            return new CombinedMappingResult<StepGreekVerseData>
                            {
                                Response = response.Where(x => x.Data.All(k => k.ElbWordId > 0)).ToList(),
                                ElbWords = elbWords.SelectMany(x => x.Data).ToList(),
                                StepWords = stepWords.SelectMany(x => x.Data).ToList()
                            };
                        }
                        catch (TaskCanceledException)
                        {
                            return null;
                        }
                        catch (KeyNotFoundException keyEx)
                        {
                            _logger.LogError("Failed to map verses {idRefs}: {msg}",
                                verses.Select(x => x.HebRefId).JoinStrings(","), keyEx.Message);
                            return null;
                        }
                        catch (Exception ex)
                        {
                            // 5. Catch exception internally per request so the other 4 aren't aborted
                            _logger.LogError(ex, "Failed to map verses {idRefs}",
                                verses.Select(x => x.HebRefId).JoinStrings(","));

                            // Return null to signify this specific request failed
                            return null;
                        }
                    });

                    var batchResults = await Task.WhenAll(tasks);

                    // 7. Filter out the failed requests (the ones that returned null)
                    var successfulResults = batchResults.Where(x => x is not null).ToList();

                    if (successfulResults.Count == 0)
                    {
                        continue;
                    }

                    // 8. Combine the lists from the successful requests.
                    var combinedResponses = successfulResults.SelectMany(x => x!.Response).ToList();
                    var combinedElbWords = successfulResults.SelectMany(x => x!.ElbWords).ToList();
                    var combinedStepWords = successfulResults.SelectMany(x => x!.StepWords).ToList();

                    // 9. Insert all gathered results from the up to 5 requests in a single database call
                    await _repo.InsertMappingsAsync(combinedResponses, combinedElbWords, combinedStepWords);
                }
            }
            catch (KeyNotFoundException keyEx)
            {
                _logger.LogError(keyEx.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during processing Greek mapping");
            }
        }
    }
}
