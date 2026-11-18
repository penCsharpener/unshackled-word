using UnshackledWord.Domain.Models.BibleStructure.References;

namespace UnshackledWord.Domain.Tests.Model.BibleStructure.References;

public class ReaderPathTests
{
    private static readonly ScriptureParseOptions German = new(AliasProfile.German, false, true, VerseCounting.Hebrew);

    /// <summary>Spans as "c:v-c:v" strings joined by ", "; a single verse "c:v" and a whole chapter "c:0-c:999".</summary>
    private static ScriptureReference Ref(int? bookId, string spans) =>
        new(bookId, null, spans.Split(", ").Select(ParseSpan).ToList(), 0, 0, false);

    private static VerseSpan ParseSpan(string text)
    {
        var parts = text.Split('-');
        var start = parts[0].Split(':');
        var end = (parts.Length > 1 ? parts[1] : parts[0]).Split(':');
        return new VerseSpan(int.Parse(start[0]), int.Parse(start[1]), int.Parse(end[0]), int.Parse(end[1]));
    }

    [Theory]
    [InlineData(66, "2:1-2:5, 2:7, 2:9-2:10", "/bible/66/2?v=1-5,7,9-10#v1")]
    [InlineData(66, "2:1-2:5, 7:0-7:999, 9:0-10:999", "/bible/66/2?v=1-5#v1")]
    [InlineData(45, "8:0-8:999", "/bible/45/8")]
    [InlineData(45, "8:0-9:999", "/bible/45/8")]
    [InlineData(45, "8:28-8:28, 12:1-12:2", "/bible/45/8?v=28#v28")]
    [InlineData(45, "12:1-12:2", "/bible/45/12?v=1-2#v1")]
    [InlineData(1, "1:1-2:3", "/bible/1/1?v=1-31#v1")]
    [InlineData(45, "8:0-8:5", "/bible/45/8?v=1-5#v1")]
    [InlineData(66, "1:0-1:999", "/bible/66/1")]
    [InlineData(43, "3:16-3:16", "/bible/43/3?v=16#v16")]
    public void Create_links_the_first_chapter_with_its_verses(int bookId, string spans, string expected)
    {
        ReaderPath.Create(Ref(bookId, spans)).ShouldBe(expected);
    }

    [Theory]
    [InlineData("Offb 2,1-5.7.9-10", "/bible/66/2?v=1-5,7,9-10#v1")]
    [InlineData("Röm 8", "/bible/45/8")]
    [InlineData("Röm 8-9", "/bible/45/8")]
    [InlineData("1Mo 1,1-2,3", "/bible/1/1?v=1-31#v1")]
    [InlineData("Röm 8,28; 12,1-2", "/bible/45/8?v=28#v28")]
    [InlineData("Joh 3,16", "/bible/43/3?v=16#v16")]
    [InlineData("Ps 23,1f", "/bible/19/23?v=1-2#v1")]
    public void Create_for_parsed_citations(string input, string expected)
    {
        var result = ScriptureReferenceParser.Parse(input, German);
        result.Error.ShouldBeNull();

        ReaderPath.Create(result.References[0]).ShouldBe(expected);
    }

    [Fact]
    public void Create_for_a_later_group_of_a_reference_links_its_own_chapter()
    {
        var result = ScriptureReferenceParser.Parse("Röm 8,28; 12,1-2", German);
        var secondGroup = Ref(45, "12:1-12:2");

        result.Error.ShouldBeNull();
        ReaderPath.Create(secondGroup).ShouldBe("/bible/45/12?v=1-2#v1");
    }

    [Fact]
    public void Create_returns_null_for_non_canonical_books()
    {
        var reference = new ScriptureReference(null, "Sir", [new VerseSpan(3, 1, 3, 1)], 0, 0, false);

        ReaderPath.Create(reference).ShouldBeNull();
    }

    [Fact]
    public void Create_returns_null_for_a_chapter_the_book_does_not_have()
    {
        ReaderPath.Create(Ref(66, "23:1-23:1")).ShouldBeNull();
    }

    [Fact]
    public void Create_uses_hebrew_counting_like_the_reader()
    {
        ReaderPath.Create(Ref(39, "3:20-3:24")).ShouldBe("/bible/39/3?v=20-24#v20");
        ReaderPath.Create(Ref(39, "4:1-4:1")).ShouldBeNull();
        ReaderPath.Create(Ref(39, "3:0-3:999")).ShouldBe("/bible/39/3");
        ReaderPath.Create(Ref(19, "3:1-3:999")).ShouldBe("/bible/19/3?v=1-9#v1");
    }
}
