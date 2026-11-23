namespace UnshackledWord.Application.Features.Notes;

/// <summary>Notes table (roadmap 0023). Every read goes through the visibility predicate; <c>viewerId</c> null = anonymous.</summary>
public interface INoteRepository
{
    /// <summary>Whether the Elb1871 text has this verse.</summary>
    Task<bool> VerseExistsAsync(int hebRefId, CancellationToken token = default);

    /// <summary>Whether the Elb1871 text has at least one verse in this chapter.</summary>
    Task<bool> ChapterExistsAsync(int bookId, int chapter, CancellationToken token = default);

    /// <summary>The Elb1871 words of these verses, ordered by verse and position.</summary>
    Task<IReadOnlyList<NoteWordRow>> GetElbWordsAsync(IReadOnlyCollection<int> hebRefIds, CancellationToken token = default);

    /// <summary>
    /// The words of the variant that is active at <paramref name="level"/> (same selection as the reader) for these verses of one chapter;
    /// verses without a variant at that level return nothing.
    /// </summary>
    Task<IReadOnlyList<NoteVariantWordRow>> GetVariantWordsAsync(IReadOnlyCollection<int> hebRefIds, int level, CancellationToken token = default);

    /// <summary>Inserts a private note and returns its id.</summary>
    Task<Guid> InsertAsync(int userId, NoteAnchor anchor, string bodyMarkdown, CancellationToken token = default);

    /// <summary>Updates body and anchor of the user's own note; false when there is no such note of this user. An approved public note goes back to pending.</summary>
    Task<bool> UpdateAsync(Guid id, int userId, NoteAnchor? anchor, string bodyMarkdown, CancellationToken token = default);

    /// <summary>Hard-deletes the user's own note; false when there is no such note of this user.</summary>
    Task<bool> DeleteAsync(Guid id, int userId, CancellationToken token = default);

    /// <summary>One note if the viewer may see it.</summary>
    Task<NoteRecord?> GetAsync(Guid id, int? viewerId, CancellationToken token = default);

    /// <summary>The chapter's notes the viewer may see, in reading order; <paramref name="others"/> false = only the viewer's own.</summary>
    Task<IReadOnlyList<NoteRecord>> ListChapterAsync(int bookId, int chapter, int? viewerId, bool others, CancellationToken token = default);

    /// <summary>One page of the user's own notes (all visibilities, hidden ones included), newest change first, plus the total matching the filter.</summary>
    Task<NotePage> ListMineAsync(int userId, NoteListFilter filter, CancellationToken token = default);

    /// <summary>Every note of the user (<paramref name="userId"/>) or of everybody (null) with author and approver emails, ordered by id; for the export file.</summary>
    Task<IReadOnlyList<NoteExportRow>> ListForExportAsync(int? userId, CancellationToken token = default);

    /// <summary>
    /// Sets the visibility of the user's own note: <c>private</c>/<c>internal</c> clear state and approval snapshot, <c>public</c> requests review
    /// (<c>pending</c>; an already pending or approved note keeps its state). False when there is no such note of this user.
    /// </summary>
    Task<bool> SetVisibilityAsync(Guid id, int userId, string visibility, CancellationToken token = default);

    /// <summary>Whether a note with this id exists, whoever owns it and whatever its visibility (maintainer actions).</summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken token = default);

    /// <summary>
    /// Applies a maintainer action if the note is in the right state (approve/reject: public and pending; hide: not private and not hidden; unhide: hidden)
    /// and returns the author's user id; null when nothing changed. Approve with <paramref name="expectedVersion"/> applies only while
    /// <c>COALESCE(UpdatedAt, CreatedAt)</c> still equals it.
    /// </summary>
    Task<int?> ReviewAsync(Guid id, NoteReviewAction action, int actorUserId, string? reason, DateTime? expectedVersion = null, CancellationToken token = default);

    /// <summary>The review queue; notes carry the current and the approved text. Oldest change first.</summary>
    Task<NoteReviewQueue> ListReviewAsync(int viewerId, CancellationToken token = default);
}
