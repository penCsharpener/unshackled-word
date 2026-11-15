using UnshackledWord.Application.Features.Revision;
using UnshackledWord.Application.Features.WordDetail;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetWordDetail;

namespace UnshackledWord.Application.Tests.Features.Revision;

public class RevisionWordDetailServiceTests
{
    private readonly IRevisionWordDetailRepository _repository = Substitute.For<IRevisionWordDetailRepository>();
    private readonly IWordDetailService _wordDetail = Substitute.For<IWordDetailService>();

    private RevisionWordDetailService Service() => new(_repository, _wordDetail);

    private static RevisionWordHeaderRow Header(string change = "changed", int book = 43) => new()
    {
        Id = 12, BibleBookId = book, Chapter = 3, Verse = 16, HebRefId = book * 1_000_000 + 3016, PositionInVerse = 11, WordInContext = "einzigen",
        Level = 200, Change = change, BaseWord = change == "changed" ? "eingeborenen" : null, GermanLemma = change == "same" ? "einzig" : null,
    };

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenTheWordDoesNotExist()
    {
        (await Service().GetAsync(1)).ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_BuildsFromGreekRowsOfTheVariant_AndAddsTheRevisionInfo()
    {
        _repository.GetWordAsync(12, Arg.Any<CancellationToken>()).Returns(Header());
        _repository.GetGreekReadingsAsync(12, Arg.Any<CancellationToken>()).Returns([new GreekReadingRow { StepWordId = 5, MappingOrigin = "carried" }]);
        _repository.GetCoMappedWordsAsync(12, Arg.Any<CancellationToken>()).Returns([]);
        _wordDetail.BuildAsync(Arg.Any<ElbWordRow>(), Arg.Any<IReadOnlyList<ReadingRowBase>>(), Arg.Any<IReadOnlyList<CoMappedWordRow>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetWordDetailResponse { WordId = 12 });

        var response = (await Service().GetAsync(12))!;

        response.Revision!.Level.ShouldBe(200);
        response.Revision.Change.ShouldBe("changed");
        response.Revision.BaseWord.ShouldBe("eingeborenen");
        await _wordDetail.Received(1).BuildAsync(
            Arg.Is<ElbWordRow>(w => w.Id == 12 && w.BibleBookId == 43 && w.HebRefId == 43003016 && w.WordInContext == "einzigen"),
            Arg.Is<IReadOnlyList<ReadingRowBase>>(r => r.Count == 1),
            Arg.Any<IReadOnlyList<CoMappedWordRow>>(),
            "carried",
            Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().GetHebrewReadingsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_InsertedWordWithoutRows_HasNoReadingsAndSkipsTheCoMappedQuery()
    {
        _repository.GetWordAsync(13, Arg.Any<CancellationToken>()).Returns(Header("new"));
        _repository.GetGreekReadingsAsync(13, Arg.Any<CancellationToken>()).Returns([]);
        _wordDetail.BuildAsync(Arg.Any<ElbWordRow>(), Arg.Any<IReadOnlyList<ReadingRowBase>>(), Arg.Any<IReadOnlyList<CoMappedWordRow>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetWordDetailResponse { WordId = 13 });

        var response = (await Service().GetAsync(13))!;

        response.Readings.ShouldBeEmpty();
        response.Revision!.Change.ShouldBe("new");
        await _repository.DidNotReceive().GetCoMappedWordsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("carried", "manual", "manual")]
    [InlineData("carried", "seed", "seed")]
    [InlineData("carried", "carried", "carried")]
    public async Task GetAsync_ReportsTheStrongestOriginOfTheRowsAsMappingSource(string a, string b, string expected)
    {
        _repository.GetWordAsync(12, Arg.Any<CancellationToken>()).Returns(Header("same", book: 1));
        _repository.GetHebrewReadingsAsync(12, Arg.Any<CancellationToken>())
            .Returns([new HebrewReadingRow { StepWordId = 1, MappingOrigin = a }, new HebrewReadingRow { StepWordId = 2, MappingOrigin = b }]);
        _repository.GetCoMappedWordsAsync(12, Arg.Any<CancellationToken>()).Returns([]);
        _wordDetail.BuildAsync(Arg.Any<ElbWordRow>(), Arg.Any<IReadOnlyList<ReadingRowBase>>(), Arg.Any<IReadOnlyList<CoMappedWordRow>>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GetWordDetailResponse());

        await Service().GetAsync(12);

        await _wordDetail.Received(1).BuildAsync(Arg.Is<ElbWordRow>(w => w.GermanLemma == "einzig"), Arg.Any<IReadOnlyList<ReadingRowBase>>(), Arg.Any<IReadOnlyList<CoMappedWordRow>>(), expected, Arg.Any<CancellationToken>());
    }
}
