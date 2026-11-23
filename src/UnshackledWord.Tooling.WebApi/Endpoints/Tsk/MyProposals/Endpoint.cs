using System.Security.Claims;
using FastEndpoints;
using UnshackledWord.Application.Features.TskCuration;
using UnshackledWord.Domain.WebApi.Tsk;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Tsk.MyProposals;

/// <summary>The caller's own TSK curation proposals (roadmap 0024.6), all states, so the editor can show a rejection reason.</summary>
[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<MyTskProposalsResponse>
{
    private readonly TskProposalReviewService _service;

    public Endpoint(TskProposalReviewService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("proposals/mine");
        Group<RouteGroupConfig>();
        Summary(s => s.Summary = "Logged-in user: own TSK curation proposals, newest first (at most 200), with `state` (pending, accepted, rejected, superseded), "
                                 + "`proposed` (full desired state), `createdAt`, `decidedAt` and the rejection `reason`.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "private, no-store";
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await Send.OkAsync(await _service.ListMineAsync(userId, ct), ct);
    }
}
