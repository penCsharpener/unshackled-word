using UnshackledWord.Domain.WebApi.Notes;

namespace UnshackledWord.Application.Features.Notes;

/// <summary>
/// Notes on chapters, verse ranges, verses and words (roadmap 0023.1/0023.2). The request validators check the anchor shape; this service checks that the anchored
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

    /// <summary>The owner changes the visibility; <c>public</c> only requests review. 404 for notes of others.</summary>
    public async Task<NoteOutcome<NoteResponse>> SetVisibilityAsync(int userId, SetNoteVisibilityRequest request, CancellationToken token = default)
    {
        if (!await _repository.SetVisibilityAsync(request.Id, userId, request.Visibility, token))
        {
            return NoteOutcome<NoteResponse>.NotFound(NotFoundMessage);
        }

        return await ReadBackAsync(request.Id, userId, token);
    }

    /// <summary>The maintainer's queue with current and approved text.</summary>
    public async Task<ReviewNotesResponse> ListReviewAsync(int maintainerId, CancellationToken token = default)
    {
        var queue = await _repository.ListReviewAsync(maintainerId, token);

        return new ReviewNotesResponse
        {
            Pending = await ToReviewDtosAsync(queue.Pending, maintainerId, token),
            Hidden = await ToReviewDtosAsync(queue.Hidden, maintainerId, token),
        };
    }

    private async Task<List<ReviewNoteDto>> ToReviewDtosAsync(IReadOnlyList<NoteRecord> rows, int viewerId, CancellationToken token)
    {
        var responses = await ToResponsesAsync(rows, viewerId, null, token);

        return rows.Select((row, i) => new ReviewNoteDto
        {
            Note = responses[i],
            ApprovedBodyMarkdown = row.ApprovedBodyMarkdown,
            ApprovedAt = row.ApprovedAt,
            HiddenAt = row.HiddenAt,
            ReviewNote = row.ReviewNote,
        }).ToList();
    }

    /// <summary>One note as the viewer may see it (anonymous: the approved snapshot); 404 when the visibility filter hides it.</summary>
    public async Task<NoteOutcome<NoteResponse>> GetAsync(int? viewerId, Guid id, CancellationToken token = default)
    {
        var row = await _repository.GetAsync(id, viewerId, token);

        return row is null
            ? NoteOutcome<NoteResponse>.NotFound("No such note.")
            : NoteOutcome<NoteResponse>.Ok((await ToResponsesAsync([row], viewerId, null, token)).Single());
    }

    /// <summary>True when the caller's own note was deleted.</summary>
    public Task<bool> DeleteAsync(int userId, Guid id, CancellationToken token = default) => _repository.DeleteAsync(id, userId, token);

    public async Task<ChapterNotesResponse> ListChapterAsync(int? viewerId, int bookId, int chapter, bool others, int? level = null, CancellationToken token = default)
    {
        var rows = await _repository.ListChapterAsync(bookId, chapter, viewerId, others, token);

        return new ChapterNotesResponse { Notes = await ToResponsesAsync(rows, viewerId, level, token) };
    }

    /// <summary>One page of the caller's own notes with the Elb1871 anchor state; the excerpt is the frontend's job.</summary>
    public async Task<MyNotesResponse> ListMineAsync(int userId, ListMyNotesRequest request, CancellationToken token = default)
    {
        var page = await _repository.ListMineAsync(userId, new NoteListFilter(request.Book, request.Visibility, request.Q, request.Skip, request.Take), token);

        return new MyNotesResponse { Total = page.Total, Notes = await ToResponsesAsync(page.Rows, userId, null, token) };
    }

    /// <summary>The normalised anchor (verse: end = start), or null when the Elb1871 text does not have what the anchor points at. The shape is validated before.</summary>
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
            case NoteKinds.Verses when dto.StartHebRefId is { } first && dto.EndHebRefId is { } last:
                return await _repository.VerseExistsAsync(first, token) && await _repository.VerseExistsAsync(last, token)
                    ? new NoteAnchor(NoteKinds.Verses, dto.BookId, dto.Chapter, first, last, null, null, null)
                    : null;
            case NoteKinds.Words when dto.StartHebRefId is { } wordsVerse && dto.StartPosition is { } from && dto.EndPosition is { } to:
                return await ResolveWordsAnchorAsync(dto, wordsVerse, from, to, token);
            default:
                return null;
        }
    }

    /// <summary>The quote is always rebuilt from the Elb1871 words; a client quote is never used. Null when a position is missing or the quote is too long.</summary>
    private async Task<NoteAnchor?> ResolveWordsAnchorAsync(NoteAnchorDto dto, int verse, int from, int to, CancellationToken token)
    {
        var words = (await _repository.GetElbWordsAsync([verse], token)).Where(w => w.PositionInVerse >= from && w.PositionInVerse <= to).ToList();
        if (words.Count != to - from + 1)
        {
            return null;
        }

        var quote = NoteQuote.Join(words.OrderBy(w => w.PositionInVerse).Select(w => w.WordInContext));

        return quote.Length is 0 or > NoteQuote.MaxLength
            ? null
            : new NoteAnchor(NoteKinds.Words, dto.BookId, dto.Chapter, verse, verse, from, to, quote);
    }

    private async Task<List<NoteResponse>> ToResponsesAsync(IReadOnlyList<NoteRecord> rows, int? viewerId, int? level, CancellationToken token)
    {
        var wordVerses = rows.Where(r => r.Kind == NoteKinds.Words && r.StartHebRefId is not null).Select(r => r.StartHebRefId!.Value).Distinct().ToList();
        var elbWords = new Dictionary<int, List<NoteWordRow>>();
        var variantWords = new Dictionary<int, List<NoteVariantWordRow>>();
        if (wordVerses.Count > 0)
        {
            elbWords = (await _repository.GetElbWordsAsync(wordVerses, token)).GroupBy(w => w.HebRefId).ToDictionary(g => g.Key, g => g.ToList());
            if (level is > 0)
            {
                variantWords = (await _repository.GetVariantWordsAsync(wordVerses, level.Value, token)).GroupBy(w => w.HebRefId).ToDictionary(g => g.Key, g => g.ToList());
            }
        }

        return rows.Select(row =>
        {
            var response = ToResponse(row, viewerId);
            if (row.Kind == NoteKinds.Words && row.StartHebRefId is { } verse)
            {
                ApplyWordsState(response, row, elbWords.GetValueOrDefault(verse), variantWords.GetValueOrDefault(verse));
            }

            return response;
        }).ToList();
    }

    /// <summary>drifted: the Elb1871 words at the positions differ from the quote (or are gone); else the variant mapping, if the verse has an active variant.</summary>
    public static void ApplyWordsState(NoteResponse response, NoteRecord row, List<NoteWordRow>? elbWords, List<NoteVariantWordRow>? variantWords)
    {
        var from = row.StartPosition ?? 0;
        var to = row.EndPosition ?? 0;
        var current = (elbWords ?? []).Where(w => w.PositionInVerse >= from && w.PositionInVerse <= to).OrderBy(w => w.PositionInVerse).ToList();
        if (current.Count != to - from + 1 || !NoteQuote.Matches(row.QuotedText, NoteQuote.Join(current.Select(w => w.WordInContext))))
        {
            response.AnchorState = NoteAnchorStates.Drifted;

            return;
        }

        if (variantWords is not { Count: > 0 })
        {
            return;
        }

        var mapped = variantWords
            .Where(w => w.BasePositionInVerse >= from && w.BasePositionInVerse <= to)
            .OrderBy(w => w.PositionInVerse)
            .Select(w => new NoteMappedWordDto { RevisionWordId = w.RevisionWordId, PositionInVerse = w.PositionInVerse, BasePositionInVerse = w.BasePositionInVerse!.Value })
            .ToList();
        response.RevisionVerseId = variantWords[0].RevisionVerseId;
        response.MappedWords = mapped;
        response.AnchorState = mapped.Count == 0 ? NoteAnchorStates.NoBaseWords : NoteAnchorStates.Ok;
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
        BodyLinked = NoteBodyLinker.Link(row.BodyMarkdown),
        Visibility = row.Visibility,
        PublicState = row.PublicState,
        IsOwn = viewerId is not null && row.UserId == viewerId,
        IsHidden = row.IsHidden,
        ReviewNote = viewerId is not null && row.UserId == viewerId ? row.ReviewNote : null,
        AuthorDisplayName = row.AuthorDisplayName,
        CreatedAt = row.CreatedAt,
        UpdatedAt = row.UpdatedAt,
    };

    private const string NotFoundMessage = "No such note of yours.";

    private static string MissingTextMessage(NoteAnchorDto anchor) => anchor.Kind == NoteKinds.Chapter
        ? $"The Elb1871 text has no chapter {anchor.BookId}/{anchor.Chapter}."
        : anchor.Kind == NoteKinds.Words
            ? $"The Elb1871 verse {anchor.StartHebRefId} has no words at positions {anchor.StartPosition}..{anchor.EndPosition} (or the quote would exceed {NoteQuote.MaxLength} characters)."
            : $"The Elb1871 text has no verse {anchor.StartHebRefId}{(anchor.Kind == NoteKinds.Verses ? $" or {anchor.EndHebRefId}" : string.Empty)}.";

    private async Task<NoteOutcome<NoteResponse>> ReadBackAsync(Guid id, int userId, CancellationToken token)
    {
        var row = await _repository.GetAsync(id, userId, token);

        if (row is null)
        {
            return NoteOutcome<NoteResponse>.NotFound(NotFoundMessage);
        }

        return NoteOutcome<NoteResponse>.Ok((await ToResponsesAsync([row], userId, null, token)).Single());
    }
}
