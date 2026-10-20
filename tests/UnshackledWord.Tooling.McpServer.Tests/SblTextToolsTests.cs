using UnshackledWord.Application.Repositories;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Tooling.McpServer.Tools;

namespace UnshackledWord.Tooling.McpServer.Tests;

public class SblTextToolsTests
{
    [Fact]
    public async Task GetSblText_ReturnsGreekVerses()
    {
        var repo = Substitute.For<ISblTextRepository>();
        repo.GetByRefIdRangeAsync(43003016, 43003016, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<SblVerse> { new() { LxxRefId = 43003016, VerseText = "Οὕτως γὰρ ἠγάπησεν ὁ θεὸς τὸν κόσμον" } });

        var text = await SblTextTools.GetSblText(repo, "Joh 3,16", CancellationToken.None);

        text.ShouldBe("Joh 3,16  Οὕτως γὰρ ἠγάπησεν ὁ θεὸς τὸν κόσμον");
    }

    [Fact]
    public async Task GetSblText_OldTestament_ReturnsErrorWithoutQuery()
    {
        var repo = Substitute.For<ISblTextRepository>();

        var text = await SblTextTools.GetSblText(repo, "1Mo 1,1", CancellationToken.None);

        text.ShouldContain("New Testament only");
        await repo.DidNotReceiveWithAnyArgs().GetByRefIdRangeAsync(default, default, default);
    }

    [Fact]
    public async Task GetSblText_InvalidOrEmpty_ReturnsText()
    {
        var repo = Substitute.For<ISblTextRepository>();
        repo.GetByRefIdRangeAsync(default, default, default).ReturnsForAnyArgs(new List<SblVerse>());

        (await SblTextTools.GetSblText(repo, "Blub 1", CancellationToken.None)).ShouldContain("Unknown book");
        (await SblTextTools.GetSblText(repo, "Joh 99", CancellationToken.None)).ShouldContain("No verses found");
    }
}
