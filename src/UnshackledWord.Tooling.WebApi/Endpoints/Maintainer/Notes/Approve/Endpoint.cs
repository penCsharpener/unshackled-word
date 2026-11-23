using System.Security.Claims;
using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.Notes;
using UnshackledWord.Domain.WebApi.Notes;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.Notes.Approve;

/// <summary>Maintainer approves a pending public note (roadmap 0023.5); the action is appended to the admin log.</summary>
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
        Post("notes/{id}/approve");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Description(b => b.Produces(204).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409));
        Summary(s =>
        {
            s.Summary = "Maintainer: approves a pending public note. The current text becomes the approved snapshot that anonymous readers see. Send `reviewedVersion` (the note's `updatedAt ?? createdAt` from the queue) so an edit made after the review is not approved unseen.";
            s[400] = "The note is not waiting for approval.";
            s[404] = "No such note.";
            s[409] = "The author changed the note after the given reviewedVersion; nothing was approved.";
        });
    }

    public override async Task HandleAsync(NoteReviewActionRequest req, CancellationToken ct)
    {
        var actorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _service.ApproveAsync(actorId, req.Id, req.ReviewedVersion, ct);

        if (result.Status == NoteStatus.Ok)
        {
            await Send.NoContentAsync(ct);
            return;
        }

        AddError(result.Message ?? "The request was refused.");
        await Send.ErrorsAsync(result.Status switch
        {
            NoteStatus.NotFound => StatusCodes.Status404NotFound,
            NoteStatus.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        }, ct);
    }
}
