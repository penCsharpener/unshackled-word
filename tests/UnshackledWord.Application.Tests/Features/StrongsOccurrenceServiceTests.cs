using UnshackledWord.Application.Features.StrongsOccurrences;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetStrongsOccurrences;

namespace UnshackledWord.Application.Tests.Features;

public class StrongsOccurrenceServiceTests
{
    private static readonly StrongsOccurrenceFilter None = StrongsOccurrenceFilter.None;

    private readonly IStrongsLexiconRepository _lexicon = Substitute.For<IStrongsLexiconRepository>();
    private readonly IStrongsStatsRepository _stats = Substitute.For<IStrongsStatsRepository>();
    private readonly StrongsOccurrenceService _sut;

    public StrongsOccurrenceServiceTests()
    {
        _sut = new StrongsOccurrenceService(_lexicon, _stats);
    }

    private static bool Same(IReadOnlyList<string> actual, params string[] expected)
    {
        return actual.SequenceEqual(expected);
    }

    [Fact]
    public async Task GetAsync_Greek_NormalizesAndPages()
    {
        _stats.CountOccurrencesAsync(Arg.Is<IReadOnlyList<string>>(k => Same(k, "G0026")), false, None, Arg.Any<CancellationToken>()).Returns(116);
        _stats.GetOccurrencesAsync(Arg.Is<IReadOnlyList<string>>(k => Same(k, "G0026")), false, None, 50, 50, Arg.Any<CancellationToken>())
            .Returns([new StrongsOccurrenceRow { BibleBookId = 40, Chapter = 24, Verse = 12, Original = "ἀγάπη", MatchedPositions = [2], VersePositions = [1, 2], VerseWords = ["Die", "Liebe"] }]);

        var result = await _sut.GetAsync("g26", 2, 50, None);

        result!.Strongs.ShouldBe("G0026");
        result.Language.ShouldBe("greek");
        result.TotalItems.ShouldBe(116);
        result.TotalPages.ShouldBe(3);
        result.Items.Single().Keyword.Single().Text.ShouldBe("Liebe");
        result.Items.Single().Verse.ShouldBe(12);
    }

