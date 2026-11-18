using UnshackledWord.Domain.Models.BibleStructure.References;

namespace UnshackledWord.Domain.Tests.Model.BibleStructure.References;

public class ScriptureReferenceParserTests
{
    private static readonly ScriptureParseOptions German = new(AliasProfile.German, false, true, VerseCounting.Hebrew);
    private static readonly ScriptureParseOptions GermanPrefix = German with { AllowPrefix = true };
    private static readonly ScriptureParseOptions Osis = new(AliasProfile.Osis, false, true, VerseCounting.English);

    /// <summary>Spans as "c:v-c:v" strings joined by ", "; a single-verse "c:v" is written "c:v-c:v".</summary>
    private static string Fmt(IEnumerable<VerseSpan> spans) =>
        string.Join(", ", spans.Select(s => $"{s.StartChapter}:{s.StartVerse}-{s.EndChapter}:{s.EndVerse}"));

    private static string Norm(string expected) =>
        string.Join(", ", expected.Split(", ").Select(s => s.Contains('-') ? s : $"{s}-{s}"));

    private static ScriptureReference Single(string input, ScriptureParseOptions? options = null)
    {
        var result = ScriptureReferenceParser.Parse(input, options ?? German);
        result.Error.ShouldBeNull($"input '{input}'");
        result.References.Count.ShouldBe(1, $"input '{input}'");
        return result.References[0];
    }

    // ---- B. single references ----

    [Theory]
    [InlineData("Offb 2,1", 66, "2:1")]
    [InlineData("Offb 2:1", 66, "2:1")]
    [InlineData("Offb. 2, 1", 66, "2:1")]
    [InlineData("offb 2 , 1", 66, "2:1")]
    [InlineData("Offb 2 1", 66, "2:1")]
    [InlineData("Joh 3,16-18", 43, "3:16-3:18")]
    [InlineData("Joh 3,16–18", 43, "3:16-3:18")]
    [InlineData("Joh 3,16 - 18", 43, "3:16-3:18")]
    [InlineData("Joh 3:16—18", 43, "3:16-3:18")]
    [InlineData("Röm 8", 45, "8:0-8:999")]
    [InlineData("Röm 8-9", 45, "8:0-9:999")]
    [InlineData("Röm 8–9", 45, "8:0-9:999")]
    [InlineData("1Mo 1,1-2,3", 1, "1:1-2:3")]
    [InlineData("1. Mose 1,1–2,3", 1, "1:1-2:3")]
    [InlineData("Ps 23,1f", 19, "23:1-23:2")]
    [InlineData("Ps 23,1 f.", 19, "23:1-23:2")]
    [InlineData("Ps 23,1ff", 19, "23:1-23:6")]
    [InlineData("Joh 3,16a", 43, "3:16")]
    [InlineData("Joh 3,16b", 43, "3:16")]
    [InlineData("Jud 3", 65, "1:3")]
    [InlineData("Judas 3", 65, "1:3")]
    [InlineData("Phlm 10", 57, "1:10")]
    [InlineData("Ob 4", 31, "1:4")]
    [InlineData("3Joh 2", 64, "1:2")]
    [InlineData("Jud 1,3", 65, "1:3")]
    [InlineData("1. Korinther 13,4-7", 46, "13:4-13:7")]
    [InlineData("I Kor 13,4–7", 46, "13:4-13:7")]
    public void Parse_SingleReference_BookAndSpans(string input, int book, string spans)
    {
        var reference = Single(input);

        reference.BookId.ShouldBe(book);
        reference.BookOnly.ShouldBeFalse();
        Fmt(reference.Spans).ShouldBe(Norm(spans));
    }

    [Fact]
    public void Parse_BookOnly_WholeFirstChapterAndFlag()
    {
        var reference = Single("Offb");

        reference.BookId.ShouldBe(66);
        reference.BookOnly.ShouldBeTrue();
        Fmt(reference.Spans).ShouldBe("1:0-1:999");
    }

    [Fact]
    public void Parse_StartAndLength_CoverTheReferenceWithoutSentenceDot()
    {
        var reference = Single("Joh 3,16.");

        reference.Start.ShouldBe(0);
        reference.Length.ShouldBe(8);
    }

    [Fact]
    public void ToRefIds_BuildsPassageRangeIds()
    {
        new VerseSpan(2, 1, 2, 5).ToRefIds(66).ShouldBe((66_002_001, 66_002_005));
        new VerseSpan(8, 0, 9, 999).ToRefIds(45).ShouldBe((45_008_000, 45_009_999));
    }

    // ---- C. lists and ranges ----

