using Shouldly;
using UnshackledWord.Domain.Models.Dbo.Step;
using UnshackledWord.Tooling.SeedDb.Services.StepBible;

namespace UnshackledWord.Tooling.SeedDb.TestRunner.Services.StepBible;

public class StrongsRegexParserTests
{
    [Theory]
    [InlineData("G0894", 1)]
    [InlineData("G0894 =", 1)]
    [InlineData("G0897 = the Greek of", 1)]
    [InlineData("H1168A", 1)]
    [InlineData("H0894", 1)]
    [InlineData("G0906G", 1)]
    [InlineData("H9001/{H4191}\\H9016\\ \\H9018", 4)]
    [InlineData("H5127_B", 1)]
    [InlineData("H9001/{H0935P}/H9038", 3)]
    [InlineData("H9003/{H6450}+", 2)]
    [InlineData("H3027G_B", 1)]
    [InlineData("H9002/{H1031}+\\H9014", 3)]
    public void ParserTests(string strongsText, int expectedCount)
    {
        var results = StrongsRegexParser.Parse(strongsText).ToList();

        results.Count.ShouldBe(expectedCount);
    }

    [Fact]
    public void ParserExtracts_DisambiguatedExtra()
    {
        var results = StrongsRegexParser.Parse("G0897 = the Greek of").ToList();

        results[0].LanguageId.ShouldBe(StrongsLanguage.Greek);
        results[0].Number.ShouldBe(897);
        results[0].DisambiguatedExtra.ShouldBe("the Greek of");
        results[0].Extra.ShouldBeNull();
        results[0].UnderscoredExtra.ShouldBeNull();
        results[0].IsRoot.ShouldBeFalse();
        results[0].CoversNextWord.ShouldBeFalse();
    }

    [Fact]
    public void ParserExtracts_ExtraAndUnderscoredExtra()
    {
        var results = StrongsRegexParser.Parse("H3027G_B").ToList();

        results[0].LanguageId.ShouldBe(StrongsLanguage.Hebrew);
        results[0].Number.ShouldBe(3027);
        results[0].DisambiguatedExtra.ShouldBeNull();
        results[0].Extra.ShouldBe("G");
        results[0].UnderscoredExtra.ShouldBe("B");
        results[0].IsRoot.ShouldBeFalse();
        results[0].CoversNextWord.ShouldBeFalse();
    }

    [Fact]
    public void ParserExtracts_MarksAsRootAndNextWord()
    {
        var results = StrongsRegexParser.Parse("H9002/{H1031}+\\H9014").ToList();

        results[1].LanguageId.ShouldBe(StrongsLanguage.Hebrew);
        results[1].Number.ShouldBe(1031);
        results[1].DisambiguatedExtra.ShouldBeNull();
        results[1].Extra.ShouldBeNull();
        results[1].UnderscoredExtra.ShouldBeNull();
        results[1].IsRoot.ShouldBeTrue();
        results[1].CoversNextWord.ShouldBeTrue();
        results[1].Order.ShouldBe(2);
    }
}