    [Fact]
    public async Task GetAsync_Hebrew_StripsInstanceSuffixAndKeepsRootLetter()
    {
        _stats.CountOccurrencesAsync(Arg.Is<IReadOnlyList<string>>(k => Same(k, "H1254A")), true, None, Arg.Any<CancellationToken>()).Returns(1);
        _stats.GetOccurrencesAsync(Arg.Is<IReadOnlyList<string>>(k => Same(k, "H1254A")), true, None, 0, 50, Arg.Any<CancellationToken>())
            .Returns([new StrongsOccurrenceRow { BibleBookId = 1, Chapter = 1, Verse = 1 }]);

        var result = await _sut.GetAsync("H1254A_B", 1, 50, None);

        result!.Language.ShouldBe("hebrew");
        result.Items.Single().Keyword.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAsync_NumberWithoutLetter_UsesEveryLexiconVariantAsKey()
    {
        _lexicon.GetEntriesAsync(Arg.Any<StrongsNumber>(), Arg.Any<CancellationToken>())
            .Returns([new LexiconEntry { Number = 3004, Extra = "G" }, new LexiconEntry { Number = 3004, Extra = "H" }]);
        _stats.CountOccurrencesAsync(Arg.Any<IReadOnlyList<string>>(), false, Arg.Any<StrongsOccurrenceFilter>(), Arg.Any<CancellationToken>()).Returns(0);

        await _sut.GetAsync("G3004", 1, 50, None);

        await _stats.Received(1).CountOccurrencesAsync(Arg.Is<IReadOnlyList<string>>(k => Same(k, "G3004", "G3004G", "G3004H")), false, None, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_BookAndRendering_ReachTheRepository()
    {
        var filter = new StrongsOccurrenceFilter(44, "versammlung");
        _stats.CountOccurrencesAsync(Arg.Any<IReadOnlyList<string>>(), false, filter, Arg.Any<CancellationToken>()).Returns(20);
        _stats.GetOccurrencesAsync(Arg.Any<IReadOnlyList<string>>(), false, filter, 0, 50, Arg.Any<CancellationToken>())
            .Returns([new StrongsOccurrenceRow { BibleBookId = 44, Chapter = 5, Verse = 11 }]);

        var result = await _sut.GetAsync("G1577", 1, 50, filter);

        result!.TotalItems.ShouldBe(20);
        result.Items.Single().BibleBookId.ShouldBe(44);
    }

    [Fact]
    public async Task GetAsync_PageBeyondTotal_ReturnsEmptyItemsWithoutQueryingRows()
    {
        _stats.CountOccurrencesAsync(Arg.Any<IReadOnlyList<string>>(), false, None, Arg.Any<CancellationToken>()).Returns(10);

        var result = await _sut.GetAsync("G26", 5, 50, None);

        result!.Items.ShouldBeEmpty();
        result.TotalItems.ShouldBe(10);
        await _stats.DidNotReceive().GetOccurrencesAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<bool>(), Arg.Any<StrongsOccurrenceFilter>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("26")]
    [InlineData("G0")]
    [InlineData("X12")]
    public async Task GetAsync_InvalidStrongs_ReturnsNull(string strongs)
    {
        (await _sut.GetAsync(strongs, 1, 50, None)).ShouldBeNull();
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 0)]
    public async Task GetAsync_InvalidPaging_ReturnsNull(int page, int pageSize)
    {
        (await _sut.GetAsync("G26", page, pageSize, None)).ShouldBeNull();
    }

    private static StrongsOccurrenceResponse Map(int[]? matched, params string[] words)
    {
        return StrongsOccurrenceService.ToResponse(new StrongsOccurrenceRow
        {
            BibleBookId = 43, Chapter = 1, Verse = 1, Original = "λόγος",
            MatchedPositions = matched,
            VersePositions = Enumerable.Range(1, words.Length).ToArray(),
            VerseWords = words
        });
    }

    [Fact]
    public void ToResponse_SplitsTheVerseAroundTheMatchedWord()
    {
        var item = Map([8], "Im", "Anfang", "war", "das", "Wort,", "und", "das", "Wort", "war");
        item.Before.Select(t => t.Text).ShouldBe(["Im", "Anfang", "war", "das", "Wort,", "und", "das"]);
        item.Keyword.ShouldHaveSingleItem().Text.ShouldBe("Wort");
        item.Keyword[0].Matched.ShouldBeTrue();
        item.KeywordSuffix.ShouldBeNull();
        item.After.Select(t => t.Text).ShouldBe(["war"]);
        item.Before.ShouldAllBe(t => !t.Matched);
    }

    [Fact]
    public void ToResponse_KeywordSpansFirstToLastMatchedAndKeepsGapWordsUnmatched()
    {
        var item = Map([1, 3], "mit", "bösen", "Worten", "wider");

        item.Keyword.Select(t => (t.Text, t.Matched)).ShouldBe([("mit", true), ("bösen", false), ("Worten", true)]);
        item.Before.ShouldBeEmpty();
        item.After.Select(t => t.Text).ShouldBe(["wider"]);
    }

    [Fact]
    public void ToResponse_MovesTrailingPunctuationOutOfTheKeyword()
    {
        var item = Map([2], "das", "Wort,", "und");

        item.Keyword.Single().Text.ShouldBe("Wort");
        item.KeywordSuffix.ShouldBe(",");
    }

    [Fact]
    public void ToResponse_UnmappedWordHasEmptyKeywordAndWholeVerseAfter()
    {
        var item = Map(null, "Im", "Anfang");

        item.Keyword.ShouldBeEmpty();
        item.Before.ShouldBeEmpty();
        item.After.Select(t => t.Text).ShouldBe(["Im", "Anfang"]);
    }

    [Fact]
    public void ToResponse_MissingVerseGivesEmptyLists()
    {
        var item = StrongsOccurrenceService.ToResponse(new StrongsOccurrenceRow { BibleBookId = 1, Chapter = 1, Verse = 1 });

        item.Before.ShouldBeEmpty();
        item.Keyword.ShouldBeEmpty();
        item.After.ShouldBeEmpty();
    }
}
