namespace UnshackledWord.Application.Features.Notes;

/// <summary>Notes table (roadmap 0023). Every read goes through the visibility predicate; <c>viewerId</c> null = anonymous.</summary>
public interface INoteRepository
{
    /// <summary>Whether the Elb1871 text has this verse.</summary>
    Task<bool> VerseExistsAsync(int hebRefId, CancellationToken token = default);

    /// <summary>Whether the Elb1871 text has at least one verse in this chapter.</summary>
    Task<bool> ChapterExistsAsync(int bookId, int chapter, CancellationToken token = default);

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
}
