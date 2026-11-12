using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.WebApi.DataQuality;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.DataQuality.Overview;

[RequiresLogin]
public sealed class Endpoint : Endpoint<OverviewRequest, OverviewResponse>
{
    private readonly DqOverviewService _overview;

    public Endpoint(DqOverviewService overview)
    {
        _overview = overview;
    }

    public override void Configure()
    {
        Get("overview");
        Group<RouteGroupConfig>();
        Summary(s => s.Summary = "Data quality tiles: count / error count / previous count per check, snapshot timestamp and refresh state.");
    }

    public override async Task HandleAsync(OverviewRequest req, CancellationToken ct)
    {
        // The page polls this while a refresh runs, so it must never be served from a browser cache.
        HttpContext.Response.Headers.CacheControl = "private, no-cache";
        await Send.OkAsync(await _overview.GetAsync(DqFacets.From(req), ct), ct);
    }
}

public sealed class Validator : Validator<OverviewRequest>
{
    public Validator()
    {
        Include(new FacetsValidator());
    }
}
