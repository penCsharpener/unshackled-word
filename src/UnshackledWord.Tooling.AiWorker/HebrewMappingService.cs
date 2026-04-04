using Polly;
using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Dbo;
using UnshackledWord.Tooling.AiWorker.Models;
using UnshackledWord.Tooling.AiWorker.Models.Hebrew;

namespace UnshackledWord.Tooling.AiWorker;

public class HebrewMappingService
{
    private readonly HebrewMappingRepository _repo;
    private readonly HebrewGeminiFlashClient _client;
    private readonly ILogger<HebrewMappingService> _logger;
    private Dictionary<int, string>? _booksDictionary;

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

        var bookNames = await _repo.GetBookNamesAsync();
        _booksDictionary = bookNames.ToDictionary(x => x.Id, x => x.Name);
        const int versesPerTask = 10;
        const int parallelTasks = 1;

        while (!token.IsCancellationRequested)
        {
            try
            {
                var missingVerses = await _repo.GetMissingVersesAsync();

                if (missingVerses.IsNullOrEmpty())
                {
                    _logger.LogWarning("no more verses left to map.");
                    break;
                }

                foreach (var requestBatches in missingVerses.Chunk(versesPerTask).Chunk(parallelTasks))
                {
                    var tasks = requestBatches.Select(async verses =>
                    {
                        try
                        {
                            var elbWords = await _repo.GetElbVerseDataAsync(verses.Select(x => x.HebRefId).ToArray());
                            var stepWords = await _repo.GetStepHebrewVerseDataAsync(verses.Select(x => x.LxxRefId).ToArray());
                            var first = verses.GetFirstReference();
                            var last = verses.GetLastReference();

                            _logger.LogInformation("Submitting {firstRef}-{lastRef} of a total of {totalVerses} verses with {totalWords} words", first, last, verses.Length, elbWords.Count);

                            var response = await _client.GetElbStepMappings(elbWords, stepWords, token);

                            _logger.LogInformation("\"HebRefId\" IN ({ids})", response.Select(x => x.RefId).JoinStrings(","));

                            return new CombinedMappingResult<StepHebrewVerseData>
                            {
                                Response = response.Where(x => x.Data.All(k => k.ElbWordId > 0)).ToList(),
                                ElbWords = elbWords.SelectMany(x => x.Data).ToList(),
                                StepWords = stepWords.SelectMany(x => x.Data).ToList()
                            };
                        }
                        catch (Exception ex)
                        {
                            // 5. Catch exception internally per request so the other 4 aren't aborted
                            _logger.LogError(ex, "Failed to map verses {idRefs}", verses.Select(x => x.HebRefId).JoinStrings(","));

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

    internal async Task MapWordsForRangeAsync(BibleVerseCountingMappingDbo[] verses, CancellationToken token)
    {
        var elbWords = await _repo.GetElbVerseDataAsync(verses.Select(x => x.HebRefId).ToArray());
        var stepWords = await _repo.GetStepHebrewVerseDataAsync(verses.Select(x => x.LxxRefId).ToArray());
        var first = verses.GetFirstReference();
        var last = verses.GetLastReference();

        _logger.LogInformation("Submitting {firstRef}-{lastRef} of a total of {totalVerses} verses with {totalWords} words", first, last, verses.Length, elbWords.Count);

        var response = await _client.GetElbStepMappings(elbWords, stepWords, token);

        _logger.LogInformation("\"HebRefId\" IN ({ids})", response.Select(x => x.RefId).JoinStrings(","));

        await _repo.InsertMappingsAsync(response,
            elbWords.SelectMany(x => x.Data).ToList(),
            stepWords.SelectMany(x => x.Data).ToList());
    }

    internal async Task MapHebrewAsync(BibleReferenceRange bRef, CancellationToken token = default)
    {
        if (_booksDictionary.IsNullOrEmpty())
        {
            var bookNames = await _repo.GetBookNamesAsync();
            _booksDictionary = bookNames.ToDictionary(x => x.Id, x => x.Name);
        }

        // 1. Get chunks of 5 verses (as you originally had)
        var verseChunks = Enumerable.Range(bRef.Start.Verse, bRef.End.Verse - bRef.Start.Verse + 1).Chunk(10);

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
                    var elbWords = await _repo.GetElbVerseDataAsync(bRef.Start.BookId, bRef.Start.Chapter, minVerse, maxVerse);
                    var stepWords = await _repo.GetHebrewVerseDataAsync(bRef.Start.BookId, bRef.Start.Chapter, minVerse, maxVerse);
                    var wordCount = elbWords.SelectMany(x => x.Data).Count();
                    var gotBookName = _booksDictionary.TryGetValue(bRef.Start.BookId, out string bookName);

                    _logger.LogInformation("Submitting {bookId} {chapter}:{minVerse}-{maxVerse} of a total of {totalVerses} verses with {totalWords} words",
                        gotBookName ? bookName : bRef.Start.BookId, bRef.Start.Chapter, minVerse, maxVerse, bRef.End.Verse, wordCount);

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

