using UnshackledWord.Application.Features.StrongsStats;
using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Application.Tests.Features;

public class StrongsMatchKeysTests
{
    private static string[] Resolve(string input, params LexiconEntry[] entries)
    {
        StrongsNumber.TryNormalize(input, out var normalized, out _).ShouldBeTrue();
        return StrongsMatchKeys.Resolve(normalized, StrongsNumber.Parse(normalized).Value!.Value, entries);
    }

    [Fact]
    public void NumberWithoutLetter_CoversEveryVariant()
    {
        Resolve("G3004", new LexiconEntry { Number = 3004, Extra = "G" }, new LexiconEntry { Number = 3004, Extra = "H" })
            .ShouldBe(["G3004", "G3004G", "G3004H"]);
    }

    [Fact]
    public void LexiconEntryWithoutExtra_GivesTheNumberOnly()
    {
        Resolve("G1577", new LexiconEntry { Number = 1577 }).ShouldBe(["G1577"]);
    }

    [Fact]
    public void NoEntries_GivesTheNormalizedRequest()
    {
        Resolve("g26").ShouldBe(["G0026"]);
    }

    [Fact]
    public void LetteredNumber_MatchesOnlyItself()
    {
        Resolve("G3004G", new LexiconEntry { Number = 3004, Extra = "G" }).ShouldBe(["G3004G"]);
    }

    [Fact]
    public void InstanceSuffixIsDropped()
    {
        Resolve("H1254A_B", new LexiconEntry { Number = 1254, Extra = "A" }).ShouldBe(["H1254A"]);
    }
}
