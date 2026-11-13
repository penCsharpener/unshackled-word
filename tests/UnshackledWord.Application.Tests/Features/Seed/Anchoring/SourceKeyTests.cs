using UnshackledWord.Application.Features.Seed.Anchoring;

namespace UnshackledWord.Application.Tests.Features.Seed.Anchoring;

public class SourceKeyTests
{
    [Fact]
    public void Hash_ShouldMatchSha256OfTheBareLetters_WhenGreekWordHasAccents()
    {
        // sha256("λογος") computed independently
        SourceKey.Hash("λόγος").ShouldBe("10594c46");
    }

    [Fact]
    public void Hash_ShouldMatchSha256OfTheBareLetters_WhenHebrewWordHasPointsAndCantillation()
    {
        // sha256("בראשית") computed independently
        SourceKey.Hash("בְּרֵאשִׁ֖ית").ShouldBe("26358cad");
    }

    [Theory]
    [InlineData("Λόγος", "λογος")]
    [InlineData("λόγος,", "λογος")]
    [InlineData("ὁ λόγος", "ὁλόγος")]
    [InlineData("הָאָֽרֶץ׃", "הארץ")]
    public void Hash_ShouldIgnoreCaseAccentsAndPunctuation_WhenSameLetters(string word, string sameLetters)
    {
        SourceKey.Hash(word).ShouldBe(SourceKey.Hash(sameLetters));
    }

    [Fact]
    public void Hash_ShouldDiffer_WhenALetterChanges()
    {
        SourceKey.Hash("λόγος").ShouldNotBe(SourceKey.Hash("λόγον"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Hash_ShouldBeStable_WhenWordIsEmpty(string? word)
    {
        SourceKey.Hash(word).ShouldBe(SourceKey.Hash(string.Empty));
        SourceKey.Hash(word).Length.ShouldBe(8);
    }

    [Fact]
    public void ToToken_ShouldCarryVerseAndPositionAndHash_WhenCreatedFromWord()
    {
        var token = SourceKey.From(40001001, 3, "λόγος").ToToken();

        token.ShouldBe(new AnchorToken(40001001, 3, "10594c46"));
        token.Chapter.ShouldBe(40001);
    }
}
