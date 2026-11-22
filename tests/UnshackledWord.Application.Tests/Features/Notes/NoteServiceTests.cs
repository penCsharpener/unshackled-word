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

    private static NoteWordRow Word(int position, string text) => new() { HebRefId = Rom828, PositionInVerse = position, WordInContext = text };

    private static CreateNoteRequest WordsRequest(int from, int to, string? clientQuote = "gefälscht") => new()
    {
        Anchor = new NoteAnchorDto { Kind = NoteKinds.Words, BookId = 45, Chapter = 8, StartHebRefId = Rom828, StartPosition = from, EndPosition = to, QuotedText = clientQuote },
        BodyMarkdown = "x",
    };

    private void GivenVerseWords() =>
        _repository.GetElbWordsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns([Word(1, "Wir"), Word(2, "wissen"), Word(3, "aber,"), Word(4, "dass")]);

    [Fact]
    public async Task CreateAsync_ShouldFillTheQuoteFromElb1871_WhenWordsNote()
    {
        var id = Guid.NewGuid();
        GivenVerseWords();
        _repository.InsertAsync(UserId, Arg.Any<NoteAnchor>(), "x", Arg.Any<CancellationToken>()).Returns(id);
        _repository.GetAsync(id, UserId, Arg.Any<CancellationToken>()).Returns(Row(id));

        var outcome = await Service().CreateAsync(UserId, WordsRequest(2, 3));

        outcome.Status.ShouldBe(NoteStatus.Ok);
        await _repository.Received(1).InsertAsync(UserId, new NoteAnchor(NoteKinds.Words, 45, 8, Rom828, Rom828, 2, 3, "wissen aber,"), "x", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(3, 5)] // position 5 is not in the verse
    [InlineData(5, 6)]
    public async Task CreateAsync_ShouldRefuse_WhenWordPositionsAreOutsideTheElb1871Verse(int from, int to)
    {
        GivenVerseWords();

        var outcome = await Service().CreateAsync(UserId, WordsRequest(from, to));

        outcome.Status.ShouldBe(NoteStatus.Invalid);
        await _repository.DidNotReceiveWithAnyArgs().InsertAsync(default, default!, default!, default);
    }

    [Fact]
    public async Task CreateAsync_ShouldRefuse_WhenTheQuoteWouldBeLongerThanTheLimit()
    {
        _repository.GetElbWordsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(Enumerable.Range(1, 3).Select(i => Word(i, new string('a', 200))).ToList());

        var outcome = await Service().CreateAsync(UserId, WordsRequest(1, 3));

        outcome.Status.ShouldBe(NoteStatus.Invalid);
    }

    [Fact]
    public async Task CreateAsync_ShouldCheckBothVerses_WhenVersesNote()
    {
        _repository.VerseExistsAsync(Rom828, Arg.Any<CancellationToken>()).Returns(true);
        _repository.VerseExistsAsync(45008040, Arg.Any<CancellationToken>()).Returns(false);

        var outcome = await Service().CreateAsync(UserId, new CreateNoteRequest
        {
            Anchor = new NoteAnchorDto { Kind = NoteKinds.Verses, BookId = 45, Chapter = 8, StartHebRefId = Rom828, EndHebRefId = 45008040 },
            BodyMarkdown = "x",
        });

        outcome.Status.ShouldBe(NoteStatus.Invalid);
    }

    [Fact]
    public async Task CreateAsync_ShouldStoreTheRange_WhenBothVersesExist()
    {
        var id = Guid.NewGuid();
        _repository.VerseExistsAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(true);
        _repository.InsertAsync(UserId, Arg.Any<NoteAnchor>(), "x", Arg.Any<CancellationToken>()).Returns(id);
        _repository.GetAsync(id, UserId, Arg.Any<CancellationToken>()).Returns(Row(id));

        var outcome = await Service().CreateAsync(UserId, new CreateNoteRequest
        {
            Anchor = new NoteAnchorDto { Kind = NoteKinds.Verses, BookId = 45, Chapter = 8, StartHebRefId = Rom828, EndHebRefId = 45008030 },
            BodyMarkdown = "x",
        });

        outcome.Status.ShouldBe(NoteStatus.Ok);
        await _repository.Received(1).InsertAsync(UserId, new NoteAnchor(NoteKinds.Verses, 45, 8, Rom828, 45008030, null, null, null), "x", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_ShouldRefreshTheQuote_WhenReanchoringToOtherWords()
    {
        var id = Guid.NewGuid();
        GivenVerseWords();
        _repository.UpdateAsync(id, UserId, Arg.Any<NoteAnchor>(), "neu", Arg.Any<CancellationToken>()).Returns(true);
        _repository.GetAsync(id, UserId, Arg.Any<CancellationToken>()).Returns(Row(id));

        var request = WordsRequest(3, 4);
        var outcome = await Service().UpdateAsync(UserId, new UpdateNoteRequest { Id = id, BodyMarkdown = "neu", Anchor = request.Anchor });

        outcome.Status.ShouldBe(NoteStatus.Ok);
        await _repository.Received(1).UpdateAsync(id, UserId, new NoteAnchor(NoteKinds.Words, 45, 8, Rom828, Rom828, 3, 4, "aber, dass"), "neu", Arg.Any<CancellationToken>());
    }

    private static NoteRecord WordsRow(string quote, int from = 2, int to = 3) => new()
    {
        Id = Guid.NewGuid(), UserId = UserId, Kind = NoteKinds.Words, BibleBookId = 45, Chapter = 8, StartHebRefId = Rom828, EndHebRefId = Rom828,
        StartPosition = from, EndPosition = to, QuotedText = quote, BodyMarkdown = "x",
    };

    [Fact]
    public async Task ListChapterAsync_ShouldReportOk_WhenTheQuoteStillMatchesIgnoringPunctuationAndWhitespace()
    {
        GivenVerseWords();
        _repository.ListChapterAsync(45, 8, UserId, true, Arg.Any<CancellationToken>()).Returns([WordsRow("wissen   aber")]);

        var note = (await Service().ListChapterAsync(UserId, 45, 8, true)).Notes.Single();

        note.AnchorState.ShouldBe(NoteAnchorStates.Ok);
        note.MappedWords.ShouldBeNull();
    }

    [Theory]
    [InlineData("wissen nun,")]  // a word was replaced
    [InlineData("wissen")]       // a word was removed
    public async Task ListChapterAsync_ShouldReportDrifted_WhenTheWordsChanged(string quote)
    {
        GivenVerseWords();
        _repository.ListChapterAsync(45, 8, UserId, true, Arg.Any<CancellationToken>()).Returns([WordsRow(quote)]);

        (await Service().ListChapterAsync(UserId, 45, 8, true)).Notes.Single().AnchorState.ShouldBe(NoteAnchorStates.Drifted);
    }

    [Fact]
    public async Task ListChapterAsync_ShouldReportDrifted_WhenThePositionsAreGone()
    {
        GivenVerseWords();
        _repository.ListChapterAsync(45, 8, UserId, true, Arg.Any<CancellationToken>()).Returns([WordsRow("dass Gott", 4, 5)]);

        (await Service().ListChapterAsync(UserId, 45, 8, true)).Notes.Single().AnchorState.ShouldBe(NoteAnchorStates.Drifted);
    }

    private static NoteVariantWordRow Variant(int word, int position, int? basePosition) =>
        new() { HebRefId = Rom828, RevisionVerseId = 77, RevisionWordId = word, PositionInVerse = position, BasePositionInVerse = basePosition };

    [Fact]
    public async Task ListChapterAsync_ShouldMapThroughTheVariant_WhenALevelHasAVariantOfTheVerse()
    {
        GivenVerseWords();
        _repository.ListChapterAsync(45, 8, UserId, true, Arg.Any<CancellationToken>()).Returns([WordsRow("wissen aber,")]);
        _repository.GetVariantWordsAsync(Arg.Any<IReadOnlyCollection<int>>(), 150, Arg.Any<CancellationToken>())
            .Returns([Variant(900, 1, 1), Variant(901, 2, 2), Variant(902, 3, null), Variant(903, 4, 3), Variant(904, 5, 4)]);

        var note = (await Service().ListChapterAsync(UserId, 45, 8, true, 150)).Notes.Single();

        note.AnchorState.ShouldBe(NoteAnchorStates.Ok);
        note.RevisionVerseId.ShouldBe(77);
        note.MappedWords.ShouldNotBeNull().Select(w => (w.RevisionWordId, w.PositionInVerse, w.BasePositionInVerse)).ShouldBe([(901, 2, 2), (903, 4, 3)]);
    }

    [Fact]
    public async Task ListChapterAsync_ShouldReportNoBaseWords_WhenNoVariantWordMapsIntoTheRange()
    {
        GivenVerseWords();
        _repository.ListChapterAsync(45, 8, UserId, true, Arg.Any<CancellationToken>()).Returns([WordsRow("wissen aber,")]);
        _repository.GetVariantWordsAsync(Arg.Any<IReadOnlyCollection<int>>(), 150, Arg.Any<CancellationToken>())
            .Returns([Variant(900, 1, 1), Variant(901, 2, null), Variant(902, 3, 4)]);

        var note = (await Service().ListChapterAsync(UserId, 45, 8, true, 150)).Notes.Single();

        note.AnchorState.ShouldBe(NoteAnchorStates.NoBaseWords);
        note.MappedWords.ShouldNotBeNull().ShouldBeEmpty();
    }

    [Fact]
    public async Task ListChapterAsync_ShouldNotAskForVariants_WhenLevelIsZero()
    {
        GivenVerseWords();
        _repository.ListChapterAsync(45, 8, UserId, true, Arg.Any<CancellationToken>()).Returns([WordsRow("wissen aber,")]);

        await Service().ListChapterAsync(UserId, 45, 8, true, 0);

        await _repository.DidNotReceiveWithAnyArgs().GetVariantWordsAsync(default!, default, default);
    }

    [Theory]
    [InlineData("  Gott,  lieben! ", "Gott lieben")]
    [InlineData("»Denen«, die", "Denen die")]
    [InlineData("", "")]
    public void Normalize_ShouldDropPunctuationAndCollapseWhitespace(string text, string expected) =>
        NoteQuote.Normalize(text).ShouldBe(expected);

    [Fact]
    public void Matches_ShouldBeCaseSensitiveAndTreatAnotherWordAsDifferent()
    {
        NoteQuote.Matches("Gott lieben", "Gott, lieben").ShouldBeTrue();
        NoteQuote.Matches("Gott lieben", "Gott liebten").ShouldBeFalse();
        NoteQuote.Matches("Gott", "gott").ShouldBeFalse();
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
