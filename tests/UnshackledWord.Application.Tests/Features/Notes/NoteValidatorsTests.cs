using UnshackledWord.Application.Features.Notes;
using UnshackledWord.Domain.WebApi.Notes;

namespace UnshackledWord.Application.Tests.Features.Notes;

public class NoteValidatorsTests
{
    private const int Rom828 = 45008028;

    private static CreateNoteRequest Request(NoteAnchorDto anchor, string body = "Gott wirkt alles zum Guten.") => new() { Anchor = anchor, BodyMarkdown = body };

    private static NoteAnchorDto Verse(int? start = Rom828, int? end = null) => new() { Kind = NoteKinds.Verse, BookId = 45, Chapter = 8, StartHebRefId = start, EndHebRefId = end };

    private static NoteAnchorDto Chapter() => new() { Kind = NoteKinds.Chapter, BookId = 45, Chapter = 8 };

    [Fact]
    public void Validate_ShouldPass_WhenChapterNote() =>
        new CreateNoteRequestValidator().Validate(Request(Chapter())).IsValid.ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData(Rom828)]
    public void Validate_ShouldPass_WhenVerseNoteEndsWhereItStarts(int? end) =>
        new CreateNoteRequestValidator().Validate(Request(Verse(end: end))).IsValid.ShouldBeTrue();

    [Theory]
    [InlineData(NoteKinds.Verses)]
    [InlineData(NoteKinds.Words)]
    [InlineData("paragraph")]
    [InlineData("")]
    public void Validate_ShouldFail_WhenKindIsNotSupportedYet(string kind)
    {
        var anchor = Verse();
        anchor.Kind = kind;

        var result = new CreateNoteRequestValidator().Validate(Request(anchor));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "Anchor.Kind");
    }

    [Theory]
    [InlineData(45008039, 45009001)] // Röm 8,39–9,1: across chapters
    [InlineData(Rom828, 45008030)]   // a range is a verses note, not a verse note
    public void Validate_ShouldFail_WhenVerseNoteHasAnotherEnd(int start, int end) =>
        new CreateNoteRequestValidator().Validate(Request(Verse(start, end))).IsValid.ShouldBeFalse();

    [Theory]
    [InlineData(null)]
    [InlineData(45009001)] // other chapter
    [InlineData(46008028)] // other book
    [InlineData(45008000)] // verse 0
    public void Validate_ShouldFail_WhenVerseRefIsMissingOrOutsideTheChapter(int? start) =>
        new CreateNoteRequestValidator().Validate(Request(Verse(start, start))).IsValid.ShouldBeFalse();

    [Fact]
    public void Validate_ShouldFail_WhenChapterNoteHasAVerse()
    {
        var anchor = Chapter();
        anchor.StartHebRefId = Rom828;

        new CreateNoteRequestValidator().Validate(Request(anchor)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validate_ShouldFail_WhenVerseNoteHasWordPositions()
    {
        var anchor = Verse();
        anchor.StartPosition = 1;
        anchor.EndPosition = 2;

        new CreateNoteRequestValidator().Validate(Request(anchor)).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0, 8)]
    [InlineData(67, 8)]
    [InlineData(45, 0)]
    [InlineData(45, 151)]
    public void Validate_ShouldFail_WhenBookOrChapterIsOutOfRange(int book, int chapter) =>
        new CreateNoteRequestValidator().Validate(Request(new NoteAnchorDto { Kind = NoteKinds.Chapter, BookId = book, Chapter = chapter })).IsValid.ShouldBeFalse();

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(NoteBody.MaxLength, true)]
    [InlineData(NoteBody.MaxLength + 1, false)]
    public void Validate_ShouldCheckBodyLength_WhenCreating(int length, bool valid) =>
        new CreateNoteRequestValidator().Validate(Request(Chapter(), new string('x', length))).IsValid.ShouldBe(valid);

    [Fact]
    public void Validate_ShouldFail_WhenBodyIsOnlyWhitespace() =>
        new CreateNoteRequestValidator().Validate(Request(Chapter(), "   \n ")).IsValid.ShouldBeFalse();

    [Fact]
    public void Validate_ShouldFail_WhenAnchorIsMissing() =>
        new CreateNoteRequestValidator().Validate(new CreateNoteRequest { Anchor = null!, BodyMarkdown = "x" }).IsValid.ShouldBeFalse();

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Validate_ShouldValidateTheAnchorOnlyWhenGiven_WhenUpdating(bool withBadAnchor, bool valid)
    {
        var request = new UpdateNoteRequest { Id = Guid.NewGuid(), BodyMarkdown = "neu", Anchor = withBadAnchor ? Verse(45008039, 45009001) : null };

        new UpdateNoteRequestValidator().Validate(request).IsValid.ShouldBe(valid);
    }
}
