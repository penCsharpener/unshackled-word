using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Revision;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Revision.GetOverview;

[RequiresLogin]
public sealed class Endpoint : Endpoint<RevisionOverviewRequest, RevisionOverviewResponse>
{
    private readonly RevisionOverviewService _service;

    public Endpoint(RevisionOverviewService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("overview");
        Group<RouteGroupConfig>();
        // Reviewers only read the overview; they cannot write variants (roadmap 0011 open question 6).
        Roles(AppRoles.Admin, AppRoles.Maintainer, AppRoles.Reviewer);
        Summary(s => s.Summary = "Revision work overview: revised verses per level and book, sizes of the work lists and one page of a list (list=unreviewed|incomplete|stale, optional level, page, pageSize).");
    }

    public override async Task HandleAsync(RevisionOverviewRequest req, CancellationToken ct)
    {
        await Send.OkAsync(await _service.GetAsync(req, AuthEmailBuilder.ResolveLocale(HttpContext)), ct);
    }
}
