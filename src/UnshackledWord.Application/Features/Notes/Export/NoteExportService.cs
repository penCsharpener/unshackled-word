using System.Text;
using System.Text.Json;
using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Application.Features.Notes;

/// <summary>Builds and writes the notes export file (<c>06-export-import.md</c>). One caller's notes for the user download, everybody's for the maintainer job (0023.6).</summary>
public sealed class NoteExportService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly INoteRepository _repository;

    public NoteExportService(INoteRepository repository)
    {
        _repository = repository;
    }

    /// <summary>The caller's notes (<paramref name="userId"/>) or all notes (null), ordered by id.</summary>
    public async Task<NoteExportFile> BuildAsync(int? userId, string appVersion, DateTime exportedAtUtc, CancellationToken token = default)
    {
        var rows = await _repository.ListForExportAsync(userId, token);

        return new NoteExportFile
        {
            ExportedAt = exportedAtUtc,
            AppVersion = appVersion,
            Notes = rows.Select(ToItem).ToList(),
        };
    }

    /// <summary>UTF-8 without BOM, LF line endings.</summary>
    public static byte[] Serialize(NoteExportFile file) => new UTF8Encoding(false).GetBytes(JsonSerializer.Serialize(file, Json) + "\n");

    public static NoteExportFile? Deserialize(ReadOnlySpan<byte> utf8) => JsonSerializer.Deserialize<NoteExportFile>(utf8, Json);

    public static string FileName(DateTime exportedAtUtc) => $"notes-{exportedAtUtc:yyyyMMdd-HHmmss}.json";

    /// <summary>Röm 8 (chapter), Röm 8,28, Röm 8,28–30.</summary>
    public static string StartRef(string kind, int bookId, int chapter, int? startHebRefId, int? endHebRefId)
    {
        var book = GermanBookNames.GetAbbreviation(bookId);
        if (startHebRefId is not { } start)
        {
            return $"{book} {chapter}";
        }

        var verse = $"{book} {chapter},{start % 1000}";

        return kind == NoteKinds.Verses && endHebRefId is { } end && end != start ? $"{verse}–{end % 1000}" : verse;
    }

    private static NoteExportItem ToItem(NoteExportRow row) => new()
    {
        Id = row.Id,
        AuthorEmail = row.AuthorEmail,
        AuthorDisplayName = row.AuthorDisplayName,
        Kind = row.Kind,
        StartRef = StartRef(row.Kind, row.BibleBookId, row.Chapter, row.StartHebRefId, row.EndHebRefId),
        BibleBookId = row.BibleBookId,
        Chapter = row.Chapter,
        StartHebRefId = row.StartHebRefId,
        EndHebRefId = row.EndHebRefId,
        StartPosition = row.StartPosition,
        EndPosition = row.EndPosition,
        QuotedText = row.QuotedText,
        BodyMarkdown = row.BodyMarkdown,
        Visibility = row.Visibility,
        PublicState = row.PublicState,
        ApprovedBodyMarkdown = row.ApprovedBodyMarkdown,
        ApprovedAt = row.ApprovedAt,
        ApprovedByEmail = row.ApprovedByEmail,
        ReviewNote = row.ReviewNote,
        HiddenAt = row.HiddenAt,
        CreatedAt = row.CreatedAt,
        UpdatedAt = row.UpdatedAt,
    };
}
