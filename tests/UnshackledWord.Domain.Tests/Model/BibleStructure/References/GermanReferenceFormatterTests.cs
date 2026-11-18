using UnshackledWord.Domain.Models.BibleStructure.References;

namespace UnshackledWord.Domain.Tests.Model.BibleStructure.References;

public class GermanReferenceFormatterTests
{
    private static readonly ScriptureParseOptions German = new(AliasProfile.German, false, true, VerseCounting.Hebrew);

    /// <summary>Spans as "c:v-c:v" strings joined by ", "; a single verse "c:v" and a whole chapter "c:0-c:999".</summary>
    private static ScriptureReference Ref(int? bookId, string spans, string? nonCanonical = null) =>
        new(bookId, nonCanonical, ParseSpans(spans), 0, 0, false);

    private static List<VerseSpan> ParseSpans(string spans) =>
        spans.Split(", ").Select(ParseSpan).ToList();

    private static VerseSpan ParseSpan(string text)
    {
        var parts = text.Split('-');
        var start = parts[0].Split(':');
        var end = (parts.Length > 1 ? parts[1] : parts[0]).Split(':');
        return new VerseSpan(int.Parse(start[0]), int.Parse(start[1]), int.Parse(end[0]), int.Parse(end[1]));
    }

    [Theory]
    [InlineData(66, "2:1-2:5, 2:7, 2:9-2:10", "Offb 2,1–5.7.9–10")]
    [InlineData(66, "2:1-2:5, 7:0-7:999, 9:0-10:999", "Offb 2,1–5; 7; 9–10")]
    [InlineData(66, "2:1-2:5, 2:7-2:7, 3:1-3:1", "Offb 2,1–5.7; 3,1")]
    [InlineData(66, "2:1-2:1", "Offb 2,1")]
    [InlineData(66, "1:0-1:999", "Offb 1")]
    [InlineData(45, "8:0-8:999", "Röm 8")]
    [InlineData(45, "8:0-9:999", "Röm 8–9")]
    [InlineData(45, "8:0-8:999, 12:0-12:999", "Röm 8; 12")]
    [InlineData(45, "8:28-8:28, 12:1-12:2", "Röm 8,28; 12,1–2")]
    [InlineData(45, "8:28-8:28, 8:31-8:31, 12:1-12:2, 12:9-12:9", "Röm 8,28.31; 12,1–2.9")]
    [InlineData(45, "8:0-8:5", "Röm 8–8,5")]
    [InlineData(1, "1:1-2:3", "1Mo 1,1–2,3")]
    [InlineData(1, "1:1-2:3, 2:5-2:5", "1Mo 1,1–2,3.5")]
    [InlineData(43, "3:16-3:18", "Joh 3,16–18")]
    [InlineData(19, "23:1-23:2", "Ps 23,1–2")]
    public void Format_writes_german_citation_style(int bookId, string spans, string expected)
    {
        GermanReferenceFormatter.Format(Ref(bookId, spans)).ShouldBe(expected);
    }

    [Fact]
    public void Format_uses_the_original_text_for_non_canonical_books()
    {
        GermanReferenceFormatter.Format(Ref(null, "3:1-3:1", nonCanonical: "Sir")).ShouldBe("Sir 3,1");
    }

    [Theory]
    [InlineData("Offb 2,1-5.7.9-10")]
    [InlineData("Offb 2,1-5; 7; 9-10")]
    [InlineData("Röm 8,28; 12,1-2")]
    [InlineData("Röm 8,28.31; 12,1-2.9")]
    [InlineData("Röm 8; 12")]
    [InlineData("Röm 8-9")]
    [InlineData("1Mo 1,1-2,3")]
    [InlineData("Ps 23,1f")]
    [InlineData("Joh 3,16; Röm 8,28")]
    public void Format_then_parse_gives_the_same_spans(string input)
    {
        var original = ScriptureReferenceParser.Parse(input, German);
        original.Error.ShouldBeNull();

        var text = string.Join("; ", original.References.Select(GermanReferenceFormatter.Format));
        var again = ScriptureReferenceParser.Parse(text, German);

        again.Error.ShouldBeNull();
        Describe(again).ShouldBe(Describe(original));
    }

    private static string Describe(ScriptureParseResult result) =>
        string.Join(" | ", result.References.Select(r => $"{r.BookId}: {string.Join("; ", r.Spans)}"));
}
