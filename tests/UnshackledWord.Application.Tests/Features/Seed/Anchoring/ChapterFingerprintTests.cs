using UnshackledWord.Application.Features.Seed.Anchoring;

namespace UnshackledWord.Application.Tests.Features.Seed.Anchoring;

public class ChapterFingerprintTests
{
    [Fact]
    public void Compute_ShouldReturnTwelveLowerCaseHexDigits_WhenGivenTokens()
    {
        var fingerprint = ChapterFingerprint.Compute(AnchorTestData.Chapter1("Im Anfang"));

        fingerprint.Length.ShouldBe(12);
        fingerprint.ShouldMatch("^[0-9a-f]{12}$");
    }

    [Fact]
    public void Compute_ShouldMatchSha256OfTheTokenLines_WhenGivenTwoTokens()
    {
        var tokens = new[] { new AnchorToken(1001001, 1, "Im"), new AnchorToken(1001001, 2, "Anfang") };

        // sha256("1001001|1|Im\n1001001|2|Anfang\n") computed independently
        ChapterFingerprint.Compute(tokens).ShouldBe("66b798aca2f9");
    }

    [Fact]
    public void Compute_ShouldBeEqual_WhenTokensAreIdentical()
    {
        ChapterFingerprint.Compute(AnchorTestData.Chapter1("Im Anfang", "Und die Erde"))
            .ShouldBe(ChapterFingerprint.Compute(AnchorTestData.Chapter1("Im Anfang", "Und die Erde")));
    }

    [Fact]
    public void Compute_ShouldIgnorePunctuation_WhenTokensWereBuiltWithForWord()
    {
        ChapterFingerprint.Compute(AnchorTestData.Chapter1("Im Anfang, schuf"))
            .ShouldBe(ChapterFingerprint.Compute(AnchorTestData.Chapter1("Im Anfang schuf")));
    }

    [Theory]
    [InlineData("Im Anfang schuf", "Im Anfang schuft")]
    [InlineData("Im Anfang schuf", "Im schuf Anfang")]
    [InlineData("Im Anfang schuf", "Im Anfang")]
    [InlineData("Im Anfang schuf", "Im Anfang schuf Gott")]
    public void Compute_ShouldDiffer_WhenTextOrderOrCountChanges(string left, string right)
    {
        ChapterFingerprint.Compute(AnchorTestData.Chapter1(left)).ShouldNotBe(ChapterFingerprint.Compute(AnchorTestData.Chapter1(right)));
    }

    [Fact]
    public void Compute_ShouldDiffer_WhenSameWordsAreSplitIntoVersesDifferently()
    {
        ChapterFingerprint.Compute(AnchorTestData.Chapter1("Im Anfang schuf", "Gott"))
            .ShouldNotBe(ChapterFingerprint.Compute(AnchorTestData.Chapter1("Im Anfang", "schuf Gott")));
    }
}
