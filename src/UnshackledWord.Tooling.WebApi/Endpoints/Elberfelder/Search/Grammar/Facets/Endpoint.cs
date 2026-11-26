using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.GrammarSearch;
using UnshackledWord.Domain.WebApi.Elberfelder.SearchGrammar;
using UnshackledWord.Tooling.WebApi.RateLimiting;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.Search.Grammar.Facets;

/// <summary>Values of one key (or all keys) with their word counts for one slot of a grammar query (roadmap 0029.5); public read in the search rate-limit group.</summary>
[RateLimitGroup(RateLimitGroups.Search)]
public class Endpoint : Endpoint<SearchGrammarFacetsRequest>
{
    private readonly IGrammarSearchService _service;

    public Endpoint(IGrammarSearchService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("search/grammar/facets");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Description(b => b.Produces<SearchGrammarFacetsResponse>(200).Produces<GrammarSearchProblem>(400));
        Summary(s => s.Summary = "For slot 'slot' of the query, the values of key 'key' (all keys with fixed values when omitted) with the number of original words that satisfy the slot's lemma and its other filters; the slot's own filter on the key is ignored. A trailing filter without value (Kasus:) is allowed. Values with 0 are omitted. 400 like the grammar search, plus unknownSlot.");
    }

    public override async Task HandleAsync(SearchGrammarFacetsRequest req, CancellationToken ct)
    {
        var result = await _service.FacetsAsync(req.Q, req.Slot, req.Key, ct);
        if (result.Error is { } error)
        {
            await Send.ResponseAsync(GrammarProblemMapper.ToProblem(error), 400, ct);
            return;
        }

        HttpContext.Response.Headers.CacheControl = "public, max-age=60";
        await Send.OkAsync(result.Response!, ct);
    }
}

public sealed class SearchGrammarFacetsRequestValidator : AbstractValidator<SearchGrammarFacetsRequest>
{
    public SearchGrammarFacetsRequestValidator()
    {
        RuleFor(x => x.Q).MaximumLength(SearchGrammarRequest.MaxQueryLength);
        RuleFor(x => x.Slot).InclusiveBetween(0, GrammarQueryParser.MaxSlots - 1);
        RuleFor(x => x.Key).MaximumLength(40);
    }
}

public class SearchGrammarFacetsValidator : Validator<SearchGrammarFacetsRequest>
{
    public SearchGrammarFacetsValidator()
    {
        Include(new SearchGrammarFacetsRequestValidator());
    }
}
