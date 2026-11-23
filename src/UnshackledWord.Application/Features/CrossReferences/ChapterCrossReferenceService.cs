using UnshackledWord.Application.Features.Notes;
using UnshackledWord.Application.Features.Revision;
using UnshackledWord.Application.Features.TskCuration;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetChapterCrossReferences;
using UnshackledWord.Domain.WebApi.Tsk;

namespace UnshackledWord.Application.Features.CrossReferences;

public sealed class ChapterCrossReferenceService : IChapterCrossReferenceService
{
    /// <summary>Safety cap: the busiest chapter (Psalm 119) has about 1,700 TSK rows.</summary>
    public const int MaxRows = 5000;

    private readonly ITskRepository _tsk;
    private readonly ITskCurationProposalRepository? _proposals;

    /// <param name="proposals">Null: no <c>pendingProposal</c> (tests of the reader list).</param>
    public ChapterCrossReferenceService(ITskRepository tsk, ITskCurationProposalRepository? proposals = null)
    {
        _tsk = tsk;
        _proposals = proposals;
    }

    public async Task<GetChapterCrossReferencesResponse> GetAsync(int bibleBookId, int chapter, bool includeHidden = false, int level = 0, int? proposalsOfUserId = null,
        CancellationToken token = default)
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

        // The caller's own pending proposals (roadmap 0024.6), by key; proposed new links that are no link yet are appended to their verse.
        var pending = proposalsOfUserId is { } userId && _proposals is not null
            ? (await _proposals.GetPendingOfUserAsync(userId, from, to, token)).ToDictionary(p => p.Key)
            : new Dictionary<TskLinkKey, TskProposalRecord>();
        var listed = new HashSet<TskLinkKey>();

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
                        var key = new TskLinkKey(r.SourceHebRefId, r.TargetStartHebRefId, r.TargetEndHebRefId);
                        listed.Add(key);
                        if (pending.TryGetValue(key, out var proposal))
                        {
                            reference.PendingProposal = TskProposalMapper.ToPending(proposal);
                        }

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

        var proposedLinks = pending.Values
            .Where(p => p is { Delete: false, State.Origin: TskOrigins.Added } && !listed.Contains(p.Key))
            .OrderBy(p => p.CreatedAt)
            .ThenBy(p => p.Id);
        foreach (var proposal in proposedLinks)
        {
            var verseNumber = proposal.Key.SourceHebRefId % 1000;
            var verse = verses.FirstOrDefault(v => v.Verse == verseNumber);
            if (verse is null)
            {
                verse = new VerseCrossReferencesResponse { Verse = verseNumber };
                verses.Add(verse);
            }

            var reference = CrossReferenceMapper.Build(new TskCrossReference
            {
                SourceHebRefId = proposal.Key.SourceHebRefId,
                Scope = string.Empty,
                TargetStartHebRefId = proposal.Key.TargetStartHebRefId,
                TargetEndHebRefId = proposal.Key.TargetEndHebRefId,
                Origin = TskOrigins.Added,
            });
            reference.PendingProposal = TskProposalMapper.ToPending(proposal);
            verse.References.Add(reference);
        }

        verses.Sort((a, b) => a.Verse.CompareTo(b.Verse));

        return new GetChapterCrossReferencesResponse { BibleBookId = bibleBookId, Chapter = chapter, Verses = verses };
    }
}
