using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.TskCuration;
using UnshackledWord.Domain.WebApi.Tsk;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.TskProposals.List;

/// <summary>The review queue of TSK curation proposals (roadmap 0024.6): every pending proposal with current state, proposed state and the stale flag.</summary>
[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<TskProposalReviewListResponse>
{
    private readonly TskProposalReviewService _service;

    public Endpoint(TskProposalReviewService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("tsk/proposals");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Maintainer: pending TSK curation proposals ordered by source verse, target, creation time (at most 1000). Each has `current` "
                                 + "(the link's curation row, omitted when none), `proposed` (full desired state, `delete` true = withdraw the added link), "
                                 + "author, `createdAt`, `inTsk` and `stale` (the link changed since the proposal was made: accepting needs `force`).");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "private, no-store";
        await Send.OkAsync(await _service.ListPendingAsync(ct), ct);
    }
}
