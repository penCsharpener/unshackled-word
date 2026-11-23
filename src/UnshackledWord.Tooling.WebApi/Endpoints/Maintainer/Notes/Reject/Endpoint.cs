using System.Security.Claims;
using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.Notes;
using UnshackledWord.Domain.WebApi.Notes;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.Notes.Reject;

/// <summary>Maintainer rejects a pending public note (roadmap 0023.5); the action is appended to the admin log.</summary>
[RequiresLogin]
public sealed class Endpoint : Endpoint<NoteReviewActionRequest>
{
    private readonly NoteReviewService _service;

    public Endpoint(NoteReviewService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("notes/{id}/reject");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Description(b => b.Produces(204).ProducesProblem(400).ProducesProblem(404));
        Summary(s =>
        {
            s.Summary = "Maintainer: rejects a pending public note with an optional `reason`. The note stays internal (an earlier approval is withdrawn); the author may edit and request public again.";
            s[400] = "The note is not waiting for approval.";
            s[404] = "No such note.";
        });
    }

    public override async Task HandleAsync(NoteReviewActionRequest req, CancellationToken ct)
    {
        var actorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _service.RejectAsync(actorId, req.Id, req.Reason, ct);

        if (result.Status == NoteStatus.Ok)
        {
            await Send.NoContentAsync(ct);
            return;
        }

        AddError(result.Message ?? "The request was refused.");
        await Send.ErrorsAsync(result.Status == NoteStatus.NotFound ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest, ct);
    }
}

public sealed class Validator : Validator<NoteReviewActionRequest>
{
    public Validator()
    {
        Include(new NoteReviewActionRequestValidator());
    }
}
