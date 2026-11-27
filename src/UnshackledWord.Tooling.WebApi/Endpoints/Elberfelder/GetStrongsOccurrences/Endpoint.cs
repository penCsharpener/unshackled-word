using FastEndpoints;
using UnshackledWord.Application.Features.StrongsOccurrences;
using UnshackledWord.Application.Features.StrongsStats;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetStrongsOccurrences;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Domain.WebApi.Elberfelder.SearchGrammar;
using UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.Search.Grammar;
using UnshackledWord.Tooling.WebApi.RateLimiting;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.GetStrongsOccurrences;

[RateLimitGroup(RateLimitGroups.Search)]
public class Endpoint : Endpoint<GetStrongsOccurrencesRequest>
{
    private readonly IStrongsOccurrenceService _service;

    public Endpoint(IStrongsOccurrenceService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("strongsOccurrences/strongs/{Strongs}");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Description(b => b.Produces<GetStrongsOccurrencesResponse>(200).Produces<GrammarSearchProblem>(400));
    }

    public override async Task HandleAsync(GetStrongsOccurrencesRequest req, CancellationToken ct)
    {
        if (req.Corpus == "lxx")
        {
            var lxx = await _service.GetLxxAsync(req.Strongs, req.Page, req.PageSize, User.HasWriteRole(), ct);
            if (lxx is null)
            {
                await Send.ErrorsAsync(400, ct);
                return;
            }

            // The count of unconfirmed tags depends on the role, and a confirmation must show on the next request.
            HttpContext.Response.Headers.CacheControl = "private, no-cache";
            await Send.OkAsync(lxx, ct);
            return;
        }

        var result = await _service.GetAsync(req.Strongs, req.Page, req.PageSize, new StrongsOccurrenceFilter(req.Book, req.Rendering), req.Grammar, ct);
        if (result.Error is { } error)
        {
            await Send.ResponseAsync(GrammarProblemMapper.ToProblem(error), 400, ct);
            return;
        }

        if (result.Response is null)
        {
            await Send.ErrorsAsync(400, ct);
            return;
        }

        HttpContext.Response.Headers.CacheControl = "public, max-age=3600";
        await Send.OkAsync(result.Response, ct);
    }
}

public class GetStrongsOccurrencesValidator : Validator<GetStrongsOccurrencesRequest>
{
    public GetStrongsOccurrencesValidator()
    {
        Include(new GetStrongsOccurrencesRequestValidator());
    }
}
