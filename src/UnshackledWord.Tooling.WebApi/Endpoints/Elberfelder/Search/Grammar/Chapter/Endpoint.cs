using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.GrammarSearch;
using UnshackledWord.Domain.WebApi.Elberfelder.SearchGrammar;
using UnshackledWord.Tooling.WebApi.RateLimiting;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.Search.Grammar.Chapter;

/// <summary>All hits of a grammar query in one Elb1871 chapter, for marking in the reader (roadmap 0029.2); public read in the search rate-limit group.</summary>
[RateLimitGroup(RateLimitGroups.Search)]
public class Endpoint : Endpoint<SearchGrammarChapterRequest>
{
    private readonly IGrammarSearchService _service;

    public Endpoint(IGrammarSearchService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("search/grammar/chapter/{bibleBookId}/{chapter}");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Description(b => b.Produces<SearchGrammarChapterResponse>(200).Produces<GrammarSearchProblem>(400));
        Summary(s => s.Summary = "Hits of a grammar query (a query of words only is valid) in one chapter: per verse the Elb1871 word positions and the ids of the matched original words. 400 like the grammar search.");
    }

    public override async Task HandleAsync(SearchGrammarChapterRequest req, CancellationToken ct)
    {
        var result = await _service.ChapterAsync(req.Q, req.BibleBookId, req.Chapter, ct);
        if (result.Error is { } error)
        {
            await Send.ResponseAsync(GrammarProblemMapper.ToProblem(error), 400, ct);
            return;
        }

        HttpContext.Response.Headers.CacheControl = "public, max-age=60";
        await Send.OkAsync(result.Response!, ct);
    }
}

public sealed class SearchGrammarChapterRequestValidator : AbstractValidator<SearchGrammarChapterRequest>
{
    public SearchGrammarChapterRequestValidator()
    {
        RuleFor(x => x.Q).MaximumLength(SearchGrammarRequest.MaxQueryLength);
        RuleFor(x => x.BibleBookId).InclusiveBetween(1, 66);
        RuleFor(x => x.Chapter).GreaterThanOrEqualTo(1);
    }
}

public class SearchGrammarChapterValidator : Validator<SearchGrammarChapterRequest>
{
    public SearchGrammarChapterValidator()
    {
        Include(new SearchGrammarChapterRequestValidator());
    }
}
