using UnshackledWord.Application.Features.ScriptureReferences;
using UnshackledWord.Application.Features.VerseCounting;
using UnshackledWord.Application.Features.VerseMapping;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Grammar;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetWordDetail;
using UnshackledWord.Domain.WebApi.Elberfelder.GetChapterOriginals;
using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;

namespace UnshackledWord.Application.Features.ChapterOriginals;

public sealed class ChapterOriginalsService : IChapterOriginalsService
{
    private const int LastHebrewBookId = 39;

    private readonly IVerseMappingRepository _repository;
    private readonly IStrongsLexiconRepository _lexicon;
    private readonly IVerseCountingMapProvider _countingMap;

    public ChapterOriginalsService(IVerseMappingRepository repository, IStrongsLexiconRepository lexicon, IVerseCountingMapProvider countingMap)
    {
        _countingMap = countingMap;
        _repository = repository;
        _lexicon = lexicon;
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
                    Morphology = MorphologyResponses.Decode(hebrew, w.MorphologyCode),
                }).ToList(),
                Links = linksByVerse.GetValueOrDefault(g.Key) ?? [],
            })
            .ToList();

        var lexicon = await BuildLexiconAsync(words.Select(w => w.Strongs), token);

        return new GetChapterOriginalsResponse
        {
            Lexicon = lexicon.Count == 0 ? null : lexicon,
            BibleBookId = bibleBookId,
            Chapter = chapter,
            Language = hebrew ? "hebrew" : "greek",
            Verses = verses,
        };
    }

    private async Task<Dictionary<string, ChapterLexiconResponse>> BuildLexiconAsync(IEnumerable<string?> strongsOfWords, CancellationToken token)
    {
        var result = new Dictionary<string, ChapterLexiconResponse>();
        var map = await _countingMap.GetAsync(token);
        foreach (var raw in strongsOfWords.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct())
        {
            var text = raw!.Trim();
            var open = text.IndexOf('{');
            var close = text.IndexOf('}');
            var cleaned = open >= 0 && close > open ? text[(open + 1)..close].Trim() : text;
            var parsed = StrongsNumber.Parse(cleaned);
            if (parsed.Value is not { } number)
            {
                continue;
            }

            var entries = await _lexicon.GetEntriesAsync(number, token);
            var entry = entries.FirstOrDefault(e => number.Extra is not null && string.Equals(e.Extra, number.Extra, StringComparison.OrdinalIgnoreCase))
                        ?? entries.FirstOrDefault(e => string.IsNullOrEmpty(e.Extra))
                        ?? entries.FirstOrDefault();
            if (entry is null)
            {
                continue;
            }

            var gloss = string.IsNullOrWhiteSpace(entry.Gloss) ? null : entry.Gloss.Trim();
            var (definition, _) = LexiconTextCleaner.Clean(entry.Lexicon, 300);
            var (segments, _) = LexiconTextSegmenter.Segment(entry.Lexicon, 300, map);
            if (gloss is null && definition.Length == 0)
            {
                continue;
            }

            result[raw!] = new ChapterLexiconResponse { Gloss = gloss, Definition = definition.Length == 0 ? null : definition, DefinitionSegments = LexiconSegmentMapper.ToResponse(segments) };
        }

        return result;
    }

}
