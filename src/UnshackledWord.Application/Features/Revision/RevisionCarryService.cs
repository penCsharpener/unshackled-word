using UnshackledWord.Application.Repositories;

namespace UnshackledWord.Application.Features.Revision;

public interface IRevisionCarryService
{
    /// <summary>
    /// Carries the mapping of the Elb1871 verse over to the new tokens (roadmap 0011 "Carry-over"). Reads the existing mapping through
    /// <see cref="IVerseMappingRepository"/>; rows that point outside the verse (seed over-assignment) are ignored like the mapping read does.
    /// </summary>
    Task<RevisionCarryResult> CarryFromBaseAsync(int bibleBookId, int chapter, int verse, IReadOnlyList<RevisionBaseWordRow> baseWords, IReadOnlyList<RevisionToken> tokens, CancellationToken token = default);

    /// <summary>
    /// Same as <see cref="CarryFromBaseAsync"/> but the parent is an existing variant version: its words and its mapping rows
    /// (manual links included) are carried; base position and change are composed through the parent.
    /// </summary>
    Task<RevisionCarryResult> CarryFromVariantAsync(int parentRevisionVerseId, IReadOnlyList<RevisionToken> tokens, CancellationToken token = default);
}

public sealed class RevisionCarryService : IRevisionCarryService
{
    private readonly IVerseMappingRepository _verseMapping;
    private readonly IRevisionRepository _revisions;

    public RevisionCarryService(IVerseMappingRepository verseMapping, IRevisionRepository revisions)
    {
        _verseMapping = verseMapping;
        _revisions = revisions;
    }

    public async Task<RevisionCarryResult> CarryFromVariantAsync(int parentRevisionVerseId, IReadOnlyList<RevisionToken> tokens, CancellationToken token = default)
    {
        var words = await _revisions.GetWordsAsync(parentRevisionVerseId, token);
        var mapping = await _revisions.GetMappingAsync(parentRevisionVerseId, token);
        var positionById = words.ToDictionary(w => w.Id, w => w.PositionInVerse);

        var parentWords = words
            .Select(w => new RevisionCarryParentWord(w.PositionInVerse, w.PlainWord, w.BasePositionInVerse, w.Change))
            .ToList();

        var parentRows = mapping
            .Where(r => positionById.ContainsKey(r.RevisionWordId))
            .Select(r => new RevisionCarryParentRow(
                positionById[r.RevisionWordId],
                r.IsAddedWord ? null : r.StepWordId,
                r.IsAddedWord,
                r.IsAddedWord && r.ParentRevisionWordId is { } parent && positionById.TryGetValue(parent, out var parentPosition) ? parentPosition : null,
                r.GermanWordPart))
            .ToList();

        return RevisionCarry.Carry(parentWords, parentRows, tokens.Select(t => new RevisionCarryToken(t.Position, t.PlainWord)).ToList());
    }

    public async Task<RevisionCarryResult> CarryFromBaseAsync(int bibleBookId, int chapter, int verse, IReadOnlyList<RevisionBaseWordRow> baseWords, IReadOnlyList<RevisionToken> tokens, CancellationToken token = default)
    {
        var hebrew = RevisionSql.IsHebrew(bibleBookId);
        var german = await _verseMapping.GetGermanWordsAsync(bibleBookId, chapter, verse, token);
        var original = await _verseMapping.GetOriginalWordsAsync(hebrew, bibleBookId, chapter, verse, token);
        var mapping = await _verseMapping.GetMappingRowsAsync(hebrew, bibleBookId, chapter, verse, token);

        var positionById = german.GroupBy(w => w.Id).ToDictionary(g => g.Key, g => g.First().PositionInVerse);
        var originalIds = original.Select(w => w.Id).ToHashSet();

        var parentWords = baseWords
            .Select(w => new RevisionCarryParentWord(w.PositionInVerse, w.PlainWord, w.PositionInVerse, RevisionCarry.Same))
            .ToList();

        var parentRows = mapping
            .Where(r => positionById.ContainsKey(r.ElbWordId))
            .Where(r => r.IsAddedWord || (r.StepWordId is { } step && originalIds.Contains(step)))
            .Select(r => new RevisionCarryParentRow(
                positionById[r.ElbWordId],
                r.IsAddedWord ? null : r.StepWordId,
                r.IsAddedWord,
                r.IsAddedWord && r.ParentElbWordId is { } parent && positionById.TryGetValue(parent, out var parentPosition) ? parentPosition : null,
                r.GermanWordPart))
            .ToList();

        return RevisionCarry.Carry(parentWords, parentRows, tokens.Select(t => new RevisionCarryToken(t.Position, t.PlainWord)).ToList());
    }
}
