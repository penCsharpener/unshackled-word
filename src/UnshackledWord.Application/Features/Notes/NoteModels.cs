namespace UnshackledWord.Application.Features.Notes;

public static class NoteKinds
{
    public const string Chapter = "chapter";
    public const string Verses = "verses";
    public const string Verse = "verse";
    public const string Words = "words";

    public static readonly IReadOnlyList<string> All = [Chapter, Verses, Verse, Words];

    /// <summary>Kinds the API accepts at the moment; <c>verses</c> and <c>words</c> follow in roadmap 0023.2.</summary>
    public static readonly IReadOnlyList<string> Supported = [Chapter, Verse];
}

public static class NoteVisibilities
{
    public const string Private = "private";
    public const string Internal = "internal";
    public const string Public = "public";
}

public static class NotePublicStates
{
    public const string None = "none";
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
}

/// <summary>HebRefId arithmetic: book * 1,000,000 + chapter * 1,000 + verse.</summary>
public static class HebRef
{
    public static int Book(int hebRefId) => hebRefId / 1_000_000;

    public static int Chapter(int hebRefId) => hebRefId / 1_000 % 1_000;

    public static int Verse(int hebRefId) => hebRefId % 1_000;
}

/// <summary>The anchor columns of a <c>Notes</c> row, already normalised (verse: end = start).</summary>
public sealed record NoteAnchor(
    string Kind,
    int BookId,
    int Chapter,
    int? StartHebRefId,
    int? EndHebRefId,
    int? StartPosition,
    int? EndPosition,
    string? QuotedText);

/// <summary>A note as the viewer may see it; <see cref="BodyMarkdown"/> is already the viewer's text (approved snapshot for anonymous).</summary>
public sealed class NoteRecord
{
    public Guid Id { get; init; }
    public int UserId { get; init; }
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
    public string Visibility { get; init; } = NoteVisibilities.Private;
    public string PublicState { get; init; } = NotePublicStates.None;
    public bool IsHidden { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public enum NoteStatus
{
    Ok,
    NotFound,
    Invalid,
}

public sealed class NoteOutcome<T> where T : class
{
    public NoteStatus Status { get; private init; }
    public T? Response { get; private init; }
    public string? Message { get; private init; }

    public static NoteOutcome<T> Ok(T response) => new() { Status = NoteStatus.Ok, Response = response };

    public static NoteOutcome<T> NotFound(string message) => new() { Status = NoteStatus.NotFound, Message = message };

    public static NoteOutcome<T> Invalid(string message) => new() { Status = NoteStatus.Invalid, Message = message };
}
