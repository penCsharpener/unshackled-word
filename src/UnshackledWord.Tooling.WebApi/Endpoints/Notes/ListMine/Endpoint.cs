using FastEndpoints;
using UnshackledWord.Application.Features.Notes;
using UnshackledWord.Domain.WebApi.Notes;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Notes.ListMine;

/// <summary>The signed-in user's own notes, filtered and paged (roadmap 0023.4).</summary>
[RequiresLogin]
public sealed class Endpoint : NoteEndpoint<ListMyNotesRequest, MyNotesResponse>
{
    private readonly NoteService _service;

    public Endpoint(NoteService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("mine");
        Group<RouteGroupConfig>();
        Description(b => b.Produces<MyNotesResponse>(200).ProducesProblem(400));
        Summary(s => s.Summary = "Own notes (all visibilities), newest change first. Filters `book` (1..66), `visibility`, `q` (case-insensitive text in the body, wildcards literal); paging `skip` and `take` (1..50, default 50). `total` counts all matches.");
    }

    public override async Task HandleAsync(ListMyNotesRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "private, no-store";
        await Send.OkAsync(await _service.ListMineAsync(UserId, req, ct), ct);
    }
}

public sealed class Validator : Validator<ListMyNotesRequest>
{
    public Validator()
    {
        Include(new ListMyNotesRequestValidator());
    }
}
