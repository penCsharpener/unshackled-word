using UnshackledWord.Application.Features.Notes;
using UnshackledWord.Domain.WebApi.Notes;

namespace UnshackledWord.Application.Tests.Features.Notes;

public class NoteServiceTests
{
    private const int UserId = 7;
    private const int Rom828 = 45008028;

    private readonly INoteRepository _repository = Substitute.For<INoteRepository>();

    private NoteService Service() => new(_repository);

    private static NoteRecord Row(Guid id, int userId = UserId) => new()
    {
        Id = id,
        UserId = userId,
        AuthorDisplayName = "Anna",
        Kind = NoteKinds.Verse,
        BibleBookId = 45,
        Chapter = 8,
        StartHebRefId = Rom828,
        EndHebRefId = Rom828,
        BodyMarkdown = "**alles**",
        Visibility = NoteVisibilities.Private,
        PublicState = NotePublicStates.None,
    };

    private static CreateNoteRequest VerseRequest(int start = Rom828) => new()
    {
        Anchor = new NoteAnchorDto { Kind = NoteKinds.Verse, BookId = 45, Chapter = 8, StartHebRefId = start },
        BodyMarkdown = "**alles**",
    };

    [Fact]
    public async Task CreateAsync_ShouldStoreANormalisedVerseAnchorForTheCaller_WhenTheVerseExists()
    {
        var id = Guid.NewGuid();
        _repository.VerseExistsAsync(Rom828, Arg.Any<CancellationToken>()).Returns(true);
        _repository.InsertAsync(UserId, Arg.Any<NoteAnchor>(), "**alles**", Arg.Any<CancellationToken>()).Returns(id);
        _repository.GetAsync(id, UserId, Arg.Any<CancellationToken>()).Returns(Row(id));

        var outcome = await Service().CreateAsync(UserId, VerseRequest());

        outcome.Status.ShouldBe(NoteStatus.Ok);
        await _repository.Received(1).InsertAsync(UserId, new NoteAnchor(NoteKinds.Verse, 45, 8, Rom828, Rom828, null, null, null), "**alles**", Arg.Any<CancellationToken>());
        var response = outcome.Response!;
        response.IsOwn.ShouldBeTrue();
        response.AnchorState.ShouldBe(NoteAnchorStates.Ok);
        response.BodyLinked.ShouldBe("**alles**");
        response.Anchor.StartVerse.ShouldBe(28);
        response.Anchor.EndVerse.ShouldBe(28);
        response.AuthorDisplayName.ShouldBe("Anna");
    }

    [Fact]
    public async Task CreateAsync_ShouldRefuse_WhenTheVerseIsNotInElb1871()
    {
        _repository.VerseExistsAsync(45008040, Arg.Any<CancellationToken>()).Returns(false);

        var outcome = await Service().CreateAsync(UserId, VerseRequest(45008040));

        outcome.Status.ShouldBe(NoteStatus.Invalid);
        await _repository.DidNotReceiveWithAnyArgs().InsertAsync(default, default!, default!, default);
    }

    [Theory]
    [InlineData(true, NoteStatus.Ok)]
    [InlineData(false, NoteStatus.Invalid)]
    public async Task CreateAsync_ShouldCheckTheChapter_WhenChapterNote(bool exists, NoteStatus expected)
    {
        var id = Guid.NewGuid();
        _repository.ChapterExistsAsync(45, 8, Arg.Any<CancellationToken>()).Returns(exists);
        _repository.InsertAsync(UserId, Arg.Any<NoteAnchor>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(id);
        _repository.GetAsync(id, UserId, Arg.Any<CancellationToken>()).Returns(Row(id));

        var outcome = await Service().CreateAsync(UserId, new CreateNoteRequest { Anchor = new NoteAnchorDto { Kind = NoteKinds.Chapter, BookId = 45, Chapter = 8 }, BodyMarkdown = "x" });

        outcome.Status.ShouldBe(expected);
    }

    [Fact]
    public async Task UpdateAsync_ShouldReturnNotFound_WhenTheNoteIsNotTheCallers()
    {
        var id = Guid.NewGuid();
        _repository.UpdateAsync(id, UserId, null, "neu", Arg.Any<CancellationToken>()).Returns(false);

        var outcome = await Service().UpdateAsync(UserId, new UpdateNoteRequest { Id = id, BodyMarkdown = "neu" });

        outcome.Status.ShouldBe(NoteStatus.NotFound);
    }

    [Fact]
    public async Task UpdateAsync_ShouldKeepTheAnchor_WhenNoAnchorIsGiven()
    {
        var id = Guid.NewGuid();
        _repository.UpdateAsync(id, UserId, null, "neu", Arg.Any<CancellationToken>()).Returns(true);
        _repository.GetAsync(id, UserId, Arg.Any<CancellationToken>()).Returns(Row(id));

        var outcome = await Service().UpdateAsync(UserId, new UpdateNoteRequest { Id = id, BodyMarkdown = "neu" });

        outcome.Status.ShouldBe(NoteStatus.Ok);
        await _repository.DidNotReceiveWithAnyArgs().VerseExistsAsync(default, default);
    }

    [Fact]
    public async Task UpdateAsync_ShouldRefuseAndNotWrite_WhenTheNewVerseDoesNotExist()
    {
        var id = Guid.NewGuid();
        _repository.VerseExistsAsync(45008040, Arg.Any<CancellationToken>()).Returns(false);

        var outcome = await Service().UpdateAsync(UserId, new UpdateNoteRequest { Id = id, BodyMarkdown = "neu", Anchor = VerseRequest(45008040).Anchor });

        outcome.Status.ShouldBe(NoteStatus.Invalid);
        await _repository.DidNotReceiveWithAnyArgs().UpdateAsync(default, default, default, default!, default);
    }

    [Theory]
    [InlineData(UserId, true)]
    [InlineData(8, false)]
    [InlineData(null, false)]
    public async Task ListChapterAsync_ShouldMarkOwnNotes_WhenViewerIsTheAuthor(int? viewerId, bool isOwn)
    {
        var id = Guid.NewGuid();
        _repository.ListChapterAsync(45, 8, viewerId, true, Arg.Any<CancellationToken>()).Returns([Row(id)]);

        var response = await Service().ListChapterAsync(viewerId, 45, 8, true);

        response.Notes.ShouldHaveSingleItem().IsOwn.ShouldBe(isOwn);
    }
}
