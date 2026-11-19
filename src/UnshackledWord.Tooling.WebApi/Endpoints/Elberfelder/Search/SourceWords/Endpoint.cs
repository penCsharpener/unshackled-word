using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.WebApi.Elberfelder.SearchSourceWords;
using UnshackledWord.Tooling.WebApi.RateLimiting;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.Search.SourceWords;

[RateLimitGroup(RateLimitGroups.Search)]
public class Endpoint : Endpoint<SearchSourceWordsRequest, SearchSourceWordsResponse>
{
    private readonly ISourceWordSearchService _service;

    public Endpoint(ISourceWordSearchService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("search/sourcewords");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Description(b => b.Produces<SearchSourceWordsResponse>(200).ProducesProblem(400).ProducesProblem(503));
        Summary(s => s.Summary = "Hebrew / Greek words by script (diacritics optional), Strong's number, transliteration or gloss: the lemma entries and the inflected forms of the text that match, with occurrences and a few verses. 503 until the data quality facts were computed once.");
    }

    public override async Task HandleAsync(SearchSourceWordsRequest req, CancellationToken ct)
    {
        var response = await _service.SearchAsync(req.Q, req.Limit, ct);
        if (response is null)
        {
            await Send.StringAsync("The source word statistics were never computed; press \"Neu berechnen\" on the data quality page.", 503, cancellation: ct);
            return;
        }

        HttpContext.Response.Headers.CacheControl = "public, max-age=60";
        await Send.OkAsync(response, ct);
    }
}

public sealed class SearchSourceWordsRequestValidator : AbstractValidator<SearchSourceWordsRequest>
{
    public SearchSourceWordsRequestValidator()
    {
        RuleFor(x => x.Q)
            .NotEmpty()
            .MaximumLength(SearchSourceWordsRequest.MaxQueryLength);
        RuleFor(x => SourceWordSearchService.Tokenize(x.Q))
            .Must(t => t.Count >= 1)
            .WithName(nameof(SearchSourceWordsRequest.Q))
            .WithMessage("Query must contain at least one word.")
            .Must(t => t.Count <= SearchSourceWordsRequest.MaxTokens)
            .WithMessage($"Query may contain at most {SearchSourceWordsRequest.MaxTokens} different words.")
            .Must(t => t.All(w => w.Length <= SearchSourceWordsRequest.MaxTokenLength))
            .WithMessage($"A word may have at most {SearchSourceWordsRequest.MaxTokenLength} letters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Q) && x.Q.Length <= SearchSourceWordsRequest.MaxQueryLength);
        RuleFor(x => x.Limit).InclusiveBetween(1, SearchSourceWordsRequest.MaxLimit);
    }
}

public class SearchSourceWordsValidator : Validator<SearchSourceWordsRequest>
{
    public SearchSourceWordsValidator()
    {
        Include(new SearchSourceWordsRequestValidator());
    }
}
