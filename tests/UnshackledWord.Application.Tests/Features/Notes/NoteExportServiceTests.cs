using System.Text;
using System.Text.Json;
using UnshackledWord.Application.Features.Notes;

namespace UnshackledWord.Application.Tests.Features.Notes;

public class NoteExportServiceTests
{
    private readonly INoteRepository _repository = Substitute.For<INoteRepository>();

    [Theory]
    [InlineData(NoteKinds.Chapter, 45, 8, null, null, "Röm 8")]
    [InlineData(NoteKinds.Verse, 45, 8, 45008028, 45008028, "Röm 8,28")]
    [InlineData(NoteKinds.Words, 45, 8, 45008028, 45008028, "Röm 8,28")]
    [InlineData(NoteKinds.Verses, 45, 8, 45008028, 45008030, "Röm 8,28–30")]
    public void StartRef_ShouldWriteGermanCitation(string kind, int book, int chapter, int? start, int? end, string expected) =>
        NoteExportService.StartRef(kind, book, chapter, start, end).ShouldBe(expected);

    [Fact]
    public void FileName_ShouldUseTheExportTimestamp() =>
        NoteExportService.FileName(new DateTime(2026, 11, 22, 18, 5, 9, DateTimeKind.Utc)).ShouldBe("notes-20261122-180509.json");

    [Fact]
    public async Task BuildAsync_ShouldWriteTheDocumentedFormat_WhenTheUserHasAWordsNote()
    {
        var id = Guid.NewGuid();
        _repository.ListForExportAsync(7, Arg.Any<CancellationToken>()).Returns([new NoteExportRow
        {
            Id = id,
            AuthorEmail = "anna@example.org",
            AuthorDisplayName = "Anna",
            Kind = NoteKinds.Words,
            BibleBookId = 45,
            Chapter = 8,
            StartHebRefId = 45008028,
            EndHebRefId = 45008028,
            StartPosition = 4,
            EndPosition = 8,
            QuotedText = "denen, die Gott lieben",
            BodyMarkdown = "**gut**",
            Visibility = NoteVisibilities.Private,
            PublicState = NotePublicStates.None,
            CreatedAt = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc),
        }]);
        var service = new NoteExportService(_repository);

        var file = await service.BuildAsync(7, "0.9.6", new DateTime(2026, 11, 22, 18, 0, 0, DateTimeKind.Utc));
        var bytes = NoteExportService.Serialize(file);
        var text = Encoding.UTF8.GetString(bytes);
        using var doc = JsonDocument.Parse(bytes);

        bytes.Take(3).ShouldNotBe([0xEF, 0xBB, 0xBF]);
        text.ShouldNotContain("\r");
        text.ShouldContain("Röm 8,28");
        text.ShouldContain("denen, die Gott lieben");
        var root = doc.RootElement;
        root.GetProperty("format").GetString().ShouldBe("unshackled-word.notes");
        root.GetProperty("formatVersion").GetInt32().ShouldBe(1);
        root.GetProperty("appVersion").GetString().ShouldBe("0.9.6");
        root.GetProperty("exportedAt").GetDateTime().ShouldBe(new DateTime(2026, 11, 22, 18, 0, 0, DateTimeKind.Utc));
        var note = root.GetProperty("notes").EnumerateArray().Single();
        note.GetProperty("id").GetGuid().ShouldBe(id);
        note.GetProperty("authorEmail").GetString().ShouldBe("anna@example.org");
        note.GetProperty("startRef").GetString().ShouldBe("Röm 8,28");
        note.GetProperty("startPosition").GetInt32().ShouldBe(4);
        note.GetProperty("endPosition").GetInt32().ShouldBe(8);
        note.GetProperty("visibility").GetString().ShouldBe("private");
        note.GetProperty("reviewNote").ValueKind.ShouldBe(JsonValueKind.Null);
        note.GetProperty("hiddenAt").ValueKind.ShouldBe(JsonValueKind.Null);

        NoteExportService.Deserialize(bytes)!.Notes.Single().Id.ShouldBe(id);
    }
}
