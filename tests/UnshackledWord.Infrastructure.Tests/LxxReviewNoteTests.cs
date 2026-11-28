using UnshackledWord.Infrastructure.Lxx;

namespace UnshackledWord.Infrastructure.Tests;

public class LxxReviewNoteTests
{
    [Fact]
    public void PlainNote_turns_breaks_into_lines_and_strips_tags()
    {
        Assert.Equal("1) first\nAlso: re.shit", LxxReviewRepository.PlainNote("1) first<br>Also: <i>re.shit</i>"));
    }

    [Fact]
    public void PlainNote_shortens_long_text_and_handles_empty()
    {
        Assert.Equal(string.Empty, LxxReviewRepository.PlainNote(null));
        var note = LxxReviewRepository.PlainNote(new string('a', 1000));
        Assert.Equal(401, note.Length);
        Assert.EndsWith("…", note);
    }
}
