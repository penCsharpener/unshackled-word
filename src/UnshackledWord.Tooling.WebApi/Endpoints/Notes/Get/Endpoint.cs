using FastEndpoints;
using UnshackledWord.Application.Features.Notes;
using UnshackledWord.Domain.WebApi.Notes;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Notes.Get;

/// <summary>One note the caller may see (roadmap 0023, 05-api-and-ui.md). Public; anonymous callers get the approved text and fall under the general rate limit.</summary>
public sealed class Endpoint : NoteEndpoint<GetNoteRequest, NoteResponse>
{
    private readonly NoteService _service;

    public Endpoint(NoteService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("{id:guid}");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Description(b => b.Produces<NoteResponse>(200).ProducesProblem(404));
        Summary(s =>
        {
            s.Summary = "One note with the same visibility filter as the chapter list (anonymous: approved public notes with the approved text). Anchor state against Elb1871.";
            s[404] = "No such note, or the caller may not see it.";
        });
    }

    public override async Task HandleAsync(GetNoteRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "private, no-store";
        await SendOutcomeAsync(await _service.GetAsync(ViewerId, req.Id, ct), StatusCodes.Status200OK, ct);
    }
}
