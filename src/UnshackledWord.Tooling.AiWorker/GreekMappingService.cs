using UnshackledWord.Domain.Extensions;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Dbo;
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
            try
            {
                var missingVerses = await _repo.GetMissingVersesAsync();

                if (missingVerses.IsNullOrEmpty())
                {
                    _logger.LogWarning("no more verses left to map.");
                    break;
                }

                foreach (var verseChunk in missingVerses.Chunk(5))
                {
                    await MapWordsForRangeAsync(verseChunk, token);
                }
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
        var stepWords = await _repo.GetStepGreekVerseDataAsync(verses.Select(x => x.LxxRefId).ToArray());
        var first = verses.GetFirstReference();
        var last = verses.GetLastReference();

        _logger.LogInformation("Submitting {firstRef}-{lastRef} of a total of {totalVerses} verses with {totalWords} words", first, last, verses.Length, elbWords.Count);

        var response = await _client.GetElbStepMappings(elbWords, stepWords, token);

        _logger.LogInformation("\"HebRefId\" IN ({ids})", response.Select(x => x.RefId).JoinStrings(","));

        await _repo.InsertMappingsAsync(response,
            elbWords.SelectMany(x => x.Data).ToList(),
            stepWords.SelectMany(x => x.Data).ToList());
    }
}
