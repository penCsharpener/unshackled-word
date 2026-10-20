using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Tooling.McpServer.Tools;

namespace UnshackledWord.Tooling.McpServer.Tests;

public class OriginalTextToolsTests
{
    private static StepBibleVerse Verse(int refId) => new() { HebRefId = refId, VerseText = "בראשית ברא", LemmaText = "ראשית ברא" };

    [Fact]
    public async Task GetOriginalText_ReturnsHebrewVerses()
    {
        var repo = Substitute.For<IStepBibleVerseRepository>();
        repo.GetByHebRefIdRangeAsync(1001001, 1001002, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<StepBibleVerse> { Verse(1001001), Verse(1001002) });

        var text = await OriginalTextTools.GetOriginalText(repo, "1Mo 1,1-2");

        text.ShouldContain("1Mo 1,1  בראשית ברא");
        text.ShouldContain("1Mo 1,2  ");
        text.ShouldNotContain("Lemmas");
    }

    [Fact]
    public async Task GetOriginalText_IncludeLemmas_AddsLemmaLine()
    {
        var repo = Substitute.For<IStepBibleVerseRepository>();
        repo.GetByHebRefIdRangeAsync(default, default, default).ReturnsForAnyArgs(new List<StepBibleVerse> { Verse(1001001) });

        var text = await OriginalTextTools.GetOriginalText(repo, "1Mo 1,1", includeLemmas: true);

        text.ShouldContain("  Lemmas: ראשית ברא");
    }

    [Fact]
    public async Task GetOriginalText_InvalidEmptyOrTooLong_ReturnsText()
    {
        var repo = Substitute.For<IStepBibleVerseRepository>();
        repo.GetByHebRefIdRangeAsync(default, default, default).ReturnsForAnyArgs(new List<StepBibleVerse>());
        (await OriginalTextTools.GetOriginalText(repo, "Blub 1")).ShouldContain("Unknown book");
        (await OriginalTextTools.GetOriginalText(repo, "Joh 99")).ShouldContain("No verses found");

        repo.GetByHebRefIdRangeAsync(default, default, default).ReturnsForAnyArgs(
            Enumerable.Range(1, OriginalTextTools.MaxVerses + 1).Select(i => Verse(19119000 + i)).ToList());
        (await OriginalTextTools.GetOriginalText(repo, "Ps 119")).ShouldContain("Truncated");
    }
}
