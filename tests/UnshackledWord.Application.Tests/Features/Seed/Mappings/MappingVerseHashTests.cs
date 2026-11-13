using UnshackledWord.Application.Features.Seed.Mappings;

namespace UnshackledWord.Application.Tests.Features.Seed.Mappings;

public class MappingVerseHashTests
{
    private static readonly VerseEdge First = new(1, string.Empty, 1001001, 1, false, 0, 0);
    private static readonly VerseEdge Second = new(2, "Schaf", 1001001, 2, false, 0, 0);

    [Fact]
    public void Compute_ShouldNotDependOnTheOrderOfTheEdges()
    {
        MappingVerseHash.Compute([First, Second]).ShouldBe(MappingVerseHash.Compute([Second, First]));
    }

    [Fact]
    public void Compute_ShouldChange_WhenAnyEdgeFieldChanges()
    {
        var original = MappingVerseHash.Compute([First, Second]);

        MappingVerseHash.Compute([First, Second with { Part = "hirten" }]).ShouldNotBe(original);
        MappingVerseHash.Compute([First, Second with { SrcPos = 3 }]).ShouldNotBe(original);
        MappingVerseHash.Compute([First, Second with { Added = true }]).ShouldNotBe(original);
        MappingVerseHash.Compute([First, Second with { ParentPos = 1, ParentRef = 1001001 }]).ShouldNotBe(original);
        MappingVerseHash.Compute([First]).ShouldNotBe(original);
    }

    [Fact]
    public void Compute_ShouldBe64LowerCaseHexDigits()
    {
        var hash = MappingVerseHash.Compute([First]);

        hash.Length.ShouldBe(64);
        hash.ShouldBe(hash.ToLowerInvariant());
    }

    [Fact]
    public void Compute_ShouldMatchTheSqlFormula_ForAKnownLine()
    {
        // encode(sha256(convert_to('1||1001001|1|false|0|0', 'UTF8')), 'hex') in Postgres, as SeedVerseState computes it
        MappingVerseHash.Compute([First]).ShouldBe("d623e675b99b4bcd1b6a9266b5d9c455a5ee10ed85e2dd1e65875d4850faf6df");
    }
}
