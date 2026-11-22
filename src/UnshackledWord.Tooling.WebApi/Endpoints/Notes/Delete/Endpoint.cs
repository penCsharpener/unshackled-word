using System.Security.Claims;
using FastEndpoints;
using UnshackledWord.Application.Features.Notes;
using UnshackledWord.Domain.WebApi.Notes;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Notes.Delete;

/// <summary>Hard-deletes the caller's own note (roadmap 0023.1).</summary>
[RequiresLogin]
public sealed class Endpoint : Endpoint<DeleteNoteRequest>
{
    private readonly NoteService _service;

    public Endpoint(NoteService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Delete("{id}");
        Group<RouteGroupConfig>();
        Description(b => b.Produces(204).Produces(404));
        Summary(s =>
        {
            s.Summary = "Deletes one of the caller's own notes.";
            s[404] = "No such note, or it belongs to someone else.";
        });
    }

    public override async Task HandleAsync(DeleteNoteRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (await _service.DeleteAsync(userId, req.Id, ct))
        {
            await Send.NoContentAsync(ct);
            return;
        }

        await Send.NotFoundAsync(ct);
    }
}
