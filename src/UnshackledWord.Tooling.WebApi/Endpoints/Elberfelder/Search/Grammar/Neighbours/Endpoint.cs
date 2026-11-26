using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.GrammarSearch;
using UnshackledWord.Domain.WebApi.Elberfelder.SearchGrammar;
using UnshackledWord.Tooling.WebApi.RateLimiting;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.Search.Grammar.Neighbours;

/// <summary>The nearest chapters before and after a chapter that have a hit of a grammar query (roadmap 0029.2); public read in the search rate-limit group.</summary>
[RateLimitGroup(RateLimitGroups.Search)]
public class Endpoint : Endpoint<SearchGrammarNeighboursRequest>
{
    private readonly IGrammarSearchService _service;

    public Endpoint(IGrammarSearchService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("search/grammar/neighbours");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Description(b => b.Produces<SearchGrammarNeighboursResponse>(200).Produces<GrammarSearchProblem>(400));
        Summary(s => s.Summary = "Nearest chapter before and after the given one with at least one hit of the grammar query (canonical order; a side without a hit is omitted). 400 like the grammar search.");
    }

    public override async Task HandleAsync(SearchGrammarNeighboursRequest req, CancellationToken ct)
    {
        var result = await _service.NeighboursAsync(req.Q, req.BibleBookId, req.Chapter, ct);
        if (result.Error is { } error)
        {
            await Send.ResponseAsync(GrammarProblemMapper.ToProblem(error), 400, ct);
            return;
        }

        HttpContext.Response.Headers.CacheControl = "public, max-age=60";
        await Send.OkAsync(result.Response!, ct);
    }
}

public sealed class SearchGrammarNeighboursRequestValidator : AbstractValidator<SearchGrammarNeighboursRequest>
{
    public SearchGrammarNeighboursRequestValidator()
    {
        RuleFor(x => x.Q).MaximumLength(SearchGrammarRequest.MaxQueryLength);
        RuleFor(x => x.BibleBookId).InclusiveBetween(1, 66);
        RuleFor(x => x.Chapter).GreaterThanOrEqualTo(1);
    }
}

public class SearchGrammarNeighboursValidator : Validator<SearchGrammarNeighboursRequest>
{
    public SearchGrammarNeighboursValidator()
    {
        Include(new SearchGrammarNeighboursRequestValidator());
    }
}