    [Theory]
    [InlineData("Offb 2,1-5.7.9-10", "2:1-2:5, 2:7, 2:9-2:10")]
    [InlineData("Offb 2,1–5. 7. 9–10", "2:1-2:5, 2:7, 2:9-2:10")]
    [InlineData("Offb 2:1-5.7.9-10", "2:1-2:5, 2:7, 2:9-2:10")]
    [InlineData("Offb 2,1-5; 7; 9-10", "2:1-2:5, 7:0-7:999, 9:0-10:999")]
    [InlineData("Offb 2,1-5 u. 7", "2:1-2:5, 2:7")]
    [InlineData("Offb 2,1-5 und 7", "2:1-2:5, 2:7")]
    [InlineData("Offb 2,1-5,7", "2:1-5:7")]
    [InlineData("Offb 2,1; 3,4", "2:1, 3:4")]
    [InlineData("Offb 2,1; 3:4", "2:1, 3:4")]
    [InlineData("Röm 8; 12", "8:0-8:999, 12:0-12:999")]
    [InlineData("Röm 8; 9,1", "8:0-8:999, 9:1")]
    [InlineData("Röm 8,28; 12", "8:28, 12:0-12:999")]
    [InlineData("Röm 8,28; 12,1-2", "8:28, 12:1-12:2")]
    [InlineData("Röm 8,28.31; 12,1-2.9", "8:28, 8:31, 12:1-12:2, 12:9")]
    [InlineData("Joh 3,1 und 2", "3:1, 3:2")]
    [InlineData("Ps 23,1f.; 5,2", "23:1-23:2, 5:2")]
    [InlineData("Joh 3,16.", "3:16")]
    [InlineData("Phlm 4.10", "1:4, 1:10")]
    [InlineData("Jud 3; 5", "1:3, 1:5")]
    public void Parse_ListsAndRanges_Spans(string input, string spans)
    {
        var reference = Single(input);

        Fmt(reference.Spans).ShouldBe(Norm(spans));
    }

    [Fact]
    public void Parse_VerseList_IsOneReferenceWithThreeSpansInChapterTwo()
    {
        var reference = Single("Offb 2,1-5.7.9-10");

        reference.Spans.Count.ShouldBe(3);
        reference.Spans.ShouldAllBe(s => s.StartChapter == 2 && s.EndChapter == 2);
    }

    [Theory]
    [InlineData("Joh 3,16; Röm 8,28", 43, "3:16", 45, "8:28")]
    [InlineData("Joh 3,16; 1Kor 13,4", 43, "3:16", 46, "13:4")]
    [InlineData("Joh 3,16; 1 Kor 13,4", 43, "3:16", 46, "13:4")]
    [InlineData("Joh 3,16; 1. Kor 13,4-7", 43, "3:16", 46, "13:4-13:7")]
    public void Parse_TwoBooks_TwoReferences(string input, int book1, string spans1, int book2, string spans2)
    {
        var result = ScriptureReferenceParser.Parse(input, German);

        result.Error.ShouldBeNull();
        result.References.Count.ShouldBe(2);
        result.References[0].BookId.ShouldBe(book1);
        Fmt(result.References[0].Spans).ShouldBe(Norm(spans1));
        result.References[1].BookId.ShouldBe(book2);
        Fmt(result.References[1].Spans).ShouldBe(Norm(spans2));
    }

    [Fact]
    public void Parse_SecondReference_StartsAtItsBook()
    {
        var result = ScriptureReferenceParser.Parse("Joh 3,16; Röm 8,28", German);

        result.References[1].Start.ShouldBe(10);
        result.References[1].Length.ShouldBe(8);
    }

    [Fact]
    public void Parse_BookOnlyThenAnotherBook_Works()
    {
        var result = ScriptureReferenceParser.Parse("Joh; Röm 8", German);

        result.Error.ShouldBeNull();
        result.References[0].BookOnly.ShouldBeTrue();
        result.References[1].BookId.ShouldBe(45);
    }

    // ---- prefix, counting, notation ----

    [Fact]
    public void Parse_PrefixBook_ResolvesWhenAllowed()
    {
        Single("Offen 2,1", GermanPrefix).BookId.ShouldBe(66);
        ScriptureReferenceParser.Parse("Offen 2,1", German).Error!.Code.ShouldBe("unknownBook");
    }

    [Fact]
    public void Parse_EnglishCounting_DoesNotValidateUpperBounds()
    {
        var reference = Single("Mal 4:5", new ScriptureParseOptions(AliasProfile.German, false, true, VerseCounting.English));

        Fmt(reference.Spans).ShouldBe("4:5-4:5");
    }

    [Theory]
    [InlineData("Mal 3,24", "3:24-3:24")]
    [InlineData("Ps 3,9", "3:9-3:9")]
    [InlineData("Joel 4,21", "4:21-4:21")]
    public void Parse_HebrewCounting_UsesHebrewVerseCounts(string input, string expected) =>
        Fmt(Single(input, German).Spans).ShouldBe(expected);

