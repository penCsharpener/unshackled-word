using UnshackledWord.Application.Features.Revision;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.WebApi.Elberfelder.GetChapterCrossReferences;

namespace UnshackledWord.Application.Features.CrossReferences;

public sealed class ChapterCrossReferenceService : IChapterCrossReferenceService
{
    /// <summary>Safety cap: the busiest chapter (Psalm 119) has about 1,700 TSK rows.</summary>
    public const int MaxRows = 5000;

    private readonly ITskRepository _tsk;

    public ChapterCrossReferenceService(ITskRepository tsk)
    {
        _tsk = tsk;
    }

    public async Task<GetChapterCrossReferencesResponse> GetAsync(int bibleBookId, int chapter, CancellationToken token = default)
    {
        var (from, to) = RevisionSql.ChapterRange(bibleBookId, chapter);
        var rows = await _tsk.GetOutgoingAsync(from, to, MaxRows, token);

        var verses = rows
            .GroupBy(r => r.SourceHebRefId % 1000)
            .OrderBy(g => g.Key)
            .Select(g => new VerseCrossReferencesResponse
            {
                Verse = g.Key,
                References = g
                    .Select(r =>
                    {
                        var reference = CrossReferenceMapper.Build(r);
                        reference.Keyword = string.IsNullOrWhiteSpace(r.Scope) ? null : r.Scope;
                        return reference;
                    })
                    .DistinctBy(r => (r.BibleBookId, r.Chapter, r.Verse, r.EndVerse))
                    .ToList(),
            })
            .ToList();

        return new GetChapterCrossReferencesResponse { BibleBookId = bibleBookId, Chapter = chapter, Verses = verses };
    }
}
