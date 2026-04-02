using UnshackledWord.Domain.Models.BibleStructure;
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
        while (!token.IsCancellationRequested)
        {
            var structureData = await _repo.GetMissingVerseRangesAsync();
            var scope = structureData.FirstOrDefault();

            if (scope is null)
            {
                break;
            }

            var start = new BibleReference(scope.BibleBookId, scope.Chapter, scope.MinVerse);
            var end = new BibleReference(scope.BibleBookId, scope.Chapter, scope.MaxVerse);
            var bRef = new BibleReferenceRange(start, end);

            foreach (var verseChunk in Enumerable.Range(bRef.Start.Verse, bRef.End.Verse - bRef.Start.Verse + 1).Chunk(5))
            {
                var minVerse = verseChunk.Min();
                var maxVerse = verseChunk.Max();

                await MapWordsForRangeAsync(minVerse, maxVerse, bRef, token);
            }
        }
    }

    internal async Task MapWordsForRangeAsync(int minVerse, int maxVerse, BibleReferenceRange bRef, CancellationToken token)
    {
        var elbWords = await _repo.GetElbVerseDataAsync(bRef.Start.BookId, bRef.Start.Chapter, minVerse, maxVerse);
        var stepWords = await _repo.GetStepGreekVerseDataAsync(bRef.Start.BookId, bRef.Start.Chapter, minVerse, maxVerse);
        var wordCount = elbWords.SelectMany(x => x.Data).Count();

        _logger.LogInformation("Submitting {bookId}:{chapter}:{minVerse}-{maxVerse} of a total of {totalVerses} verses with {totalWords} words", bRef.Start.BookId, bRef.Start.Chapter, minVerse, maxVerse, bRef.Start.Verse, wordCount);

        var response = await _client.GetElbStepMappings(elbWords, stepWords, token);

        response = response.Where(x => x.Data.All(d => d.ElbWordId >= 551768)).ToList();

        await _repo.InsertMappingsAsync(response,
            elbWords.SelectMany(x => x.Data).ToList(),
            stepWords.SelectMany(x => x.Data).ToList());
    }
}
