using FastEndpoints;
using UnshackledWord.Application.Features.StrongsStats;
using UnshackledWord.Domain.WebApi.Elberfelder.SearchOriginals;
using UnshackledWord.Tooling.WebApi.RateLimiting;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.Search.Originals;

[RateLimitGroup(RateLimitGroups.Search)]
public class Endpoint : Endpoint<SearchOriginalsRequest, SearchOriginalsResponse>
{
    private readonly ISearchOriginalsService _service;

    public Endpoint(ISearchOriginalsService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("search/originals");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Description(b => b.Produces<SearchOriginalsResponse>(200).ProducesProblem(400).ProducesProblem(503));
        Summary(s => s.Summary = "Per search token: the German lemma + PoS it resolves to and the Greek/Hebrew originals they render (from the stats snapshot). 503 until the snapshot was refreshed once.");
    }

    public override async Task HandleAsync(SearchOriginalsRequest req, CancellationToken ct)
    {
        var response = await _service.LookupAsync(req.Q, req.Top, req.IncludeFrequent, ct);
        if (response is null)
        {
            await Send.StringAsync("The mapping stats were never computed; press \"Neu berechnen\" on the data quality page.", 503, cancellation: ct);
            return;
        }

        HttpContext.Response.Headers.CacheControl = "public, max-age=60";
        await Send.OkAsync(response, ct);
    }
}

public class SearchOriginalsValidator : Validator<SearchOriginalsRequest>
{
    public SearchOriginalsValidator()
    {
        Include(new SearchOriginalsRequestValidator());
    }
}
