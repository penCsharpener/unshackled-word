using UnshackledWord.Application.Features.CrossReferences;
using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Tsk;
using UnshackledWord.Tooling.McpServer.Tools;

namespace UnshackledWord.Tooling.McpServer.Tests;

public class CrossReferenceToolsTests
{
    private static TskCrossReference Ref(int source, string scope, int start, int? end = null, string? rating = null, string? comment = null) =>
        new() { SourceHebRefId = source, Scope = scope, TargetStartHebRefId = start, TargetEndHebRefId = end, Rating = rating, Comment = comment };

    [Fact]
    public async Task GetCrossReferences_Curated_DropsHiddenAndListsHighlightedFirstWithComment()
    {
        var tsk = Substitute.For<ITskRepository>();
        var verses = Substitute.For<IElb1871VerseRepository>();
        tsk.GetOutgoingAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new List<TskCrossReference>
        {
            Ref(1001001, "God", 2020011),
            Ref(1001001, "God", 19033006, rating: TskRatings.Highlight, comment: "Schöpfung durch das Wort"),
            Ref(1001001, "God", 34002011, rating: TskRatings.Hide),
        });

        var text = await CrossReferenceTools.GetCrossReferences(tsk, verses, "1Mo 1,1", limit: 2, cancellationToken: TestContext.Current.CancellationToken);

        text.ShouldContain("  - Ps 33,6 [highlighted] (comment: Schöpfung durch das Wort)");
        text.IndexOf("Ps 33,6", StringComparison.Ordinal).ShouldBeLessThan(text.IndexOf("2Mo 20,11", StringComparison.Ordinal));
        text.ShouldNotContain(UnshackledWord.Domain.Models.BibleStructure.GermanReferenceFormatter.Format(34002011));
        text.ShouldNotContain("Truncated");
    }

    [Fact]
    public async Task GetCrossReferences_GroupsBySourceAndScope()
    {
        var tsk = Substitute.For<ITskRepository>();
        var verses = Substitute.For<IElb1871VerseRepository>();
        tsk.GetOutgoingAsync(43003016, 43003017, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(new List<TskCrossReference>
        {
            Ref(43003016, "God so loved", 62004009, 62004010),
            Ref(43003016, "God so loved", 45005008),
            Ref(43003017, "condemn", 43012047),
        });

        var text = await CrossReferenceTools.GetCrossReferences(tsk, verses, "Joh 3,16-17");

        text.ShouldContain("[Joh 3,16] God so loved");
        text.ShouldContain("  - 1Joh 4,9-10");
        text.ShouldContain("  - Röm 5,8");
        text.ShouldContain("[Joh 3,17] condemn");
        await verses.DidNotReceiveWithAnyArgs().GetByHebRefIdRangeAsync(default, default, default);
    }

    [Fact]
    public async Task GetCrossReferences_IncludeText_LoadsTargetText()
    {
        var tsk = Substitute.For<ITskRepository>();
        var verses = Substitute.For<IElb1871VerseRepository>();
        tsk.GetOutgoingAsync(default, default, default).ReturnsForAnyArgs(new List<TskCrossReference> { Ref(43003016, "x", 45005008) });
        verses.GetByHebRefIdRangeAsync(45005008, 45005008, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<ElbVerse> { new() { HebRefId = 45005008, VerseText = "Gott aber erweist" } });

        var text = await CrossReferenceTools.GetCrossReferences(tsk, verses, "Joh 3,16", includeText: true);

        text.ShouldContain("  - Röm 5,8: Gott aber erweist");
    }

    [Fact]
    public async Task GetCrossReferences_MoreThanLimit_IsTruncated()
    {
        var tsk = Substitute.For<ITskRepository>();
        var verses = Substitute.For<IElb1871VerseRepository>();
        tsk.GetOutgoingAsync(default, default, default).ReturnsForAnyArgs(
            Enumerable.Range(1, 3).Select(i => Ref(43003016, "x", 45005000 + i)).ToList());

        var text = await CrossReferenceTools.GetCrossReferences(tsk, verses, "Joh 3,16", limit: 2);

        await tsk.Received().GetOutgoingAsync(43003016, 43003016, ChapterCrossReferenceService.MaxRows, Arg.Any<CancellationToken>());
        text.ShouldContain("Röm 5,2");
        text.ShouldNotContain("Röm 5,3");
        text.ShouldContain("Truncated");
    }

    [Fact]
    public async Task GetCrossReferences_InvalidReferenceOrNoRows_ReturnsText()
    {
        var tsk = Substitute.For<ITskRepository>();
        var verses = Substitute.For<IElb1871VerseRepository>();
        tsk.GetOutgoingAsync(default, default, default).ReturnsForAnyArgs(new List<TskCrossReference>());

        (await CrossReferenceTools.GetCrossReferences(tsk, verses, "Blub 1")).ShouldContain("Unknown book");
        (await CrossReferenceTools.GetCrossReferences(tsk, verses, "Joh 3,16")).ShouldContain("No cross-references");
    }
}
