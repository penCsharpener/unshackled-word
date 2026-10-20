using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Tooling.McpServer.Tools;

namespace UnshackledWord.Tooling.McpServer.Tests;

public class PassageToolsTests
{
    [Fact]
    public async Task GetPassage_ReturnsVersesWithGermanReferences()
    {
        var repo = Substitute.For<IElb1871VerseRepository>();
        repo.GetByHebRefIdRangeAsync(43003016, 43003017, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<ElbVerse>
            {
                new() { HebRefId = 43003016, VerseText = "Denn also hat Gott die Welt geliebt" },
                new() { HebRefId = 43003017, VerseText = "Denn Gott hat seinen Sohn nicht gesandt" },
            });

        var text = await PassageTools.GetPassage(repo, "Joh 3,16-17", CancellationToken.None);

        text.ShouldContain("Joh 3,16  Denn also hat Gott die Welt geliebt");
        text.ShouldContain("Joh 3,17  Denn Gott");
    }

    [Fact]
    public async Task GetPassage_InvalidReference_ReturnsErrorText()
    {
        var repo = Substitute.For<IElb1871VerseRepository>();

        var text = await PassageTools.GetPassage(repo, "Blub 1,1", CancellationToken.None);

        text.ShouldContain("Unknown book");
        await repo.DidNotReceiveWithAnyArgs().GetByHebRefIdRangeAsync(default, default, default);
    }

    [Fact]
    public async Task GetPassage_NoRows_ReturnsNotFound()
    {
        var repo = Substitute.For<IElb1871VerseRepository>();
        repo.GetByHebRefIdRangeAsync(default, default, default).ReturnsForAnyArgs(new List<ElbVerse>());

        var text = await PassageTools.GetPassage(repo, "Joh 99", CancellationToken.None);

        text.ShouldContain("No verses found");
    }

    [Fact]
    public async Task GetPassage_TooManyVerses_IsTruncatedWithNote()
    {
        var repo = Substitute.For<IElb1871VerseRepository>();
        var rows = Enumerable.Range(1, PassageTools.MaxVerses + 1)
            .Select(i => new ElbVerse { HebRefId = 19119000 + i, VerseText = "x" }).ToList();
        repo.GetByHebRefIdRangeAsync(default, default, default).ReturnsForAnyArgs(rows);

        var text = await PassageTools.GetPassage(repo, "Ps 119", CancellationToken.None);

        text.ShouldContain("Truncated");
    }
}
