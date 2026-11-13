using UnshackledWord.Application.Features.Seed.Anchoring;

namespace UnshackledWord.Application.Features.Seed.Mappings;

/// <summary>A mapping seed line and where its words ended up on this installation.</summary>
/// <param name="Class">The weakest anchoring of the German word, the source word and the parent; <see cref="AnchorClass.Orphaned"/> when any of them has no counterpart.</param>
/// <param name="Reason">Empty unless orphaned: <c>German</c>, <c>Source</c>, <c>Parent</c> or <c>Duplicate</c>.</param>
public sealed record PlacedMapping(
    MappingSeedLine Line,
    AnchorToken? German,
    AnchorToken? Source,
    AnchorToken? Parent,
    AnchorClass Class,
    string Reason);

public sealed class MappingPlacement
{
    public MappingPlacement(IReadOnlyList<PlacedMapping> lines)
    {
        Lines = lines;
    }

    public IReadOnlyList<PlacedMapping> Lines { get; }

    /// <summary>Lines to insert: exact, shifted and modified.</summary>
    public IEnumerable<PlacedMapping> Placed => Lines.Where(l => l.Class != AnchorClass.Orphaned);

    /// <summary>Lines the report lists: everything that is not an exact match.</summary>
    public IEnumerable<PlacedMapping> Reportable => Lines.Where(l => l.Class != AnchorClass.Exact);

    public int Exact => Lines.Count(l => l.Class == AnchorClass.Exact);
    public int Shifted => Lines.Count(l => l.Class == AnchorClass.Shifted);
    public int Modified => Lines.Count(l => l.Class == AnchorClass.Modified);
    public int Orphaned => Lines.Count(l => l.Class == AnchorClass.Orphaned);
}

/// <summary>
/// Places mapping seed lines onto the German and STEP words of this installation (roadmap 0010.9). An edge is only imported when its
/// German word, its source word and its parent word are all found; two lines that land on the same pair of words keep the first.
/// </summary>
public static class MappingPlacer
{
    public static MappingPlacement Place(IReadOnlyList<MappingSeedLine> lines, GermanWordResolver german, SourceWordResolver source)
    {
        var placed = new List<PlacedMapping>(lines.Count);
        var taken = new HashSet<(int, int, int, int, string)>();

        foreach (var line in lines)
        {
            var result = PlaceLine(line, german, source);

            if (result.Class != AnchorClass.Orphaned)
            {
                var key = (result.German!.Value.Ref, result.German.Value.Pos, result.Source?.Ref ?? 0, result.Source?.Pos ?? 0, result.Source?.Text ?? string.Empty);

                if (!taken.Add(key))
                {
                    result = result with { Class = AnchorClass.Orphaned, Reason = "Duplicate" };
                }
            }

            placed.Add(result);
        }

        return new MappingPlacement(placed);
    }

    private static PlacedMapping PlaceLine(MappingSeedLine line, GermanWordResolver german, SourceWordResolver source)
    {
        var germanMatch = german.Resolve(line.Ref, line.Pos, line.Form);

        if (germanMatch.Class == AnchorClass.Orphaned)
        {
            return new PlacedMapping(line, null, null, null, AnchorClass.Orphaned, "German");
        }

        TokenMatch? sourceMatch = line.Source is { } key ? source.Resolve(key) : null;

        if (sourceMatch is { Class: AnchorClass.Orphaned })
        {
            return new PlacedMapping(line, germanMatch.New, null, null, AnchorClass.Orphaned, "Source");
        }

        TokenMatch? parentMatch = line.HasParent ? german.Resolve(line.EffectiveParentRef, line.ParentPos!.Value, line.ParentForm) : null;

        if (parentMatch is { Class: AnchorClass.Orphaned })
        {
            return new PlacedMapping(line, germanMatch.New, sourceMatch?.New, null, AnchorClass.Orphaned, "Parent");
        }

        var worst = new[] { germanMatch.Class, sourceMatch?.Class ?? AnchorClass.Exact, parentMatch?.Class ?? AnchorClass.Exact }.Max();

        return new PlacedMapping(line, germanMatch.New, sourceMatch?.New, parentMatch?.New, worst, string.Empty);
    }
}