    [Theory]
    [InlineData("Mal 4,6", "chapterOutOfRange")]
    [InlineData("Ps 3,10", "verseOutOfRange")]
    [InlineData("Mal 3,25", "verseOutOfRange")]
    public void Parse_HebrewCounting_RejectsEnglishOnlyNumbers(string input, string code) =>
        ScriptureReferenceParser.Parse(input, German).Error!.Code.ShouldBe(code);

    [Fact]
    public void Parse_NonCanonicalBook_KeepsTextAndSpans()
    {
        var reference = Single("Sir.3.1", Osis);

        reference.BookId.ShouldBeNull();
        reference.NonCanonical.ShouldBe("Sir.");
        Fmt(reference.Spans).ShouldBe("3:1-3:1");
    }

    [Theory]
    [InlineData("Mat.5.29", 40, "5:29")]
    [InlineData("Heb.5.4; 7.11; 9.4", 58, "5:4, 7:11, 9:4")]
    [InlineData("Mat.25.33, 34", 40, "25:33, 25:34")]
    [InlineData("Gen.4.2-10", 1, "4:2-4:10")]
    [InlineData("Luk.4.34.", 42, "4:34")]
    [InlineData("Mat.5:29", 40, "5:29")]
    public void Parse_OsisNotation_Spans(string input, int book, string spans)
    {
        var reference = Single(input, Osis);

        reference.BookId.ShouldBe(book);
        Fmt(reference.Spans).ShouldBe(Norm(spans));
    }

    [Fact]
    public void Parse_NonWholeInput_StopsQuietlyAtTrailingText()
    {
        var options = German with { WholeInput = false };

        var result = ScriptureReferenceParser.Parse("Joh 3,16 xyz", options);

        result.Error.ShouldBeNull();
        result.References.Count.ShouldBe(1);
    }

    [Fact]
    public void ParseAt_ReadsReferenceInsideText()
    {
        var reference = ScriptureReferenceParser.ParseAt("see Mat.5.29 and more", 4, Osis);

        reference.ShouldNotBeNull();
        reference.BookId.ShouldBe(40);
        reference.Start.ShouldBe(4);
        reference.Length.ShouldBe(8);
        ScriptureReferenceParser.ParseAt("see nothing", 4, Osis).ShouldBeNull();
    }

    [Fact]
    public void Parse_NonAsciiDigits_ReturnsErrorInsteadOfThrowing()
    {
        var result = ScriptureReferenceParser.Parse("Joh ٣,16", German);

        result.Error.ShouldNotBeNull();
    }

    // ---- D. errors ----

    [Theory]
    [InlineData("", "empty", 0)]
    [InlineData("   ", "empty", 0)]
    [InlineData("Xyz 3,16", "unknownBook", 0)]
    [InlineData("Rev 2:1", "unknownBook", 0)]
    [InlineData("Joh 3,0", "verseOutOfRange", 6)]
    [InlineData("Joh 3,99", "verseOutOfRange", 6)]
    [InlineData("Offb 23", "chapterOutOfRange", 5)]
    [InlineData("Joh 0", "chapterOutOfRange", 4)]
    [InlineData("Joh 3,18-16", "endBeforeStart", 9)]
    [InlineData("Joh 4-3", "endBeforeStart", 6)]
    [InlineData("Joh 3,16-", "unexpectedToken", 9)]
    [InlineData("Joh 3,,16", "unexpectedToken", 6)]
    [InlineData("Röm 8.9", "unexpectedToken", 5)]
    [InlineData("Joh 3,16,18", "unexpectedToken", 8)]
    [InlineData("Joh 3,16 xyz", "unexpectedToken", 9)]
    [InlineData("Joh ,16", "missingChapter", 4)]
    [InlineData("Joh 3,16; Xyz 2", "unknownBook", 10)]
    public void Parse_Invalid_ErrorCodeAndPosition(string input, string code, int position)
    {
        var result = ScriptureReferenceParser.Parse(input, German);

        result.Error.ShouldNotBeNull();
        result.Error.Code.ShouldBe(code);
        result.Error.Position.ShouldBe(position);
    }

    [Fact]
    public void Parse_AmbiguousPrefix_ListsCandidates()
    {
        var result = ScriptureReferenceParser.Parse("Phili 1", GermanPrefix);

        result.Error.ShouldNotBeNull();
        result.Error.Code.ShouldBe("ambiguousBook");
        result.BookCandidates.ShouldContain(50);
        result.BookCandidates.ShouldContain(57);
    }

    [Fact]
    public void Parse_BookOnlyWholeInput_IsNotAnError()
    {
        var result = ScriptureReferenceParser.Parse("Joh", German);

        result.Error.ShouldBeNull();
        result.References[0].BookOnly.ShouldBeTrue();
    }

    [Fact]
    public void Parse_OutOfRangeErrorArgs_CarryValueAndMax()
    {
        var result = ScriptureReferenceParser.Parse("Joh 3,99", German);

        result.Error!.Args["value"].ShouldBe("99");
        result.Error.Args["max"].ShouldBe("36");
    }
}
