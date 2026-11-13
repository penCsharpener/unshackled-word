namespace UnshackledWord.Application.Features.Seed.Anchoring;

/// <summary>
/// Finds the German word of this installation that a seed key (<c>Ref</c>, <c>Pos</c>, <c>Form</c>) was exported from. The old side is the
/// seed's token manifest (one token per German word, as exported with the morphology seed); chapters whose tokens are identical to the
/// freshly parsed words are taken as they are, every other chapter is aligned with <see cref="ChapterAligner"/>. A key that is not in
/// the manifest is only accepted when the word at that exact place has the same form. Nothing is guessed beyond that.
/// </summary>
public sealed class GermanWordResolver
{
    private readonly Dictionary<(int Ref, int Pos), AnchorToken> _newByKey;
    private readonly Dictionary<(int Ref, int Pos), TokenMatch> _alignedByOldKey;

    private GermanWordResolver(
        Dictionary<(int Ref, int Pos), AnchorToken> newByKey,
        Dictionary<(int Ref, int Pos), TokenMatch> alignedByOldKey,
        IReadOnlyList<int> chaptersAligned)
    {
        _newByKey = newByKey;
        _alignedByOldKey = alignedByOldKey;
        ChaptersAligned = chaptersAligned;
    }

    /// <summary>Chapters (<c>BBCCC</c>) that needed an alignment because their words differ from the seed's manifest.</summary>
    public IReadOnlyList<int> ChaptersAligned { get; }

    /// <param name="manifestTokens">All German words of the exporting installation (the morphology seed's lines); may be empty.</param>
    /// <param name="newTokens">The German words of this installation.</param>
    public static GermanWordResolver Create(IReadOnlyList<AnchorToken> manifestTokens, IReadOnlyList<AnchorToken> newTokens)
    {
        var newByKey = newTokens.ToDictionary(t => (t.Ref, t.Pos));
        var oldByChapter = manifestTokens.GroupBy(t => t.Chapter).ToDictionary(g => g.Key, g => g.ToList());
        var newByChapter = newTokens.GroupBy(t => t.Chapter).ToDictionary(g => g.Key, g => g.ToList());
        var aligned = new Dictionary<(int Ref, int Pos), TokenMatch>();
        var chaptersAligned = new List<int>();

        foreach (var chapter in oldByChapter.Keys.Order())
        {
            var oldList = oldByChapter[chapter];
            var newList = newByChapter.GetValueOrDefault(chapter) ?? [];

            if (AreIdentical(oldList, newList))
            {
                continue;
            }

            chaptersAligned.Add(chapter);

            foreach (var match in ChapterAligner.Align(chapter, oldList, newList).Matches)
            {
                aligned[(match.Old.Ref, match.Old.Pos)] = match;
            }
        }

        return new GermanWordResolver(newByKey, aligned, chaptersAligned);
    }

    public TokenMatch Resolve(int reference, int position, string? form)
    {
        var old = AnchorToken.ForWord(reference, position, form);

        if (_alignedByOldKey.TryGetValue((reference, position), out var match) && match.Old.Text == old.Text)
        {
            return match;
        }

        if (_newByKey.TryGetValue((reference, position), out var same) && same.Text == old.Text)
        {
            return new TokenMatch(old, same, AnchorClass.Exact);
        }

        return new TokenMatch(old, null, AnchorClass.Orphaned);
    }

    private static bool AreIdentical(List<AnchorToken> oldList, List<AnchorToken> newList)
    {
        return oldList.Count == newList.Count && oldList.SequenceEqual(newList);
    }
}
