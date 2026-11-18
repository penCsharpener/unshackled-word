using UnshackledWord.Domain.Models.BibleStructure.References;
using UnshackledWord.Domain.Models.Study;

namespace UnshackledWord.Domain.Tests.Model.Study;

public class LexiconTextSegmenterTests
{
    // Only the rows these tests touch (Mal 4 / Ps 3 differ between English and Hebrew counting).
    private static readonly VerseCountingMap Map = new(
    [
        new(39004005, 39003023), new(19003001, 19003002),
    ]);

    private static (IReadOnlyList<LexiconSegment> Segments, bool Truncated) Seg(string raw, int max = 600) =>
        LexiconTextSegmenter.Segment(raw, max, Map);

    private static LexiconRef? RefOf(string raw) => Seg(raw).Segments.FirstOrDefault(s => s.Ref is not null)?.Ref;

    private static string Joined(IEnumerable<LexiconSegment> segments) => string.Concat(segments.Select(s => s.Text));

    private static string Spans(LexiconRef r) => string.Join(",", r.Spans.Select(s => $"{s.StartChapter}:{s.StartVerse}-{s.EndChapter}:{s.EndVerse}"));

    [Fact]
    public void SingleTag()
    {
        var r = RefOf("<ref='Mat.5.29'>Mat.5:29</ref>")!;
        r.BookId.ShouldBe(40);
        Spans(r).ShouldBe("5:29-5:29");
    }

    [Fact]
    public void ChapterList()
    {
        var r = RefOf("<ref='Heb.5.4; 7.11; 9.4'>Heb.5:4, 7:11, 9:4</ref>")!;
        r.BookId.ShouldBe(58);
        Spans(r).ShouldBe("5:4-5:4,7:11-7:11,9:4-9:4");
    }

    [Fact]
    public void VerseList() => Spans(RefOf("<ref='Mat.25.33, 34'>Mat.25:33, 34</ref>")!).ShouldBe("25:33-25:33,25:34-25:34");

    [Fact]
    public void RangeInAttribute()
    {
        var r = RefOf("<ref='Gen.4.2-10'>Gen.4:2-10</ref>")!;
        r.BookId.ShouldBe(1);
        Spans(r).ShouldBe("4:2-4:10");
    }

    [Fact]
    public void TrailingDotInAttributeAndText()
    {
        var (segments, _) = Seg("<ref='Luk.4.34.'>Luk.4:34.</ref>");
        segments[0].Text.ShouldBe("Luk.4:34");
        Spans(segments[0].Ref!).ShouldBe("4:34-4:34");
        Joined(segments).ShouldBe("Luk.4:34.");
    }

    [Fact]
    public void ListPunctuationMovesOutOfLabel()
    {
        var (segments, _) = Seg("<ref='Rev.5.1'>Rev.5:1;</ref> x");
        segments.Select(s => s.Text).ShouldBe(["Rev.5:1", "; x"]);
        segments[0].Ref.ShouldNotBeNull();
        segments[1].Ref.ShouldBeNull();
    }

    [Fact]
    public void TextWithoutSeparatorIsOneLink()
    {
        var (segments, _) = Seg("<ref='Act.2.33; 5.31'>Act.2:33 5:31</ref>");
        segments.Count.ShouldBe(1);
        Spans(segments[0].Ref!).ShouldBe("2:33-2:33,5:31-5:31");
    }

    [Fact]
    public void AttributeFailsTextIsUsed()
    {
        var (segments, _) = Seg("<ref='bogus'>Mat.5:29</ref>");
        segments[0].Ref.ShouldNotBeNull();
        segments[0].Text.ShouldBe("Mat.5:29");
    }

    [Fact]
    public void NonCanonicalTagIsPlainText()
    {
        var (segments, _) = Seg("<ref='Sir.3.1'>Sir.3:1</ref>");
        segments.ShouldBe([new LexiconSegment("Sir.3:1", null)]);
    }

    [Fact]
    public void UnknownBookTagIsPlainText()
    {
        var (segments, _) = Seg("<ref='Xyz.1.1'>Xyz.1:1</ref>");
        segments.ShouldBe([new LexiconSegment("Xyz.1:1", null)]);
    }

