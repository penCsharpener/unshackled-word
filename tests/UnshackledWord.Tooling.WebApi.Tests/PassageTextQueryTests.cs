using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetPassageText;
using UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.GetPassageText;

namespace UnshackledWord.Tooling.WebApi.Tests;

public class PassageTextQueryTests
{
    /// <summary>A verse repository holding every verse of the range; the text is "v{verse}" so tests can tell verses apart.</summary>
    private static IElb1871VerseRepository FakeVerses()
    {
        var verses = Substitute.For<IElb1871VerseRepository>();
        verses.GetByHebRefIdRangeAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<IReadOnlyList<ElbVerse>>(
                Ids(ci.ArgAt<int>(0), ci.ArgAt<int>(1))
                    .Select(id => new ElbVerse { HebRefId = id, VerseText = $"base {id % 1000}" })
                    .ToList()));
        verses.GetEffectiveByHebRefIdRangeAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<IReadOnlyList<EffectiveElbVerse>>(
                Ids(ci.ArgAt<int>(0), ci.ArgAt<int>(1))
                    .Select(id => new EffectiveElbVerse { HebRefId = id, VerseText = $"effective {id % 1000}" })
                    .ToList()));
        return verses;
    }

    private static IEnumerable<int> Ids(int from, int to) => Enumerable.Range(from, to - from + 1);

    private static Task<GetPassageTextResponse> Read(IElb1871VerseRepository verses, string spans, int level) =>
        PassageTextQuery.ReadAsync(verses, spans, level, TestContext.Current.CancellationToken);

    [Fact]
    public async Task ReadAsync_ReturnsOneGroupPerSpan_ForTheRoadmapExample()
    {
        var response = await Read(FakeVerses(), "66002001-66002005,66002007", 200);

        response.Truncated.ShouldBeFalse();
        response.Passages.Count.ShouldBe(2);
        response.Passages.Sum(p => p.Verses.Count).ShouldBe(6);

        var first = response.Passages[0];
        first.BibleBookId.ShouldBe(66);
        first.Chapter.ShouldBe(2);
        first.Verse.ShouldBe(1);
        first.EndVerse.ShouldBe(5);
        first.Verses.Select(v => v.Verse).ShouldBe([1, 2, 3, 4, 5]);
        first.Verses[0].Text.ShouldBe("effective 1");

        var second = response.Passages[1];
        second.Chapter.ShouldBe(2);
        second.Verse.ShouldBe(7);
        second.EndVerse.ShouldBeNull();
        second.Verses.Select(v => v.Verse).ShouldBe([7]);
    }

    [Fact]
    public async Task ReadAsync_TruncatesAt40Verses_AndSaysSo()
    {
        // Revelation 2 has 29 verses, Revelation 3 has 22: 41 verses requested, 40 returned.
        var response = await Read(FakeVerses(), "66002001-66002029,66003001-66003012", 200);

        response.Truncated.ShouldBeTrue();
        response.Passages.Sum(p => p.Verses.Count).ShouldBe(40);
        response.Passages[^1].Chapter.ShouldBe(3);
        response.Passages[^1].Verse.ShouldBe(1);
        response.Passages[^1].EndVerse.ShouldBe(11);
    }

    [Fact]
    public async Task ReadAsync_ExactlyFortyVerses_IsNotTruncated()
    {
        var response = await Read(FakeVerses(), "66002001-66002029,66003001-66003011", 200);

        response.Truncated.ShouldBeFalse();
        response.Passages.Sum(p => p.Verses.Count).ShouldBe(40);
    }

    [Fact]
    public async Task ReadAsync_TruncatesBeyondTenSpans()
    {
        var spans = string.Join(',', Enumerable.Range(1, 11).Select(v => $"66001{v:000}"));

        var response = await Read(FakeVerses(), spans, 200);

        response.Truncated.ShouldBeTrue();
        response.Passages.Count.ShouldBe(10);
    }

    [Fact]
    public async Task ReadAsync_SplitsASpanAcrossChapters_IntoOneGroupPerChapter()
    {
        // Revelation 1 has 20 verses.
        var response = await Read(FakeVerses(), "66001019-66002002", 200);

        response.Passages.Count.ShouldBe(2);
        response.Passages[0].Chapter.ShouldBe(1);
        response.Passages[0].Verse.ShouldBe(19);
        response.Passages[0].EndVerse.ShouldBe(20);
        response.Passages[1].Chapter.ShouldBe(2);
        response.Passages[1].Verse.ShouldBe(1);
        response.Passages[1].EndVerse.ShouldBe(2);
    }

    [Fact]
    public async Task ReadAsync_AtLevel0_ReadsElb1871()
    {
        var verses = FakeVerses();

        var response = await Read(verses, "66002001", 0);

        response.Passages.Single().Verses.Single().Text.ShouldBe("base 1");
        await verses.Received(1).GetByHebRefIdRangeAsync(66002001, 66002001, 1, TestContext.Current.CancellationToken);
        await verses.DidNotReceive().GetEffectiveByHebRefIdRangeAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReadAsync_AboveLevel0_ReadsTheEffectiveVersesAtTheLevel()
    {
        var verses = FakeVerses();

        await Read(verses, "66002001", 350);

        await verses.Received(1).GetEffectiveByHebRefIdRangeAsync(66002001, 66002001, 350, TestContext.Current.CancellationToken);
        await verses.DidNotReceive().GetByHebRefIdRangeAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("66002005-66002001")]
    [InlineData("66002030")]
    [InlineData("66002001-43001001")]
    [InlineData("67001001")]
    [InlineData("66002001,")]
    [InlineData("abc")]
    [InlineData("66002001-66002002-66002003")]
    public void Parser_RejectsInvalidSpans(string spans)
    {
        PassageSpanParser.TryParse(spans, out _).ShouldBeFalse();
        PassageSpanParser.IsValid(spans).ShouldBeFalse();
    }

    [Fact]
    public void Validator_AcceptsValidSpansAndLevel_AndRejectsTheRest()
    {
        var validator = new GetPassageTextValidator();

        validator.Validate(new GetPassageTextRequest { Spans = "66002001-66002005,66002007", Level = 200 }).IsValid.ShouldBeTrue();
        validator.Validate(new GetPassageTextRequest { Spans = "", Level = 200 }).IsValid.ShouldBeFalse();
        validator.Validate(new GetPassageTextRequest { Spans = "66002001", Level = 1001 }).IsValid.ShouldBeFalse();
    }
}
