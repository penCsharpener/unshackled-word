namespace UnshackledWord.Domain.WebApi.Notes;

/// <summary>
/// Where a note sits (roadmap 0023). <c>HebRefId</c> = book * 1,000,000 + chapter * 1,000 + verse.
/// <list type="bullet">
/// <item><c>chapter</c>: only <see cref="BookId"/> and <see cref="Chapter"/>.</item>
/// <item><c>verse</c>: <see cref="StartHebRefId"/> (and <see cref="EndHebRefId"/> = start or omitted).</item>
/// <item><c>verses</c> (range within one chapter) and <c>words</c> (positions within one verse) follow in roadmap 0023.2.</item>
/// </list>
/// </summary>
public sealed class NoteAnchorDto
{
    /// <summary><c>chapter</c> | <c>verses</c> | <c>verse</c> | <c>words</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    public int BookId { get; set; }
    public int Chapter { get; set; }
    public int? StartHebRefId { get; set; }
    public int? EndHebRefId { get; set; }

    /// <summary>Elb1871 <c>PositionInVerse</c> of the first anchored word; words notes only.</summary>
    public int? StartPosition { get; set; }

    public int? EndPosition { get; set; }

    /// <summary>Response only: the Elb1871 words at save time (words notes). Ignored in requests, the server fills it.</summary>
    public string? QuotedText { get; set; }

    /// <summary>Response only: verse number of <see cref="StartHebRefId"/>.</summary>
    public int? StartVerse { get; set; }

    /// <summary>Response only: verse number of <see cref="EndHebRefId"/>.</summary>
    public int? EndVerse { get; set; }
}

/// <summary>Body of <c>POST api/notes</c>. The author is the signed-in user, never a field of the body.</summary>
public sealed class CreateNoteRequest
{
    public NoteAnchorDto Anchor { get; set; } = new();

    /// <summary>Raw Markdown, 1..20000 characters.</summary>
    public string BodyMarkdown { get; set; } = string.Empty;
}

/// <summary><c>PUT api/notes/{id}</c>: new body and, optionally, a new anchor (null keeps the anchor). Owner only.</summary>
public sealed class UpdateNoteRequest
{
    /// <summary>Route parameter.</summary>
    public Guid Id { get; set; }

    public NoteAnchorDto? Anchor { get; set; }

    public string BodyMarkdown { get; set; } = string.Empty;
}

/// <summary><c>DELETE api/notes/{id}</c>. Owner only.</summary>
public sealed class DeleteNoteRequest
{
    public Guid Id { get; set; }
}

/// <summary><c>GET api/notes/chapter/{bookId}/{chapter}?others=true</c>.</summary>
public sealed class GetChapterNotesRequest
{
    public int BookId { get; set; }
    public int Chapter { get; set; }

    /// <summary>Query parameter; false: only the caller's own notes. Default true.</summary>
    public bool Others { get; set; } = true;
}

/// <summary>One note as the caller may see it.</summary>
public sealed class NoteResponse
{
    public Guid Id { get; set; }
    public NoteAnchorDto Anchor { get; set; } = new();

    /// <summary><c>ok</c> | <c>drifted</c> | <c>noBaseWords</c> (the last two from roadmap 0023.2 on).</summary>
    public string AnchorState { get; set; } = NoteAnchorStates.Ok;

    /// <summary>Raw Markdown (anonymous readers get the approved snapshot).</summary>
    public string BodyMarkdown { get; set; } = string.Empty;

    /// <summary>Markdown with scripture references turned into links (roadmap 0023.3; equal to the raw body until then).</summary>
    public string BodyLinked { get; set; } = string.Empty;

    /// <summary><c>private</c> | <c>internal</c> | <c>public</c>.</summary>
    public string Visibility { get; set; } = string.Empty;

    /// <summary><c>none</c> | <c>pending</c> | <c>approved</c> | <c>rejected</c>.</summary>
    public string PublicState { get; set; } = string.Empty;

    public bool IsOwn { get; set; }
    public bool IsHidden { get; set; }
    public string AuthorDisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class ChapterNotesResponse
{
    /// <summary>In reading order: chapter notes first, then by verse and word position, then oldest first.</summary>
    public List<NoteResponse> Notes { get; set; } = [];
}

public static class NoteAnchorStates
{
    public const string Ok = "ok";
    public const string Drifted = "drifted";
    public const string NoBaseWords = "noBaseWords";
}
