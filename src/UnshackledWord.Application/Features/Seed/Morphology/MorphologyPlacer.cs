using UnshackledWord.Application.Features.Seed.Anchoring;

namespace UnshackledWord.Application.Features.Seed.Morphology;

/// <summary>A seed line with data and where it ended up on the new installation.</summary>
/// <param name="Line">The seed line as exported.</param>
/// <param name="New">The word it was placed on; null when <paramref name="Class"/> is <see cref="AnchorClass.Orphaned"/>.</param>
public sealed record PlacedLine(MorphologySeedLine Line, AnchorToken? New, AnchorClass Class);

/// <summary>Result of placing the seed lines of one or more chapters; only lines that carry morphology are listed.</summary>
public sealed class MorphologyPlacement
{
    public MorphologyPlacement(IReadOnlyList<PlacedLine> lines, IReadOnlyList<int> chaptersAligned)
    {
        Lines = lines;
        ChaptersAligned = chaptersAligned;
    }

    public IReadOnlyList<PlacedLine> Lines { get; }

    /// <summary>Chapters (<c>BBCCC</c>) that needed an alignment because the fingerprint (or a word) did not match.</summary>
    public IReadOnlyList<int> ChaptersAligned { get; }

    /// <summary>Lines to insert: exact, shifted and modified.</summary>
    public IEnumerable<PlacedLine> Placed => Lines.Where(l => l.Class != AnchorClass.Orphaned);

    /// <summary>Lines the report lists: everything that is not an exact match.</summary>
    public IEnumerable<PlacedLine> Reportable => Lines.Where(l => l.Class != AnchorClass.Exact);

    public int Exact => Lines.Count(l => l.Class == AnchorClass.Exact);
    public int Shifted => Lines.Count(l => l.Class == AnchorClass.Shifted);
    public int Modified => Lines.Count(l => l.Class == AnchorClass.Modified);
    public int Orphaned => Lines.Count(l => l.Class == AnchorClass.Orphaned);
}

/// <summary>
/// Places morphology seed lines onto the German words of this installation (roadmap 0010.4). A chapter whose fingerprint equals the
/// freshly parsed words is taken as it is; any other chapter is aligned with <see cref="ChapterAligner"/>.
/// </summary>
public static class MorphologyPlacer
{
    /// <param name="seedLines">All lines of the seed that belong together (a book), words without morphology included.</param>
    /// <param name="newTokens">The words of this installation for the same chapters, in reading order.</param>
    /// <param name="seedFingerprints">Fingerprint per chapter (<c>BBCCC</c>) the exporter stored; a chapter without an entry is aligned.</param>
    public static MorphologyPlacement Place(
        IReadOnlyList<MorphologySeedLine> seedLines,
        IReadOnlyList<AnchorToken> newTokens,
        IReadOnlyDictionary<int, string> seedFingerprints)
    {
        var seedByChapter = seedLines.GroupBy(l => AnchorToken.ChapterOf(l.Ref)).ToDictionary(g => g.Key, g => g.ToList());
        var newByChapter = newTokens.GroupBy(t => t.Chapter).ToDictionary(g => g.Key, g => g.ToList());
        var placedLines = new List<PlacedLine>();
        var aligned = new List<int>();

        foreach (var chapter in seedByChapter.Keys.Order())
        {
            var lines = seedByChapter[chapter];
            var tokens = newByChapter.GetValueOrDefault(chapter) ?? [];

            if (TryTakeAsIs(chapter, lines, tokens, seedFingerprints, placedLines))
            {
                continue;
            }

            aligned.Add(chapter);
            Align(chapter, lines, tokens, placedLines);
        }

        return new MorphologyPlacement(placedLines, aligned);
    }

    private static bool TryTakeAsIs(
        int chapter,
        List<MorphologySeedLine> lines,
        List<AnchorToken> tokens,
        IReadOnlyDictionary<int, string> seedFingerprints,
        List<PlacedLine> placedLines)
    {
        if (!seedFingerprints.TryGetValue(chapter, out var seedFingerprint)
            || seedFingerprint != ChapterFingerprint.Compute(tokens)
            || lines.Count != tokens.Count)
        {
            return false;
        }

        // The fingerprint says the chapter is unchanged; still make sure every line really sits on a word with the same form.
        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].ToToken() != tokens[i])
            {
                return false;
            }
        }

        placedLines.AddRange(lines.Where(l => l.HasMorphology).Select(l => new PlacedLine(l, l.ToToken(), AnchorClass.Exact)));

        return true;
    }

    private static void Align(int chapter, List<MorphologySeedLine> lines, List<AnchorToken> tokens, List<PlacedLine> placedLines)
    {
        var alignment = ChapterAligner.Align(chapter, lines.Select(l => l.ToToken()).ToList(), tokens);

        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].HasMorphology)
            {
                placedLines.Add(new PlacedLine(lines[i], alignment.Matches[i].New, alignment.Matches[i].Class));
            }
        }
    }
}
