namespace UnshackledWord.Domain.WebApi.Notes;

/// <summary>
/// Where a note sits (roadmap 0023). <c>HebRefId</c> = book * 1,000,000 + chapter * 1,000 + verse.
/// <list type="bullet">
/// <item><c>chapter</c>: only <see cref="BookId"/> and <see cref="Chapter"/>.</item>
/// <item><c>verse</c>: <see cref="StartHebRefId"/> (and <see cref="EndHebRefId"/> = start or omitted).</item>
/// <item><c>verses</c>: <see cref="StartHebRefId"/> &lt; <see cref="EndHebRefId"/>, same chapter, both verses exist in Elb1871.</item>
/// <item><c>words</c>: one verse (<see cref="StartHebRefId"/>, end omitted or equal) and <see cref="StartPosition"/>..<see cref="EndPosition"/>,
/// Elb1871 <c>PositionInVerse</c> values that exist; the server fills <see cref="QuotedText"/> (max 500 characters), a client quote is ignored.</item>
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

/// <summary><c>GET api/notes/{id}</c>. Public; 404 when the caller may not see the note.</summary>
public sealed class GetNoteRequest
{
    public Guid Id { get; set; }
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

    /// <summary>
    /// Query parameter, the reader's literalness level (0..1000, same meaning as on the verse endpoints). Omitted or 0: Elb1871 only.
    /// With a level, words notes on verses that have an active variant at that level carry <see cref="NoteResponse.MappedWords"/>.
    /// </summary>
    public int? Level { get; set; }
}

/// <summary>One word of a revision variant that stands for an anchored Elb1871 word (mapped through <c>BasePositionInVerse</c>).</summary>
public sealed class NoteMappedWordDto
{
    /// <summary><c>RevisionWords.Id</c>.</summary>
    public int RevisionWordId { get; set; }

    /// <summary><c>RevisionWords.PositionInVerse</c>: where to put the marker in the variant verse.</summary>
    public int PositionInVerse { get; set; }

    /// <summary>The Elb1871 position this variant word is based on (inside the note's start..end positions).</summary>
    public int BasePositionInVerse { get; set; }
}

/// <summary>One note as the caller may see it.</summary>
public sealed class NoteResponse
{
    public Guid Id { get; set; }
    public NoteAnchorDto Anchor { get; set; } = new();

    /// <summary>
    /// <c>ok</c> | <c>drifted</c> (words notes: the Elb1871 words at the positions no longer equal the quote, or the positions are gone; show the note
    /// at verse level with the quote as hint) | <c>noBaseWords</c> (words notes at a level whose variant has no word mapped into the range; verse level, no hint).
    /// </summary>
    public string AnchorState { get; set; } = NoteAnchorStates.Ok;

    /// <summary>
    /// Words notes with <c>anchorState = ok</c> on a verse whose active variant at the requested level differs from Elb1871: the variant words that
    /// carry the marker (none maps = <c>noBaseWords</c>). Null: use <c>anchor.startPosition..endPosition</c> (Elb1871 words).
    /// </summary>
    public List<NoteMappedWordDto>? MappedWords { get; set; }

    /// <summary><c>RevisionVerses.Id</c> of the variant that <see cref="MappedWords"/> belongs to; null when Elb1871 positions apply.</summary>
    public int? RevisionVerseId { get; set; }

    /// <summary>Raw Markdown (anonymous readers get the approved snapshot).</summary>
    public string BodyMarkdown { get; set; } = string.Empty;

    /// <summary>Markdown with scripture references turned into links (roadmap 0023.3; equal to the raw body until then).</summary>
    public string BodyLinked { get; set; } = string.Empty;

    /// <summary><c>private</c> | <c>internal</c> | <c>public</c>.</summary>
    public string Visibility { get; set; } = string.Empty;

    /// <summary><c>none</c> | <c>pending</c> | <c>approved</c> | <c>rejected</c>.</summary>
    public string PublicState { get; set; } = string.Empty;

    public bool IsOwn { get; set; }

