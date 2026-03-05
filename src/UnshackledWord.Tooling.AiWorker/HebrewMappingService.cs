using Polly;
using UnshackledWord.Tooling.AiWorker.Models;

namespace UnshackledWord.Tooling.AiWorker;

public class HebrewMappingService
{
    private readonly HebrewMappingRepository _repo;
    private readonly HebrewGeminiFlashClient _client;
    private readonly ILogger<HebrewMappingService> _logger;

    public HebrewMappingService(HebrewMappingRepository repo, HebrewGeminiFlashClient client, ILogger<HebrewMappingService> logger)
    {
        _repo = repo;
        _client = client;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken token = default)
    {
        var policy = Policy.Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(5, retryAttempt) + (10 * retryAttempt)), onRetry:
                (ex, timeSpan, retryCount, context) =>
                {
                    _logger.LogError(ex, "Retry {retryCount} after {delay} delay.", retryCount, timeSpan.ToString(@"mm\:ss"));
                });

        // Best practice: respect the cancellation token in your while loop
        while (!token.IsCancellationRequested)
        {
            var structureData = await _repo.GetMissingVerseRangesAsync();
            var bRef = structureData.FirstOrDefault();

            if (bRef is null)
            {
                break;
            }

            await policy.ExecuteAsync(async () => await MapHebrewAsync(token, bRef));
        }
    }

    private async Task MapHebrewAsync(CancellationToken token, BibleReferenceRange bRef)
    {
        // 1. Get chunks of 5 verses (as you originally had)
        var verseChunks = Enumerable.Range(bRef.MinVerse, bRef.MaxVerse - bRef.MinVerse + 1).Chunk(5);

        // 2. Chunk the verse chunks into batches of 5.
        // This means we prepare up to 5 parallel requests per iteration.
        foreach (var requestBatch in verseChunks.Chunk(5))
        {
            // 3. Create up to 5 concurrent Tasks
            var tasks = requestBatch.Select(async verseChunk =>
            {
                try
                {
                    var minVerse = verseChunk.Min();
                    var maxVerse = verseChunk.Max();

                    // Optional: You can pass 'token' to these repo calls if they support it
                    var elbWords = await _repo.GetElbVerseDataAsync(bRef.BibleBookId, bRef.Chapter, minVerse, maxVerse);
                    var stepWords = await _repo.GetHebrewVerseDataAsync(bRef.BibleBookId, bRef.Chapter, minVerse, maxVerse);
                    var wordCount = elbWords.SelectMany(x => x.Data).Count();

                    _logger.LogInformation("Submitting {bookId}:{chapter}:{minVerse}-{maxVerse} of a total of {totalVerses} verses with {totalWords} words",
                        bRef.BibleBookId, bRef.Chapter, minVerse, maxVerse, bRef.MaxVerse, wordCount);

                    var response = await _client.GetElbStepMappings(elbWords, stepWords, token);

                    // 4. Return an anonymous object mapping the successful results
                    return new
                    {
                        Response = response.Where(x => x.Data.All(k => k.ElbWordId > 0)).ToList(),
                        ElbWords = elbWords.SelectMany(x => x.Data).ToList(),
                        StepWords = stepWords.SelectMany(x => x.Data).ToList()
                    };
                }
                catch (Exception ex)
                {
                    // 5. Catch exception internally per request so the other 4 aren't aborted
                    _logger.LogError(ex, "Failed to map verses {minVerse}-{maxVerse}", verseChunk.Min(), verseChunk.Max());

                    // Return null to signify this specific request failed
                    return null;
                }
            });

            // 6. Await the batch of 5 requests concurrently
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
}
