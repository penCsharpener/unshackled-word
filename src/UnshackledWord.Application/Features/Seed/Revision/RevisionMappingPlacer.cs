using UnshackledWord.Application.Features.Revision;
using UnshackledWord.Application.Features.Seed.Anchoring;

namespace UnshackledWord.Application.Features.Seed.Revision;

/// <summary>A revision mapping seed line and where its words ended up: German word position in the variant, STEP word of this installation, parent position.</summary>
public sealed record PlacedRevisionMapping(
    RevisionMappingSeedLine Line,
    int? Pos,
    AnchorToken? Source,
    int? ParentPos,
    AnchorClass Class,
    string Reason);

public sealed class RevisionMappingPlacement
{
    public RevisionMappingPlacement(IReadOnlyList<PlacedRevisionMapping> lines)
    {
        Lines = lines;
    }

    public IReadOnlyList<PlacedRevisionMapping> Lines { get; }

    public IEnumerable<PlacedRevisionMapping> Placed => Lines.Where(l => l.Class != AnchorClass.Orphaned);
    public IEnumerable<PlacedRevisionMapping> Reportable => Lines.Where(l => l.Class != AnchorClass.Exact);
    public int Exact => Lines.Count(l => l.Class == AnchorClass.Exact);
    public int Shifted => Lines.Count(l => l.Class == AnchorClass.Shifted);
    public int Modified => Lines.Count(l => l.Class == AnchorClass.Modified);
    public int Orphaned => Lines.Count(l => l.Class == AnchorClass.Orphaned);
}

/// <summary>
/// Places the mapping lines of one variant onto its words and the STEP words of this installation (roadmap 0011.12). The German side
/// needs no alignment: the seed carries the variant's full text, so a word is found at its position when its form matches (exact),
/// else at the only position with that form (shifted), else the line is an orphan. The original side uses the 0010.9 rules through
/// <see cref="SourceWordResolver"/> (exact, n-th occurrence of the hash, aligner, else orphan). Never guesses.
/// </summary>
public static class RevisionMappingPlacer
{
    public static RevisionMappingPlacement Place(IReadOnlyList<RevisionToken> words, IReadOnlyList<RevisionMappingSeedLine> lines, SourceWordResolver source)
    {
        var placed = new List<PlacedRevisionMapping>(lines.Count);
        var taken = new HashSet<(int, int, int, string)>();

        foreach (var line in lines)
        {
            var result = PlaceLine(words, line, source);

            if (result.Class != AnchorClass.Orphaned)
            {
                var key = (result.Pos!.Value, result.Source?.Ref ?? 0, result.Source?.Pos ?? 0, result.Source?.Text ?? string.Empty);

                if (!taken.Add(key))
                {
                    result = result with { Class = AnchorClass.Orphaned, Reason = "Duplicate" };
                }
            }

            placed.Add(result);
        }

        return new RevisionMappingPlacement(placed);
    }

    private static PlacedRevisionMapping PlaceLine(IReadOnlyList<RevisionToken> words, RevisionMappingSeedLine line, SourceWordResolver source)
    {
        var (pos, posClass) = FindWord(words, line.Pos, line.Form);

        if (pos is null)
        {
            return new PlacedRevisionMapping(line, null, null, null, AnchorClass.Orphaned, "German");
        }

        if (!line.HasSource && !line.Added)
        {
            return new PlacedRevisionMapping(line, pos, null, null, AnchorClass.Orphaned, "Source");
        }

        TokenMatch? sourceMatch = line.Source is { } key ? source.Resolve(key) : null;

        if (sourceMatch is { Class: AnchorClass.Orphaned })
        {
            return new PlacedRevisionMapping(line, pos, null, null, AnchorClass.Orphaned, "Source");
        }

        int? parentPos = null;
        var parentClass = AnchorClass.Exact;

        if (line.HasParent)
        {
            (parentPos, parentClass) = FindWord(words, line.ParentPos!.Value, line.ParentForm);

            if (parentPos is null)
            {
                return new PlacedRevisionMapping(line, pos, sourceMatch?.New, null, AnchorClass.Orphaned, "Parent");
            }
        }

        var worst = new[] { posClass, sourceMatch?.Class ?? AnchorClass.Exact, parentClass }.Max();

        return new PlacedRevisionMapping(line, pos, sourceMatch?.New, parentPos, worst, string.Empty);
    }

    private static (int? Pos, AnchorClass Class) FindWord(IReadOnlyList<RevisionToken> words, int position, string form)
    {
        var wanted = form ?? string.Empty;

        if (words.Any(w => w.Position == position && (w.PlainWord ?? string.Empty) == wanted))
        {
            return (position, AnchorClass.Exact);
        }

        var candidates = words.Where(w => (w.PlainWord ?? string.Empty) == wanted).ToList();

        return candidates.Count == 1 ? (candidates[0].Position, AnchorClass.Shifted) : (null, AnchorClass.Orphaned);
    }
}