    [Fact]
    public void UntaggedReferenceInProse()
    {
        var (segments, _) = Seg("first mentioned at 1Ch.8.39; <br> son of");
        segments.Select(s => s.Text).ShouldBe(["first mentioned at ", "1Ch.8.39", "; son of"]);
        segments[1].Ref!.BookId.ShouldBe(13);
        Spans(segments[1].Ref!).ShouldBe("8:39-8:39");
    }

    [Fact]
    public void WordJobStaysText()
    {
        var (segments, _) = Seg("the Job.1.1 and Job is");
        segments.Count(s => s.Ref is not null).ShouldBe(1);
        segments.First(s => s.Ref is not null).Text.ShouldBe("Job.1.1");
        Joined(segments).ShouldBe("the Job.1.1 and Job is");
    }

    [Fact]
    public void SpaceBeforeCommaKeepsTextAfterLink()
    {
        var (segments, _) = Seg("see 2Co.6.7 , al.");
        segments.Select(s => s.Text).ShouldBe(["see ", "2Co.6.7", ", al."]);
    }

    [Fact]
    public void EnglishCountingIsMappedToHebrew()
    {
        Spans(RefOf("<ref='Mal.4.5'>Mal.4:5</ref>")!).ShouldBe("3:23-3:23");
        Spans(RefOf("<ref='Psa.3.1'>Psa.3:1</ref>")!).ShouldBe("3:2-3:2");
        Spans(RefOf("Jhn.3.16")!).ShouldBe("3:16-3:16");
    }

    [Fact]
    public void OutOfRangeSpanIsPlainText() => RefOf("<ref='Gen.99.1'>Gen.99:1</ref>").ShouldBeNull();

    [Fact]
    public void TruncationNeverCutsInsideReference()
    {
        const string raw = "alpha beta Gen.1.1 gamma delta Gen.2.2 epsilon zeta Gen.3.3 eta";
        var (segments, truncated) = Seg(raw, 34); // limit falls inside the second reference (index 31..38)
        truncated.ShouldBeTrue();
        segments.Count(s => s.Ref is not null).ShouldBe(1);
        segments[^1].Text.ShouldEndWith("…");
        Joined(segments).ShouldBe("alpha beta Gen.1.1 gamma delta…");
    }

    [Fact]
    public void NoTruncationWithinLimit()
    {
        var (segments, truncated) = Seg("alpha Gen.1.1", 600);
        truncated.ShouldBeFalse();
        segments.Count.ShouldBe(2);
    }

    [Fact]
    public void EmptyInput() => Seg("   ").ShouldBe(([], false));

    [Theory]
    [InlineData("<ref='Mat.5.29'>Mat.5:29</ref>")]
    [InlineData("<ref='Heb.5.4; 7.11; 9.4'>Heb.5:4, 7:11, 9:4</ref>")]
    [InlineData("<ref='Luk.4.34.'>Luk.4:34.</ref>")]
    [InlineData("<ref='Rev.5.1'>Rev.5:1;</ref> x")]
    [InlineData("<ref='Act.2.33; 5.31'>Act.2:33 5:31</ref>")]
    [InlineData("<ref='Sir.3.1'>Sir.3:1</ref>")]
    [InlineData("<ref='Xyz.1.1'>Xyz.1:1</ref>")]
    [InlineData("first mentioned at 1Ch.8.39; <br> son of")]
    [InlineData("the Job.1.1 and Job is")]
    [InlineData("see 2Co.6.7 , al.")]
    [InlineData("a <b>bold</b> word, see <ref='Gen.4.2-10'>Gen.4:2-10</ref>, then <i>x</i> &amp; y.")]
    [InlineData("see <ref='Mat.5.29'>Mat.5:29</ref> ; and <ref='Rev.5.1'>Rev.5:1 ;</ref>  last")]
    [InlineData("plain text only")]
    public void SegmentTextsEqualCleanOutput(string raw)
    {
        var (segments, truncated) = Seg(raw);
        truncated.ShouldBeFalse();
        Joined(segments).ShouldBe(LexiconTextCleaner.Clean(raw).Text);
    }
}
