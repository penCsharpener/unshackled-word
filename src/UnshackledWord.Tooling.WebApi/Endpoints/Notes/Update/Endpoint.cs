using FastEndpoints;
using UnshackledWord.Application.Features.Notes;
using UnshackledWord.Domain.WebApi.Notes;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Notes.Update;

/// <summary>Replaces body (and optionally anchor) of the caller's own note (roadmap 0023.1).</summary>
[RequiresLogin]
public sealed class Endpoint : NoteEndpoint<UpdateNoteRequest, NoteResponse>
{
    private readonly NoteService _service;

    public Endpoint(NoteService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Put("{id}");
        Group<RouteGroupConfig>();
        Description(b => b.Produces<NoteResponse>(200).ProducesProblem(400).ProducesProblem(404));
        Summary(s =>
        {
            s.Summary = "Updates the body and, when `anchor` is given, the anchor of an own note (\"Neu verankern\": same validation as create, `quotedText` is refreshed). An approved public note goes back to review.";
            s[404] = "No such note, or it belongs to someone else.";
        });
    }

    public override async Task HandleAsync(UpdateNoteRequest req, CancellationToken ct) =>
        await SendOutcomeAsync(await _service.UpdateAsync(UserId, req, ct), StatusCodes.Status200OK, ct);
}

public sealed class Validator : Validator<UpdateNoteRequest>
{
    public Validator()
    {
        Include(new UpdateNoteRequestValidator());
    }
}
