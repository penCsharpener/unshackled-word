using System.Security.Claims;
using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.Notes;
using UnshackledWord.Domain.WebApi.Notes;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.Notes.Unhide;

/// <summary>Maintainer unhides a note (roadmap 0023.5); the action is appended to the admin log.</summary>
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
        Post("notes/{id}/unhide");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Description(b => b.Produces(204).ProducesProblem(400).ProducesProblem(404));
        Summary(s =>
        {
            s.Summary = "Maintainer: shows a hidden note again.";
            s[400] = "The note is not hidden.";
            s[404] = "No such note.";
        });
    }

    public override async Task HandleAsync(NoteReviewActionRequest req, CancellationToken ct)
    {
        var actorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _service.UnhideAsync(actorId, req.Id, ct);

        if (result.Status == NoteStatus.Ok)
        {
            await Send.NoContentAsync(ct);
            return;
        }

        AddError(result.Message ?? "The request was refused.");
        await Send.ErrorsAsync(result.Status == NoteStatus.NotFound ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest, ct);
    }
}
