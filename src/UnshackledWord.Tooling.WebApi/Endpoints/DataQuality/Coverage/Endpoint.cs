using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.WebApi.DataQuality;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Application.Features.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.DataQuality.Coverage;

[RequiresLogin]
public sealed class Endpoint : Endpoint<CoverageRequest, CoverageResponse>
{
    private readonly IDqFactsRepository _facts;

    public Endpoint(IDqFactsRepository facts)
    {
        _facts = facts;
    }

    public override void Configure()
    {
        Get("coverage");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Per chapter: content words and how many of them are unaligned (metric german | source).");
    }

    public override async Task HandleAsync(CoverageRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "private, max-age=60";
        var state = await _facts.GetStateAsync(ct);
        var books = await _facts.GetCoverageAsync(req.Metric, DqFacets.From(req), ct);
        await Send.OkAsync(new CoverageResponse { Metric = req.Metric, RefreshedAt = state.RefreshedAt, Books = [.. books] }, ct);
    }
}

public sealed class Validator : Validator<CoverageRequest>
{
    public Validator()
    {
        Include(new FacetsValidator());
        RuleFor(x => x.Metric).Must(m => m is "german" or "source").WithMessage("metric must be german or source.");
    }
}
