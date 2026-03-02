using System.Diagnostics;
using Google.GenAI;
using Polly;

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
        var serverErrorPolicy = Policy.Handle<ServerError>()
            .WaitAndRetryAsync(
                retryCount: 15,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(1.2, retryAttempt) + 10), onRetry:
                (ex, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning("Retry {retryCount} after {delay}s delay.", retryCount, timeSpan.Seconds);
                });

        var httpTimeoutPolicy = Policy.Handle<TimeoutException>()
            .WaitAndRetryAsync(
                retryCount: 15,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), onRetry:
                (ex, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning("Retry {retryCount} after {delay}s delay.", retryCount, timeSpan.Seconds);
                });
        var wrappedPolicy = Policy.WrapAsync(serverErrorPolicy, httpTimeoutPolicy);

        while (true)
        {
            var structureData = await _repo.GetMissingVerseRangesAsync();
            var bRef = structureData.FirstOrDefault();

            if (bRef is null)
            {
                break;
            }

            foreach (var verseChunk in Enumerable.Range(bRef.MinVerse, bRef.MaxVerse - bRef.MinVerse + 1).Chunk(5))
            {
                var timeStamp = Stopwatch.GetTimestamp();
                var minVerse = verseChunk.Min();
                var maxVerse = verseChunk.Max();

                var elbWords = await _repo.GetElbVerseDataAsync(bRef.BibleBookId, bRef.Chapter, minVerse, maxVerse);
                var stepWords = await _repo.GetHebrewVerseDataAsync(bRef.BibleBookId, bRef.Chapter, minVerse, maxVerse);
                var wordCount = elbWords.SelectMany(x => x.Data).Count();

                _logger.LogInformation("Submitting {bookId}:{chapter}:{minVerse}-{maxVerse} of a total of {totalVerses} verses with {totalWords} words", bRef.BibleBookId, bRef.Chapter, minVerse, maxVerse, bRef.MaxVerse, wordCount);

                var response = await wrappedPolicy.ExecuteAsync(async () =>
                {
                    return await _client.GetElbStepMappings(elbWords, stepWords, token);
                });

                await _repo.InsertMappingsAsync(response, elbWords.SelectMany(x => x.Data).ToList());
                var elapsed = Stopwatch.GetElapsedTime(timeStamp);
                _logger.LogInformation("Request took {minutes}:{seconds}", elapsed.Minutes, elapsed.Seconds);
            }
        }
    }
}
