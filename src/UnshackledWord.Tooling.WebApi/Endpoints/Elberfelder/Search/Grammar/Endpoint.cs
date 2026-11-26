using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.GrammarSearch;
using UnshackledWord.Domain.WebApi.Elberfelder.SearchGrammar;
using UnshackledWord.Tooling.WebApi.RateLimiting;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.Search.Grammar;

/// <summary>Grammar search over the Greek NT words (roadmap 0029.1); public read in the search rate-limit group.</summary>
[RateLimitGroup(RateLimitGroups.Search)]
public class Endpoint : Endpoint<SearchGrammarRequest>
{
    private readonly IGrammarSearchService _service;

    public Endpoint(IGrammarSearchService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("search/grammar");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Description(b => b.Produces<SearchGrammarResponse>(200).Produces<GrammarSearchProblem>(400));
        Summary(s => s.Summary = "Grammar query over the Greek words, answered per Elb1871 verse: words (Strong's, Greek script, transliteration) with filters such as Kasus:nom, Tempus:aor, -Modus:partizip or Code:V-2A*. 400 with code, token and candidates when the query cannot be searched.");
    }

    public override async Task HandleAsync(SearchGrammarRequest req, CancellationToken ct)
    {
        var result = await _service.SearchAsync(req.Q, req.Page, req.Book, ct);
        if (result.Error is { } error)
        {
            var problem = new GrammarSearchProblem
            {
                Code = error.Code,
                Token = error.Token,
                Candidates = error.Candidates is null ? null : [.. error.Candidates],
            };
            await Send.ResponseAsync(problem, 400, ct);
            return;
        }

        HttpContext.Response.Headers.CacheControl = "public, max-age=60";
        await Send.OkAsync(result.Response!, ct);
    }
}

public sealed class SearchGrammarRequestValidator : AbstractValidator<SearchGrammarRequest>
{
    public SearchGrammarRequestValidator()
    {
        RuleFor(x => x.Q).MaximumLength(SearchGrammarRequest.MaxQueryLength);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.Book).InclusiveBetween(1, 66).When(x => x.Book is not null and not 0);
    }
}

public class SearchGrammarValidator : Validator<SearchGrammarRequest>
{
    public SearchGrammarValidator()
    {
        Include(new SearchGrammarRequestValidator());
    }
}
