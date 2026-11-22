using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.Notes;
using UnshackledWord.Domain.WebApi.Notes;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Notes.GetChapter;

/// <summary>The notes of a chapter the caller may see (roadmap 0023.1). Public; anonymous callers fall under the general rate limit.</summary>
public sealed class Endpoint : NoteEndpoint<GetChapterNotesRequest, ChapterNotesResponse>
{
    private readonly NoteService _service;

    public Endpoint(NoteService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("chapter/{bookId}/{chapter}");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Description(b => b.Produces<ChapterNotesResponse>(200).ProducesProblem(400));
        Summary(s => s.Summary = "Notes of a chapter in reading order. Anonymous: approved public notes; signed in: own notes plus others' internal/public ones. `others=false` returns only own notes.");
    }

    public override async Task HandleAsync(GetChapterNotesRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "private, no-store";
        await Send.OkAsync(await _service.ListChapterAsync(ViewerId, req.BookId, req.Chapter, req.Others, ct), ct);
    }
}

public sealed class Validator : Validator<GetChapterNotesRequest>
{
    public Validator()
    {
        RuleFor(x => x.BookId).InclusiveBetween(1, NoteAnchorValidator.MaxBookId);
        RuleFor(x => x.Chapter).InclusiveBetween(1, NoteAnchorValidator.MaxChapter);
    }
}