    /// <summary>A maintainer hid the note: only the author (and the review page) still sees it.</summary>
    public bool IsHidden { get; set; }

    /// <summary>Maintainer's rejection reason (<c>publicState = rejected</c>) or hide reason; only on the caller's own notes.</summary>
    public string? ReviewNote { get; set; }

    /// <summary>Always set, also on others' notes (display name of the author).</summary>
    public string AuthorDisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary><c>PUT api/notes/{id}/visibility</c>. Owner only; the answer is the updated <see cref="NoteResponse"/>.</summary>
public sealed class SetNoteVisibilityRequest
{
    /// <summary>Route parameter.</summary>
    public Guid Id { get; set; }

    /// <summary>
    /// <c>private</c> | <c>internal</c> (every signed-in user) | <c>public</c> (a request: state <c>pending</c> until a maintainer approves; until then the
    /// note is internal, anonymous readers see only an earlier approved text).
    /// </summary>
    public string Visibility { get; set; } = string.Empty;
}

/// <summary>Body/route of the maintainer actions <c>POST api/maintainer/notes/{id}/approve|reject|hide|unhide</c>.</summary>
public sealed class NoteReviewActionRequest
{
    /// <summary>Route parameter.</summary>
    public Guid Id { get; set; }

    /// <summary>Optional reason (max 500 characters) for reject and hide; ignored by approve and unhide.</summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Approve only: the <c>updatedAt ?? createdAt</c> of the text the maintainer reviewed. When set and the author changed the note since,
    /// approve answers 409 and nothing is approved, so an unseen edit never becomes the public snapshot.
    /// </summary>
    public DateTime? ReviewedVersion { get; set; }
}

/// <summary>One note in the review queue: the current text, the approved text for a diff and the review facts.</summary>
public sealed class ReviewNoteDto
{
    /// <summary>Current state as the maintainer sees it (<c>bodyMarkdown</c> is the author's latest text).</summary>
    public NoteResponse Note { get; set; } = new();

    /// <summary>The text the maintainer approved earlier, i.e. what anonymous readers see; null if never approved (or withdrawn).</summary>
    public string? ApprovedBodyMarkdown { get; set; }

    public DateTime? ApprovedAt { get; set; }
    public DateTime? HiddenAt { get; set; }

    /// <summary>Rejection or hide reason.</summary>
    public string? ReviewNote { get; set; }
}

/// <summary><c>GET api/maintainer/notes/review</c>.</summary>
public sealed class ReviewNotesResponse
{
    /// <summary>Public notes waiting for approval (not hidden), oldest change first.</summary>
    public List<ReviewNoteDto> Pending { get; set; } = [];

    /// <summary>Hidden notes, oldest hide first.</summary>
    public List<ReviewNoteDto> Hidden { get; set; } = [];
}

/// <summary><c>GET api/notes/mine?book=&amp;visibility=&amp;q=&amp;skip=&amp;take=</c>: the caller's own notes, newest change first.</summary>
public sealed class ListMyNotesRequest
{
    /// <summary>Query parameter: only notes of this book (1..66).</summary>
    public int? Book { get; set; }

    /// <summary>Query parameter: <c>private</c> | <c>internal</c> | <c>public</c>.</summary>
    public string? Visibility { get; set; }

    /// <summary>Query parameter: case-insensitive text search in the body (wildcards are literal), max 200 characters.</summary>
    public string? Q { get; set; }

    public int Skip { get; set; }

    /// <summary>Page size, 1..50 (default 50).</summary>
    public int Take { get; set; } = 50;
}

/// <summary>One page of the caller's notes; <see cref="Total"/> counts all notes matching the filters.</summary>
public sealed class MyNotesResponse
{
    public int Total { get; set; }

    /// <summary>Ordered by last change (<c>UpdatedAt</c>, else <c>CreatedAt</c>), newest first. Anchor state is the Elb1871 one.</summary>
    public List<NoteResponse> Notes { get; set; } = [];
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
