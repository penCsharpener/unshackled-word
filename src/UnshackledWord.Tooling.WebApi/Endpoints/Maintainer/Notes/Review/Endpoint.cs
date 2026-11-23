using System.Security.Claims;
using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.Notes;
using UnshackledWord.Domain.WebApi.Notes;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.Notes.Review;

/// <summary>The review queue: pending public notes and hidden notes, each with current and approved text (roadmap 0023.5).</summary>
[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<ReviewNotesResponse>
{
    private readonly NoteService _service;

    public Endpoint(NoteService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("notes/review");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Maintainer: public notes waiting for approval (`pending`) and hidden notes. `note.bodyMarkdown` is the author's current text, `approvedBodyMarkdown` the text anonymous readers see (null when never approved), so the page can show a diff.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "private, no-store";
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await Send.OkAsync(await _service.ListReviewAsync(userId, ct), ct);
    }
}
