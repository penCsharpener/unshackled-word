using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Domain.Tests.Model.Study;

public class LexiconTextCleanerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Clean_EmptyInput_ReturnsEmpty(string? raw)
    {
        LexiconTextCleaner.Clean(raw).ShouldBe((string.Empty, false));
    }

    [Fact]
    public void Clean_PlainText_IsUnchanged()
    {
        LexiconTextCleaner.Clean("a plain text").ShouldBe(("a plain text", false));
    }

    [Fact]
    public void Clean_StripsTagsAndCollapsesWhitespace()
    {
        LexiconTextCleaner.Clean("<b>x</b><br/>y").ShouldBe(("x y", false));
        LexiconTextCleaner.Clean("a \n\t  b").ShouldBe(("a b", false));
    }

    [Fact]
    public void Clean_DecodesEntities()
    {
        LexiconTextCleaner.Clean("a &amp; b &lt;c&gt;").Text.ShouldBe("a & b <c>");
    }

    [Fact]
    public void Clean_RepairsSpaceBeforePunctuation()
    {
        LexiconTextCleaner.Clean("<i>word</i>, next").Text.ShouldBe("word, next");
    }

    [Fact]
    public void Clean_LongText_IsCutAtWordBoundary()
    {
        var raw = string.Join(' ', Enumerable.Repeat("abcdefghi", 20));

        var (text, truncated) = LexiconTextCleaner.Clean(raw, 50);

        truncated.ShouldBeTrue();
        text.ShouldEndWith("…");
        text.Length.ShouldBeLessThanOrEqualTo(51);
        text.TrimEnd('…').ShouldNotEndWith(" ");
        text.TrimEnd('…').Split(' ').ShouldAllBe(w => w == "abcdefghi");
    }

    [Fact]
    public void Clean_ExactlyMaxLength_IsNotTruncated()
    {
        LexiconTextCleaner.Clean(new string('a', 10), 10).Truncated.ShouldBeFalse();
    }
}
