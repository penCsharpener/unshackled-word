using FastEndpoints;
using UnshackledWord.Application.Features.VerseSearch;
using UnshackledWord.Domain.WebApi.Elberfelder.SearchVerses;
using UnshackledWord.Tooling.WebApi.RateLimiting;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.SearchVerses.Neighbours;

/// <summary>The nearest chapters before and after a chapter with a hit of a verse query (roadmap 0029.10); public read in the search rate-limit group.</summary>
[RateLimitGroup(RateLimitGroups.Search)]
public class Endpoint : Endpoint<SearchVersesNeighboursRequest, SearchVersesNeighboursResponse>
{
    private readonly IVerseSearchService _service;

    public Endpoint(IVerseSearchService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("search/verses/neighbours");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Description(b => b.Produces<SearchVersesNeighboursResponse>(200).ProducesProblem(400));
        Summary(s => s.Summary = "Nearest chapter before and after the given one with at least one verse hit (canonical order; a side without a hit is omitted).");
    }

    public override async Task HandleAsync(SearchVersesNeighboursRequest req, CancellationToken ct)
    {
        var result = await _service.NeighboursAsync(req.Q, req.BibleBookId, req.Chapter, req.Level, ct);
        if (result is null)
        {
            await Send.ErrorsAsync(400, ct);
            return;
        }

        HttpContext.Response.Headers.CacheControl = "public, max-age=60";
        await Send.OkAsync(result, ct);
    }
}

public class SearchVersesNeighboursValidator : Validator<SearchVersesNeighboursRequest>
{
    public SearchVersesNeighboursValidator()
    {
        Include(new SearchVersesNeighboursRequestValidator());
    }
}
