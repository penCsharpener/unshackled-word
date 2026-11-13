namespace UnshackledWord.Application.Features.Seed.Anchoring;

/// <summary>What became of one token of the old side.</summary>
/// <param name="Old">The token as the seed (old installation) knows it.</param>
/// <param name="New">Its counterpart on the new side; null when <paramref name="Class"/> is <see cref="AnchorClass.Orphaned"/>.</param>
public readonly record struct TokenMatch(AnchorToken Old, AnchorToken? New, AnchorClass Class);

/// <summary>Result of aligning the old tokens of one chapter onto its new tokens.</summary>
public sealed class ChapterAlignment
{
    public ChapterAlignment(int chapter, IReadOnlyList<TokenMatch> matches, IReadOnlyList<AnchorToken> unmatchedNew, bool complete)
    {
        Chapter = chapter;
        Matches = matches;
        UnmatchedNew = unmatchedNew;
        Complete = complete;
    }

    /// <summary>Book and chapter (<c>Ref / 1000</c>).</summary>
    public int Chapter { get; }

    /// <summary>One entry per old token, in the order of the old tokens.</summary>
    public IReadOnlyList<TokenMatch> Matches { get; }

    /// <summary>New tokens no old token was placed on (inserted words); informational, nothing is imported for them.</summary>
    public IReadOnlyList<AnchorToken> UnmatchedNew { get; }

    /// <summary>False when the chapter differed by more edits than the aligner searches (see <see cref="SequenceAligner.DefaultMaxEdits"/>).</summary>
    public bool Complete { get; }

    public int Exact => Count(AnchorClass.Exact);
    public int Shifted => Count(AnchorClass.Shifted);
    public int Modified => Count(AnchorClass.Modified);
    public int Orphaned => Count(AnchorClass.Orphaned);

    public IEnumerable<AnchorToken> OrphanedTokens => Matches.Where(m => m.Class == AnchorClass.Orphaned).Select(m => m.Old);

    public int Count(AnchorClass anchorClass) => Matches.Count(m => m.Class == anchorClass);
}

/// <summary>
/// Places the tokens of one chapter of an old installation onto the tokens of a new one. Works on German words (text = normalised
/// word) and on STEP words (text = <see cref="SourceKey.SrcHash"/>) alike.
/// </summary>
public static class ChapterAligner
{
    /// <summary>
    /// Both lists must hold the tokens of the same chapter in reading order. Equal runs (Myers diff) become
    /// <see cref="AnchorClass.Exact"/> (same reference and position) or <see cref="AnchorClass.Shifted"/>; a single old token and a single
    /// new token between two equal runs (or a run and the chapter boundary) become <see cref="AnchorClass.Modified"/>; everything else is
    /// <see cref="AnchorClass.Orphaned"/>. Nothing is guessed beyond that.
    /// </summary>
    public static ChapterAlignment Align(int chapter, IReadOnlyList<AnchorToken> oldTokens, IReadOnlyList<AnchorToken> newTokens, int maxEdits = SequenceAligner.DefaultMaxEdits)
    {
        if (IsIdentical(oldTokens, newTokens))
        {
            var same = oldTokens.Select(t => new TokenMatch(t, t, AnchorClass.Exact)).ToList();

            return new ChapterAlignment(chapter, same, [], true);
        }

        var runs = SequenceAligner.Align(oldTokens.Select(t => t.Text).ToList(), newTokens.Select(t => t.Text).ToList(), maxEdits);
        var newIndexOfOld = new int[oldTokens.Count];
        Array.Fill(newIndexOfOld, -1);
        var classOfOld = new AnchorClass[oldTokens.Count];
        Array.Fill(classOfOld, AnchorClass.Orphaned);
        var usedNew = new bool[newTokens.Count];

        foreach (var (oldIndex, newIndex) in runs.Pairs)
        {
            var sameKey = oldTokens[oldIndex].Ref == newTokens[newIndex].Ref && oldTokens[oldIndex].Pos == newTokens[newIndex].Pos;
            newIndexOfOld[oldIndex] = newIndex;
            classOfOld[oldIndex] = sameKey ? AnchorClass.Exact : AnchorClass.Shifted;
            usedNew[newIndex] = true;
        }

        MarkModified(runs, oldTokens.Count, newTokens.Count, newIndexOfOld, classOfOld, usedNew);

        var matches = new List<TokenMatch>(oldTokens.Count);

        for (var i = 0; i < oldTokens.Count; i++)
        {
            AnchorToken? counterpart = newIndexOfOld[i] >= 0 ? newTokens[newIndexOfOld[i]] : null;
            matches.Add(new TokenMatch(oldTokens[i], counterpart, classOfOld[i]));
        }

        var unmatchedNew = newTokens.Where((_, index) => !usedNew[index]).ToList();

        return new ChapterAlignment(chapter, matches, unmatchedNew, runs.Complete);
    }

    private static void MarkModified(AlignedRuns runs, int oldCount, int newCount, int[] newIndexOfOld, AnchorClass[] classOfOld, bool[] usedNew)
    {
        var previous = (OldIndex: -1, NewIndex: -1);

        for (var i = 0; i <= runs.Pairs.Count; i++)
        {
            var next = i < runs.Pairs.Count ? runs.Pairs[i] : (OldIndex: oldCount, NewIndex: newCount);
            var oldGap = next.OldIndex - previous.OldIndex - 1;
            var newGap = next.NewIndex - previous.NewIndex - 1;

            // A lone 1:1 replace is only evidence of a typo fix when at least one equal run exists in the chapter to hold on to.
            if (oldGap == 1 && newGap == 1 && runs.Pairs.Count > 0)
            {
                var oldIndex = previous.OldIndex + 1;
                var newIndex = previous.NewIndex + 1;
                newIndexOfOld[oldIndex] = newIndex;
                classOfOld[oldIndex] = AnchorClass.Modified;
                usedNew[newIndex] = true;
            }

            previous = next;
        }
    }

    private static bool IsIdentical(IReadOnlyList<AnchorToken> oldTokens, IReadOnlyList<AnchorToken> newTokens)
    {
        if (oldTokens.Count != newTokens.Count)
        {
            return false;
        }

        for (var i = 0; i < oldTokens.Count; i++)
        {
            if (oldTokens[i] != newTokens[i])
            {
                return false;
            }
        }

        return true;
    }
}
