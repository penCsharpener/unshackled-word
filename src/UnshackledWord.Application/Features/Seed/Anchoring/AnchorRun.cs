namespace UnshackledWord.Application.Features.Seed.Anchoring;

/// <summary>Counts of one chapter in an <see cref="AnchorRun"/>.</summary>
public sealed record ChapterReport(int Chapter, int Exact, int Shifted, int Modified, int Orphaned, int InsertedNew, bool Complete);

/// <summary>
/// Alignment of a whole book or Bible: every chapter of the old side against the same chapter of the new side. A chapter that exists
/// on one side only is orphaned (old) or inserted (new) as a whole.
/// </summary>
public sealed class AnchorRun
{
    private AnchorRun(IReadOnlyList<ChapterAlignment> chapters)
    {
        Chapters = chapters;
    }

    public IReadOnlyList<ChapterAlignment> Chapters { get; }

    public int Exact => Chapters.Sum(c => c.Exact);
    public int Shifted => Chapters.Sum(c => c.Shifted);
    public int Modified => Chapters.Sum(c => c.Modified);
    public int Orphaned => Chapters.Sum(c => c.Orphaned);
    public int InsertedNew => Chapters.Sum(c => c.UnmatchedNew.Count);

    /// <summary>One line per chapter (ascending), the input of the "chapters with orphans" report.</summary>
    public IReadOnlyList<ChapterReport> ChapterReports => Chapters
        .Select(c => new ChapterReport(c.Chapter, c.Exact, c.Shifted, c.Modified, c.Orphaned, c.UnmatchedNew.Count, c.Complete))
        .ToList();

    /// <summary>The chapters that lost at least one old token or could not be searched completely.</summary>
    public IReadOnlyList<ChapterReport> ChaptersWithOrphans => ChapterReports.Where(c => c.Orphaned > 0 || !c.Complete).ToList();

    /// <summary>Every old token that has no counterpart, in chapter and reading order.</summary>
    public IEnumerable<AnchorToken> OrphanedTokens => Chapters.SelectMany(c => c.OrphanedTokens);

    /// <summary>
    /// Both token lists may contain several chapters in any order; within a chapter the order of the list is the reading order, so
    /// callers pass tokens sorted by reference and position.
    /// </summary>
    public static AnchorRun Align(IEnumerable<AnchorToken> oldTokens, IEnumerable<AnchorToken> newTokens, int maxEdits = SequenceAligner.DefaultMaxEdits)
    {
        var oldByChapter = oldTokens.GroupBy(t => t.Chapter).ToDictionary(g => g.Key, g => g.ToList());
        var newByChapter = newTokens.GroupBy(t => t.Chapter).ToDictionary(g => g.Key, g => g.ToList());
        var chapters = new List<ChapterAlignment>();

        foreach (var chapter in oldByChapter.Keys.Union(newByChapter.Keys).Order())
        {
            var oldList = oldByChapter.GetValueOrDefault(chapter) ?? [];
            var newList = newByChapter.GetValueOrDefault(chapter) ?? [];
            chapters.Add(ChapterAligner.Align(chapter, oldList, newList, maxEdits));
        }

        return new AnchorRun(chapters);
    }
}
