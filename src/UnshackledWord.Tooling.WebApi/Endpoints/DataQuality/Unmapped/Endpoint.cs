using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.WebApi.DataQuality;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.DataQuality.Unmapped;

[RequiresLogin]
public sealed class Endpoint : Endpoint<UnmappedRequest, UnmappedResponse>
{
    private readonly IDqFactsRepository _facts;

    public Endpoint(IDqFactsRepository facts)
    {
        _facts = facts;
    }

    public override void Configure()
    {
        Get("unmapped/{side}");
        Group<RouteGroupConfig>();
        Summary(s => s.Summary = "Words without counterpart (german | greek | hebrew): split by class, by book and the most frequent lemmas / Strong's numbers.");
    }

    public override async Task HandleAsync(UnmappedRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "private, max-age=60";
        var state = await _facts.GetStateAsync(ct);
        var response = await _facts.GetUnmappedAsync(req.Side, req.Content, req.Top, DqFacets.From(req), ct);
        response.RefreshedAt = state.RefreshedAt;
        await Send.OkAsync(response, ct);
    }
}

public sealed class Validator : Validator<UnmappedRequest>
{
    public Validator()
    {
        Include(new FacetsValidator());
        RuleFor(x => x.Side).Must(s => s is "german" or "greek" or "hebrew").WithMessage("side must be german, greek or hebrew.");
        RuleFor(x => x.Top).InclusiveBetween(1, 100);
    }
}
