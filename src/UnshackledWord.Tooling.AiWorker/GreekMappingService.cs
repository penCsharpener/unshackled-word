using UnshackledWord.Tooling.AiWorker.Models;

namespace UnshackledWord.Tooling.AiWorker;

public class GreekMappingService
{
    private readonly GreekMappingRepository _repo;
    private readonly GreekGeminiFlashClient _client;
    private readonly ILogger<GreekMappingService> _logger;

    public GreekMappingService(GreekMappingRepository repo, GreekGeminiFlashClient client, ILogger<GreekMappingService> logger)
    {
        _repo = repo;
        _client = client;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken token = default)
    {
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
                var minVerse = verseChunk.Min();
                var maxVerse = verseChunk.Max();

                await MapWordsForRangeAsync(minVerse, maxVerse, bRef, token);
            }
        }
    }

    internal async Task MapWordsForRangeAsync(int minVerse, int maxVerse, BibleReferenceRange bRef, CancellationToken token)
    {
        var elbWords = await _repo.GetElbVerseDataAsync(bRef.BibleBookId, bRef.Chapter, minVerse, maxVerse);
        var stepWords = await _repo.GetStepGreekVerseDataAsync(bRef.BibleBookId, bRef.Chapter, minVerse, maxVerse);
        var wordCount = elbWords.SelectMany(x => x.Data).Count();

        _logger.LogInformation("Submitting {bookId}:{chapter}:{minVerse}-{maxVerse} of a total of {totalVerses} verses with {totalWords} words", bRef.BibleBookId, bRef.Chapter, minVerse, maxVerse, bRef.MaxVerse, wordCount);

        var response = await _client.GetElbStepMappings(elbWords, stepWords, token);

        await _repo.InsertMappingsAsync(response,
            elbWords.SelectMany(x => x.Data).ToList(),
            stepWords.SelectMany(x => x.Data).ToList());
    }
}
