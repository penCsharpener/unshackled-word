using UnshackledWord.Domain.WebApi.Notes;

namespace UnshackledWord.Application.Features.Notes;

/// <summary>
/// Notes on chapters and verses (roadmap 0023.1). The request validators check the anchor shape; this service checks that the anchored
/// chapter/verse exists in the Elb1871 text, writes only the caller's own notes and maps rows to responses for the viewer.
/// </summary>
public sealed class NoteService
{
    private readonly INoteRepository _repository;

    public NoteService(INoteRepository repository)
    {
        _repository = repository;
    }

    public async Task<NoteOutcome<NoteResponse>> CreateAsync(int userId, CreateNoteRequest request, CancellationToken token = default)
    {
        var anchor = await ResolveAnchorAsync(request.Anchor, token);
        if (anchor is null)
        {
            return NoteOutcome<NoteResponse>.Invalid(MissingTextMessage(request.Anchor));
        }

        var id = await _repository.InsertAsync(userId, anchor, request.BodyMarkdown, token);

        return await ReadBackAsync(id, userId, token);
    }

    public async Task<NoteOutcome<NoteResponse>> UpdateAsync(int userId, UpdateNoteRequest request, CancellationToken token = default)
    {
        NoteAnchor? anchor = null;
        if (request.Anchor is not null)
        {
            anchor = await ResolveAnchorAsync(request.Anchor, token);
            if (anchor is null)
            {
                return NoteOutcome<NoteResponse>.Invalid(MissingTextMessage(request.Anchor));
            }
        }

        if (!await _repository.UpdateAsync(request.Id, userId, anchor, request.BodyMarkdown, token))
        {
            return NoteOutcome<NoteResponse>.NotFound(NotFoundMessage);
        }

        return await ReadBackAsync(request.Id, userId, token);
    }

    /// <summary>True when the caller's own note was deleted.</summary>
    public Task<bool> DeleteAsync(int userId, Guid id, CancellationToken token = default) => _repository.DeleteAsync(id, userId, token);

    public async Task<ChapterNotesResponse> ListChapterAsync(int? viewerId, int bookId, int chapter, bool others, CancellationToken token = default)
    {
        var rows = await _repository.ListChapterAsync(bookId, chapter, viewerId, others, token);

        return new ChapterNotesResponse { Notes = rows.Select(row => ToResponse(row, viewerId)).ToList() };
    }

    /// <summary>The normalised anchor (verse: end = start), or null when the Elb1871 text has no such chapter/verse. The shape is validated before.</summary>
    private async Task<NoteAnchor?> ResolveAnchorAsync(NoteAnchorDto dto, CancellationToken token)
    {
        switch (dto.Kind)
        {
            case NoteKinds.Chapter:
                return await _repository.ChapterExistsAsync(dto.BookId, dto.Chapter, token)
                    ? new NoteAnchor(NoteKinds.Chapter, dto.BookId, dto.Chapter, null, null, null, null, null)
                    : null;
            case NoteKinds.Verse when dto.StartHebRefId is { } verse:
                return await _repository.VerseExistsAsync(verse, token)
                    ? new NoteAnchor(NoteKinds.Verse, dto.BookId, dto.Chapter, verse, verse, null, null, null)
                    : null;
            default:
                return null;
        }
    }

    public static NoteResponse ToResponse(NoteRecord row, int? viewerId) => new()
    {
        Id = row.Id,
        Anchor = new NoteAnchorDto
        {
            Kind = row.Kind,
            BookId = row.BibleBookId,
            Chapter = row.Chapter,
            StartHebRefId = row.StartHebRefId,
            EndHebRefId = row.EndHebRefId,
            StartPosition = row.StartPosition,
            EndPosition = row.EndPosition,
            QuotedText = row.QuotedText,
            StartVerse = row.StartHebRefId is { } start ? HebRef.Verse(start) : null,
            EndVerse = row.EndHebRefId is { } end ? HebRef.Verse(end) : null,
        },
        AnchorState = NoteAnchorStates.Ok,
        BodyMarkdown = row.BodyMarkdown,
        BodyLinked = row.BodyMarkdown,
        Visibility = row.Visibility,
        PublicState = row.PublicState,
        IsOwn = viewerId is not null && row.UserId == viewerId,
        IsHidden = row.IsHidden,
        AuthorDisplayName = row.AuthorDisplayName,
        CreatedAt = row.CreatedAt,
        UpdatedAt = row.UpdatedAt,
    };

    private const string NotFoundMessage = "No such note of yours.";

    private static string MissingTextMessage(NoteAnchorDto anchor) => anchor.Kind == NoteKinds.Chapter
        ? $"The Elb1871 text has no chapter {anchor.BookId}/{anchor.Chapter}."
        : $"The Elb1871 text has no verse {anchor.StartHebRefId}.";

    private async Task<NoteOutcome<NoteResponse>> ReadBackAsync(Guid id, int userId, CancellationToken token)
    {
        var row = await _repository.GetAsync(id, userId, token);

        return row is null ? NoteOutcome<NoteResponse>.NotFound(NotFoundMessage) : NoteOutcome<NoteResponse>.Ok(ToResponse(row, userId));
    }
}
