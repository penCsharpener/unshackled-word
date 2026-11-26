using UnshackledWord.Application.Features.GrammarSearch;
using UnshackledWord.Domain.Models.Grammar;

namespace UnshackledWord.Application.Tests.Features.GrammarSearch;

/// <summary>Roadmap 0029.2: chapter matches and neighbour chapters of <see cref="GrammarSearchService"/> with a substituted repository.</summary>
public class GrammarChapterSearchTests
{
    private const int Matt24Start = 40_024_000;
    private static readonly GrammarLemma Agape = new("G0026", "ἀγάπη", "agape", "love");

    private readonly IGrammarSearchRepository _repository = Substitute.For<IGrammarSearchRepository>();
    private readonly GrammarSearchService _service;

    public GrammarChapterSearchTests()
    {
        var words = Substitute.For<IGrammarWordReader>();
        words.ReadGreekAsync("agape", Arg.Any<GrammarWordMode>(), Arg.Any<CancellationToken>()).Returns([Agape]);
        words.ReadGreekAsync("unknown", Arg.Any<GrammarWordMode>(), Arg.Any<CancellationToken>()).Returns([]);

        var index = Substitute.For<IMorphologyCodeIndex>();
        index.GetGreekAsync(Arg.Any<CancellationToken>()).Returns(new[] { "N-NSF", "N-ASF" }.Select(c => new MorphologyCodeEntry(c, GreekMorphologyDecoder.Decode(c))).ToList());

        _repository.ReadMappingsAsync(Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IReadOnlyList<int>>().Select(id => new GrammarMappingRow { StepWordId = id, PositionInVerse = id % 100 }).ToList());
        _service = new GrammarSearchService(words, new GrammarFilterResolver(index), _repository);
    }

    private static GrammarMatchRow Match(int slot, int verse, int wordId) => new() { Slot = slot, HebRefId = Matt24Start + verse, WordId = wordId };

    [Fact]
    public async Task Chapter_WordOnlyQuery_ReturnsPositionsAndWordIds()
    {
        _repository.ReadMatchesInRangeAsync(Arg.Any<IReadOnlyList<GrammarSlotQuery>>(), Matt24Start, Matt24Start + 999, Arg.Any<CancellationToken>())
            .Returns([Match(0, 12, 1209), Match(0, 12, 1203), Match(0, 30, 3007)]);

        var result = await _service.ChapterAsync("agape", 40, 24, TestContext.Current.CancellationToken);

        var response = result.Response.ShouldNotBeNull();
        response.Total.ShouldBe(3);
        response.Verses.Select(v => v.Verse).ShouldBe([12, 30]);
        response.Verses[0].OriginalWordIds.ShouldBe([1203, 1209]);
        response.Verses[0].GermanPositions.ShouldBe([3, 9]);
    }

    [Fact]
    public async Task Chapter_TwoSlotsSharingTheOnlyWord_IsNoHit()
    {
        _repository.ReadMatchesInRangeAsync(Arg.Any<IReadOnlyList<GrammarSlotQuery>>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([Match(0, 12, 1209), Match(1, 12, 1209), Match(0, 13, 1301), Match(1, 13, 1302)]);

        var result = await _service.ChapterAsync("agape agape", 40, 24, TestContext.Current.CancellationToken);

        result.Response!.Verses.Select(v => v.Verse).ShouldBe([13]);
    }

    [Fact]
    public async Task Chapter_FilterWithoutAnyCode_ReturnsEmptyWithoutQuery()
    {
        var result = await _service.ChapterAsync("Tempus:aor", 40, 24, TestContext.Current.CancellationToken);

        result.Response!.Verses.ShouldBeEmpty();
        await _repository.DidNotReceiveWithAnyArgs().ReadMatchesInRangeAsync(default!, default, default, default);
    }

    [Theory]
    [InlineData("", GrammarErrorCodes.EmptyQuery)]
    [InlineData("unknown", GrammarErrorCodes.UnknownWord)]
    [InlineData("Kasus:xyz", GrammarErrorCodes.UnknownValue)]
    public async Task ParseErrors_AreReturnedByBothOperations(string query, string code)
    {
        var chapter = await _service.ChapterAsync(query, 40, 24, TestContext.Current.CancellationToken);
        var neighbours = await _service.NeighboursAsync(query, 40, 24, TestContext.Current.CancellationToken);

        chapter.Error!.Code.ShouldBe(code);
        neighbours.Error!.Code.ShouldBe(code);
    }

    [Fact]
    public async Task Neighbours_NamesNearestChapterEachWay_WithBoundariesOfTheGivenChapter()
    {
        _repository.FindVersesNearAsync(Arg.Any<IReadOnlyList<GrammarSlotQuery>>(), Matt24Start, true, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new GrammarVerseRow { HebRefId = 40_005_044 }]);
        _repository.FindVersesNearAsync(Arg.Any<IReadOnlyList<GrammarSlotQuery>>(), Matt24Start + 999, false, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new GrammarVerseRow { HebRefId = 42_001_007 }]);

        var result = await _service.NeighboursAsync("agape", 40, 24, TestContext.Current.CancellationToken);

        result.Response!.Previous.ShouldNotBeNull().Chapter.ShouldBe(5);
        result.Response.Next.ShouldNotBeNull().BibleBookId.ShouldBe(42);
        result.Response.Next.Chapter.ShouldBe(1);
    }

    [Fact]
    public async Task Neighbours_RejectedSharedVerse_FallsThroughToTheNextOne()
    {
        _repository.FindVersesNearAsync(Arg.Any<IReadOnlyList<GrammarSlotQuery>>(), Matt24Start, true, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);
        _repository.FindVersesNearAsync(Arg.Any<IReadOnlyList<GrammarSlotQuery>>(), Matt24Start + 999, false, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([new GrammarVerseRow { HebRefId = 40_025_001, Shared = true }, new GrammarVerseRow { HebRefId = 40_026_001 }]);
        _repository.ReadMatchesAsync(Arg.Any<IReadOnlyList<GrammarSlotQuery>>(), Arg.Any<IReadOnlyList<int>>(), Arg.Any<CancellationToken>())
            .Returns([new GrammarMatchRow { Slot = 0, HebRefId = 40_025_001, WordId = 1 }, new GrammarMatchRow { Slot = 1, HebRefId = 40_025_001, WordId = 1 }]);

        var result = await _service.NeighboursAsync("agape agape", 40, 24, TestContext.Current.CancellationToken);

        result.Response!.Previous.ShouldBeNull();
        result.Response.Next!.Chapter.ShouldBe(26);
    }
}
