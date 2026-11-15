using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.WebApi.Elberfelder.GetChapterOriginals;
using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;

namespace UnshackledWord.Application.Features.ChapterOriginals;

public sealed class ChapterOriginalsService : IChapterOriginalsService
{
    private const int LastHebrewBookId = 39;

    private readonly IVerseMappingRepository _repository;

    public ChapterOriginalsService(IVerseMappingRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetChapterOriginalsResponse> GetAsync(int bibleBookId, int chapter, CancellationToken token = default)
    {
        var hebrew = bibleBookId <= LastHebrewBookId;
        var words = await _repository.GetChapterOriginalWordsAsync(hebrew, bibleBookId, chapter, token);
        var mapping = await _repository.GetChapterMappingRowsAsync(hebrew, bibleBookId, chapter, token);

        // Same verse numbering as chapterText: HebRefId % 1000.
        var verseOfWord = words.ToDictionary(w => w.Id, w => w.HebRefId % 1000);
        var linksByVerse = mapping
            .Where(m => verseOfWord.TryGetValue(m.StepWordId, out var verse) && verse == m.GermanVerse)
            .GroupBy(m => m.GermanVerse)
            .ToDictionary(
                g => g.Key,
                g => g.Select(m => new ChapterOriginalLinkResponse { GermanId = m.ElbWordId, OriginalId = m.StepWordId }).ToList());

        var verses = words
            .GroupBy(w => w.HebRefId % 1000)
            .OrderBy(g => g.Key)
            .Select(g => new VerseOriginalsResponse
            {
                Verse = g.Key,
                Words = g.Select(w => new VerseMappingOriginalWord
                {
                    Id = w.Id,
                    Position = w.Position,
                    Original = w.Original,
                    Lemma = w.Lemma,
                    Strongs = w.Strongs,
                    MorphologyCode = w.MorphologyCode,
                }).ToList(),
                Links = linksByVerse.GetValueOrDefault(g.Key) ?? [],
            })
            .ToList();

        return new GetChapterOriginalsResponse
        {
            BibleBookId = bibleBookId,
            Chapter = chapter,
            Language = hebrew ? "hebrew" : "greek",
            Verses = verses,
        };
    }
}
