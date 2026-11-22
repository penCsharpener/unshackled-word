namespace UnshackledWord.Application.Features.Notes;

public static class NoteKinds
{
    public const string Chapter = "chapter";
    public const string Verses = "verses";
    public const string Verse = "verse";
    public const string Words = "words";

    public static readonly IReadOnlyList<string> All = [Chapter, Verses, Verse, Words];

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

/// <summary>One Elb1871 word of a verse.</summary>
public sealed class NoteWordRow
{
    public int HebRefId { get; init; }
    public int PositionInVerse { get; init; }
    public string WordInContext { get; init; } = string.Empty;
}

/// <summary>One word of the active revision variant of a verse (at the reader's level).</summary>
public sealed class NoteVariantWordRow
{
    public int HebRefId { get; init; }
    public int RevisionVerseId { get; init; }
    public int RevisionWordId { get; init; }
    public int PositionInVerse { get; init; }
    public int? BasePositionInVerse { get; init; }
}

/// <summary>Quote handling of words notes.</summary>
public static class NoteQuote
{
    public const int MaxLength = 500;

    /// <summary>The Elb1871 words joined by single spaces.</summary>
    public static string Join(IEnumerable<string> words) => string.Join(' ', words);

    /// <summary>Whitespace and punctuation do not count: letters and digits in word groups separated by single spaces (NFC).</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var c in text.Normalize(System.Text.NormalizationForm.FormC))
        {
            if (char.IsLetterOrDigit(c) || char.GetUnicodeCategory(c) is System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                if (pendingSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                pendingSpace = false;
                builder.Append(c);
            }
            else if (char.IsWhiteSpace(c))
            {
                pendingSpace = true;
            }
        }

        return builder.ToString();
    }

    public static bool Matches(string? stored, string? current) => Normalize(stored) == Normalize(current);
}
