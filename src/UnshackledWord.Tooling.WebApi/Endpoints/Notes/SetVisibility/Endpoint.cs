using FastEndpoints;
using UnshackledWord.Application.Features.Notes;
using UnshackledWord.Domain.WebApi.Notes;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Notes.SetVisibility;

/// <summary>Owner sets private / internal / public (a review request) on a note (roadmap 0023.5).</summary>
[RequiresLogin]
public sealed class Endpoint : NoteEndpoint<SetNoteVisibilityRequest, NoteResponse>
{
    private readonly NoteService _service;

    public Endpoint(NoteService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Put("{id}/visibility");
        Group<RouteGroupConfig>();
        Description(b => b.Produces<NoteResponse>(200).ProducesProblem(400).ProducesProblem(404));
        Summary(s =>
        {
            s.Summary = "Sets the visibility of an own note: `private`, `internal` (all signed-in users, shown with the author's display name) or `public` (a request: `publicState` becomes `pending` until a maintainer approves; meanwhile the note is internal). Going back to private/internal withdraws any approval.";
            s[404] = "No such note, or it belongs to someone else.";
        });
    }

    public override async Task HandleAsync(SetNoteVisibilityRequest req, CancellationToken ct) =>
        await SendOutcomeAsync(await _service.SetVisibilityAsync(UserId, req, ct), StatusCodes.Status200OK, ct);
}

public sealed class Validator : Validator<SetNoteVisibilityRequest>
{
    public Validator()
    {
        Include(new SetNoteVisibilityRequestValidator());
    }
}
