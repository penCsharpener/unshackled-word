using UnshackledWord.Application.Features.WordDetail;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetWordDetail;

namespace UnshackledWord.Application.Features.Revision;

public sealed class RevisionWordDetailService : IRevisionWordDetailService
{
    private readonly IRevisionWordDetailRepository _repository;
    private readonly IWordDetailService _wordDetail;

    public RevisionWordDetailService(IRevisionWordDetailRepository repository, IWordDetailService wordDetail)
    {
        _repository = repository;
        _wordDetail = wordDetail;
    }

    public async Task<GetWordDetailResponse?> GetAsync(int revisionWordId, CancellationToken token = default)
    {
        var header = await _repository.GetWordAsync(revisionWordId, token);
        if (header is null)
        {
            return null;
        }

        IReadOnlyList<ReadingRowBase> rows = RevisionSql.IsHebrew(header.BibleBookId)
            ? await _repository.GetHebrewReadingsAsync(revisionWordId, token)
            : await _repository.GetGreekReadingsAsync(revisionWordId, token);
        IReadOnlyList<CoMappedWordRow> coRows = rows.Count == 0 ? [] : await _repository.GetCoMappedWordsAsync(revisionWordId, token);

        var word = new ElbWordRow
        {
            Id = header.Id,
            BibleBookId = header.BibleBookId,
            Chapter = header.Chapter,
            Verse = header.Verse,
            HebRefId = header.HebRefId,
            PositionInVerse = header.PositionInVerse,
            WordInContext = header.WordInContext,
            GermanLemma = header.GermanLemma,
        };

        var response = await _wordDetail.BuildAsync(word, rows, coRows, SourceOf(rows), token);
        response.Revision = new RevisionWordInfoResponse { Level = header.Level, Change = header.Change, BaseWord = header.BaseWord };
        return response;
    }

    /// <summary>One value for the whole word: a hand-made row wins over the seed, the seed over the algorithm.</summary>
    private static string SourceOf(IReadOnlyList<ReadingRowBase> rows)
    {
        var origins = rows.Select(r => r.MappingOrigin).ToHashSet();
        return origins.Contains(RevisionOrigin.Manual) ? RevisionOrigin.Manual
            : origins.Contains(RevisionOrigin.Seed) ? RevisionOrigin.Seed
            : RevisionOrigin.Carried;
    }
}
