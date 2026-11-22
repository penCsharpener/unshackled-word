namespace UnshackledWord.Application.Features.Notes;

/// <summary>A note with the e-mails that identify its author and approver, as the repository reads it for the export (roadmap 0023.4, reused by 0023.6).</summary>
public sealed class NoteExportRow
{
    public Guid Id { get; init; }
    public string AuthorEmail { get; init; } = string.Empty;
    public string AuthorDisplayName { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public int BibleBookId { get; init; }
    public int Chapter { get; init; }
    public int? StartHebRefId { get; init; }
    public int? EndHebRefId { get; init; }
    public int? StartPosition { get; init; }
    public int? EndPosition { get; init; }
    public string? QuotedText { get; init; }
    public string BodyMarkdown { get; init; } = string.Empty;
    public string Visibility { get; init; } = string.Empty;
    public string PublicState { get; init; } = string.Empty;
    public string? ApprovedBodyMarkdown { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public string? ApprovedByEmail { get; init; }
    public string? ReviewNote { get; init; }
    public DateTime? HiddenAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>The file of <c>06-export-import.md</c>: <c>notes-YYYYMMDD-HHmmss.json</c>.</summary>
public sealed class NoteExportFile
{
    public const string FormatName = "unshackled-word.notes";
    public const int CurrentFormatVersion = 1;

    public string Format { get; set; } = FormatName;
    public int FormatVersion { get; set; } = CurrentFormatVersion;
    public DateTime ExportedAt { get; set; }
    public string AppVersion { get; set; } = string.Empty;
    public List<NoteExportItem> Notes { get; set; } = [];
}

/// <summary>One note in the file. <see cref="StartRef"/> is for humans; an import reads the ids.</summary>
public sealed class NoteExportItem
{
    public Guid Id { get; set; }
    public string AuthorEmail { get; set; } = string.Empty;
    public string AuthorDisplayName { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string StartRef { get; set; } = string.Empty;
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int? StartHebRefId { get; set; }
    public int? EndHebRefId { get; set; }
    public int? StartPosition { get; set; }
    public int? EndPosition { get; set; }
    public string? QuotedText { get; set; }
    public string BodyMarkdown { get; set; } = string.Empty;
    public string Visibility { get; set; } = string.Empty;
    public string PublicState { get; set; } = string.Empty;
    public string? ApprovedBodyMarkdown { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovedByEmail { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime? HiddenAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
