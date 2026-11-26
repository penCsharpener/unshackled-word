using UnshackledWord.Application.Features.GrammarSearch;
using UnshackledWord.Domain.Models.Grammar;

namespace UnshackledWord.Application.Tests.Features.GrammarSearch;

public class GrammarFilterResolverTests
{
    private static readonly string[] Codes =
    [
        "N-NSF", "N-ASF", "N-GPM", "T-NSM", "V-AAI-3S", "V-APP-NSM", "V-2AAI-3S", "V-2AMN", "V-PEP-NSM", "V-FAI-3S", "V-FPI-3S", "V-2FPI-3S", "ADV", "PREP",
    ];

    private static readonly List<MorphologyCodeEntry> Entries = [.. Codes.Select(c => new MorphologyCodeEntry(c, GreekMorphologyDecoder.Decode(c)))];

    private static IReadOnlyList<string> Resolve(string query)
    {
        var slot = GrammarQueryParser.Parse(query).Slots.Single();
        return GrammarFilterResolver.Resolve(slot.Filters, Entries, GrammarVocabulary.LanguageGreek);
    }

    [Fact]
    public void SingleFilter_SelectsTheCodesWithThatKey() =>
        Resolve("Kasus:nom").ShouldBe(["N-NSF", "T-NSM", "V-APP-NSM", "V-PEP-NSM"]);

    [Fact]
    public void FiltersOfOneSlotAreAnded() =>
        Resolve("Tempus:aor Diathese:pass").ShouldBe(["V-APP-NSM"]);

    [Fact]
    public void CommaIsOr() =>
        Resolve("Kasus:gen,akk").ShouldBe(["N-ASF", "N-GPM"]);

    [Fact]
    public void Negation_KeepsCodesWithoutThatKey()
    {
        var codes = Resolve("Wortart:verb -Modus:partizip");

        codes.ShouldNotContain("V-APP-NSM");
        codes.ShouldNotContain("V-PEP-NSM");
        codes.ShouldContain("V-AAI-3S");
        codes.ShouldNotContain("N-NSF"); // not a verb

        Resolve("-Modus:partizip").ShouldContain("N-NSF"); // no mood at all counts as "not a participle"
    }

    [Fact]
    public void ExactFutureDoesNotIncludeFuturePerfect() =>
        Resolve("Tempus:futur").ShouldBe(["V-FAI-3S", "V-FPI-3S", "V-2FPI-3S"]);

    [Fact]
    public void CodeWildcard_MatchesPatternCaseInsensitive()
    {
        Resolve("Code:V-2A*").ShouldBe(["V-2AAI-3S", "V-2AMN"]);
        Resolve("Code:n-nsf").ShouldBe(["N-NSF"]);
        Resolve("Code:*-NSM").ShouldBe(["T-NSM", "V-APP-NSM", "V-PEP-NSM"]);
    }

    [Fact]
    public void Language_GreekMatchesEverything_HebrewNothing()
    {
        Resolve("Sprache:griechisch").Count.ShouldBe(Codes.Length);
        Resolve("Sprache:hebräisch").ShouldBeEmpty();
    }

    [Fact]
    public void KeyThatDoesNotApply_MatchesNothing() =>
        Resolve("Person:1").ShouldBeEmpty();

    [Theory]
    [InlineData("V-2A*", "V-2AAI-3S", true)]
    [InlineData("V-2A*", "V-AAI-3S", false)]
    [InlineData("N-*F", "N-NSF", true)]
    [InlineData("N-*F", "N-NSM", false)]
    [InlineData("*", "ADV", true)]
    [InlineData("A*V", "ADV", true)]
    [InlineData("AD", "ADV", false)]
    public void Wildcard(string pattern, string code, bool expected) =>
        GrammarFilterResolver.Wildcard(code, pattern).ShouldBe(expected);
}
