using UnshackledWord.Application.Features.CrossReferences;
using UnshackledWord.Application.Features.TskCuration;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Tsk;

namespace UnshackledWord.Application.Tests.Features.TskCuration;

/// <summary>Roadmap 0024.3: request validation, the service's outcomes and the display order of curated links.</summary>
public class TskCurationTests
{
    private const int Gen11 = 1_001_001;
    private const int Gen12 = 1_001_002;

    private static TskCrossReference Row(int source, int start, int? end = null, string? rating = null, string? comment = null, string scope = "x") =>
        new() { SourceHebRefId = source, Scope = scope, TargetStartHebRefId = start, TargetEndHebRefId = end, Rating = rating, Comment = comment };

    private static SaveTskCurationRequest Request(int source = Gen11, int start = 19_033_006, int? end = null, string? rating = null, string? comment = null) =>
        new() { SourceHebRefId = source, TargetStartHebRefId = start, TargetEndHebRefId = end, Rating = rating, Comment = comment };

    // --- validator ---

    [Theory]
    [InlineData(null, null)]
    [InlineData("highlight", null)]
    [InlineData("hide", "Kommentar")]
    [InlineData(" highlight ", "   ")]
    [InlineData("", "")]
    public void Validator_AcceptsRatingsAndComments(string? rating, string? comment)
    {
        new SaveTskCurationRequestValidator().Validate(Request(rating: rating, comment: comment)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validator_AcceptsARangeInOneChapter_AndExactly500CommentCharactersAfterTrimming()
    {
        new SaveTskCurationRequestValidator().Validate(Request(start: 20_008_022, end: 20_008_024, comment: "  " + new string('a', 500) + "  ")).IsValid.ShouldBeTrue();
    }

    public static TheoryData<string, SaveTskCurationRequest, string> Refused => new()
    {
        { "unknown rating", Request(rating: "star"), "Rating" },
        { "rating in other case", Request(rating: "Highlight"), "Rating" },
        { "comment of 501 characters", Request(comment: new string('a', 501)), "Comment" },
        { "source without verse", Request(source: 1_001_000), "SourceHebRefId" },
        { "source book 67", Request(source: 67_001_001), "SourceHebRefId" },
        { "target without chapter", Request(start: 19_000_006), "TargetStartHebRefId" },
        { "end before start", Request(start: 20_008_024, end: 20_008_022), "TargetEndHebRefId" },
        { "end equals start", Request(start: 20_008_022, end: 20_008_022), "TargetEndHebRefId" },
        { "range across chapters", Request(start: 20_008_022, end: 20_009_001), "TargetEndHebRefId" },
    };

    [Theory]
    [MemberData(nameof(Refused))]
    public void Validator_RefusesMalformedRequests(string name, SaveTskCurationRequest request, string property)
    {
        var result = new SaveTskCurationRequestValidator().Validate(request);

        result.IsValid.ShouldBeFalse(name);
        result.Errors.ShouldContain(e => e.PropertyName == property, name);
    }

    // --- service ---

    [Fact]
    public async Task Save_NonMaintainer_IsForbiddenAndWritesNothing()
    {
        var repository = Substitute.For<ITskCurationRepository>();

        var outcome = await new TskCurationService(repository).SaveAsync(7, canWrite: false, Request(rating: TskRatings.Highlight), TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(TskCurationStatus.Forbidden);
        await repository.DidNotReceive().TskLinkExistsAsync(Arg.Any<TskLinkKey>(), Arg.Any<CancellationToken>());
        await repository.DidNotReceive().ApplyAsync(Arg.Any<TskLinkKey>(), Arg.Any<Func<TskCurationState, TskCurationState>>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_UnknownKey_IsNotFound()
    {
        var repository = Substitute.For<ITskCurationRepository>();
        repository.TskLinkExistsAsync(Arg.Any<TskLinkKey>(), Arg.Any<CancellationToken>()).Returns(false);

        var outcome = await new TskCurationService(repository).SaveAsync(7, canWrite: true, Request(start: 19_033_007, rating: TskRatings.Highlight), TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(TskCurationStatus.NotFound);
        await repository.DidNotReceive().ApplyAsync(Arg.Any<TskLinkKey>(), Arg.Any<Func<TskCurationState, TskCurationState>>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_TrimsTheComment_KeepsTheWordLink_AndWritesAsEdit()
    {
        var repository = Substitute.For<ITskCurationRepository>();
        repository.TskLinkExistsAsync(Arg.Any<TskLinkKey>(), Arg.Any<CancellationToken>()).Returns(true);
        Func<TskCurationState, TskCurationState>? change = null;
        repository.ApplyAsync(Arg.Any<TskLinkKey>(), Arg.Do<Func<TskCurationState, TskCurationState>>(c => change = c), 7, TskCurationSources.Edit, Arg.Any<CancellationToken>())
            .Returns(ci => new TskCurationRecord(ci.Arg<TskLinkKey>(), change!(TskCurationState.Plain), DateTimeOffset.UnixEpoch, 7));

        var outcome = await new TskCurationService(repository).SaveAsync(7, canWrite: true, Request(rating: " highlight ", comment: "  Schöpfung durch das Wort \n"), TestContext.Current.CancellationToken);

        outcome.Status.ShouldBe(TskCurationStatus.Ok);
        outcome.Response!.Rating.ShouldBe(TskRatings.Highlight);
        outcome.Response.Comment.ShouldBe("Schöpfung durch das Wort");
        outcome.Response.Origin.ShouldBe(TskOrigins.Tsk);
        outcome.Response.TargetStartHebRefId.ShouldBe(19_033_006);
        var linked = TskCurationState.Plain with { StartPosition = 2, EndPosition = 2, QuotedText = "Anfang" };
        change!(linked).ShouldBe(linked with { Rating = TskRatings.Highlight, Comment = "Schöpfung durch das Wort" });
    }

    [Fact]
    public async Task Save_BackToNeutral_ReturnsTheKeyWithoutCuration()
    {
        var repository = Substitute.For<ITskCurationRepository>();
        repository.TskLinkExistsAsync(Arg.Any<TskLinkKey>(), Arg.Any<CancellationToken>()).Returns(true);
        repository.ApplyAsync(Arg.Any<TskLinkKey>(), Arg.Any<Func<TskCurationState, TskCurationState>>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((TskCurationRecord?)null);

        var outcome = await new TskCurationService(repository).SaveAsync(7, canWrite: true, Request(comment: "   "), TestContext.Current.CancellationToken);

        outcome.Response!.Rating.ShouldBeNull();
        outcome.Response.Comment.ShouldBeNull();
        outcome.Response.UpdatedAt.ShouldBeNull();
        outcome.Response.Origin.ShouldBe(TskOrigins.Tsk);
    }

    [Fact]
    public void State_IsDefaultOnlyWithoutRatingCommentAndWordLink()
    {
        TskCurationState.Plain.IsDefault.ShouldBeTrue();
        (TskCurationState.Plain with { Rating = TskRatings.Hide }).IsDefault.ShouldBeFalse();
        (TskCurationState.Plain with { Comment = "x" }).IsDefault.ShouldBeFalse();
        (TskCurationState.Plain with { StartPosition = 1, EndPosition = 1, QuotedText = "Im" }).IsDefault.ShouldBeFalse();
        (TskCurationState.Plain with { Origin = TskOrigins.Added }).IsDefault.ShouldBeFalse();
    }

    // --- display order ---

    [Fact]
    public void Arrange_DropsHidden_AndPutsHighlightedFirstPerVerseInTskOrder()
    {
        var arranged = CrossReferenceCuration.Arrange(
        [
            Row(Gen11, 1), Row(Gen11, 2, rating: TskRatings.Highlight), Row(Gen11, 3), Row(Gen11, 4, rating: TskRatings.Highlight), Row(Gen11, 5, rating: TskRatings.Hide),
            Row(Gen12, 6), Row(Gen12, 7, rating: TskRatings.Hide), Row(Gen12, 8, rating: TskRatings.Highlight),
        ]);

        arranged.Select(r => (r.SourceHebRefId, r.TargetStartHebRefId)).ShouldBe(
        [
            (Gen11, 2), (Gen11, 4), (Gen11, 1), (Gen11, 3),
            (Gen12, 8), (Gen12, 6),
        ]);
    }

    [Fact]
    public void Arrange_IncludeHidden_KeepsHiddenAtTheirTskPlace()
    {
        CrossReferenceCuration.Arrange([Row(Gen11, 1), Row(Gen11, 2, rating: TskRatings.Hide), Row(Gen11, 3, rating: TskRatings.Highlight)], includeHidden: true)
            .Select(r => r.TargetStartHebRefId).ShouldBe([3, 1, 2]);
    }

    [Fact]
    public async Task ChapterCrossReferences_CarryRatingAndComment_HighlightedFirst_DedupFirstWins()
    {
        var tsk = Substitute.For<ITskRepository>();
        tsk.GetOutgoingAsync(1_001_000, 1_001_999, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(
        [
            Row(Gen11, 20_008_022, 20_008_024, scope: "beginning"),
            Row(Gen11, 19_033_006, scope: "God", rating: TskRatings.Highlight, comment: "Schöpfung durch das Wort"),
            Row(Gen11, 19_033_006, scope: "word", rating: TskRatings.Highlight, comment: "Schöpfung durch das Wort"),
            Row(Gen12, 18_026_007),
            Row(Gen12, 34_002_011, rating: TskRatings.Hide),
        ]);
        var service = new ChapterCrossReferenceService(tsk);

        var reader = await service.GetAsync(1, 1, token: TestContext.Current.CancellationToken);
        var curator = await service.GetAsync(1, 1, includeHidden: true, TestContext.Current.CancellationToken);

        var verse1 = reader.Verses[0].References;
        verse1.Count.ShouldBe(2);
        (verse1[0].BibleBookId, verse1[0].Chapter, verse1[0].Verse, verse1[0].Keyword).ShouldBe((19, 33, 6, "God"));
        verse1[0].Rating.ShouldBe(TskRatings.Highlight);
        verse1[0].Comment.ShouldBe("Schöpfung durch das Wort");
        verse1[1].Rating.ShouldBeNull();
        reader.Verses[1].References.ShouldHaveSingleItem().BibleBookId.ShouldBe(18);
        curator.Verses[1].References.Select(r => (r.BibleBookId, r.Rating)).ShouldBe([(18, (string?)null), (34, TskRatings.Hide)]);
    }
}
