using UnshackledWord.Application.Features.Notes;
using UnshackledWord.Application.Features.Revision;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.WebApi.Elberfelder.GetChapterCrossReferences;
using UnshackledWord.Domain.WebApi.Tsk;

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

    public async Task<GetChapterCrossReferencesResponse> GetAsync(int bibleBookId, int chapter, bool includeHidden = false, int level = 0, CancellationToken token = default)
    {
        var (from, to) = RevisionSql.ChapterRange(bibleBookId, chapter);
        var rows = await _tsk.GetOutgoingAsync(from, to, MaxRows, token);

        // Curation (roadmap 0024.3): hidden links dropped, highlighted first. Duplicates share the key and so the curation; the first in TSK order wins.
        var arranged = CrossReferenceCuration.Arrange(rows, includeHidden);

        // Word links on revision levels (roadmap 0024.5): one query for the variants of the verses that have an ok word link.
        var variantWords = new Dictionary<int, List<NoteVariantWordRow>>();
        if (level > 0)
        {
            var linked = arranged
                .Where(r => CrossReferenceWordLink.Build(r)?.State == TskWordLinkStates.Ok)
                .Select(r => r.SourceHebRefId)
                .Distinct()
                .ToList();
            if (linked.Count > 0)
            {
                variantWords = (await _tsk.GetVariantWordsAsync(linked, level, token)).GroupBy(w => w.HebRefId).ToDictionary(g => g.Key, g => g.ToList());
            }
        }

        var verses = arranged
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
                        if (reference.Word is not null)
                        {
                            reference.Word.MappedWords = CrossReferenceWordLink.MapToVariant(reference.Word, variantWords.GetValueOrDefault(r.SourceHebRefId));
                        }

                        return reference;
                    })
                    .DistinctBy(r => (r.BibleBookId, r.Chapter, r.Verse, r.EndVerse))
                    .ToList(),
            })
            .ToList();

        return new GetChapterCrossReferencesResponse { BibleBookId = bibleBookId, Chapter = chapter, Verses = verses };
    }
}
