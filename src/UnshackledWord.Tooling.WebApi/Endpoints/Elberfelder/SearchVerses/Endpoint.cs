using FastEndpoints;
using UnshackledWord.Application.Features.VerseSearch;
using UnshackledWord.Domain.WebApi.Elberfelder.SearchVerses;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.SearchVerses;

public class Endpoint : Endpoint<SearchVersesRequest, SearchVersesResponse>
{
    private readonly IVerseSearchService _service;

    public Endpoint(IVerseSearchService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("search/verses");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Description(b => b.Produces<SearchVersesResponse>(200).ProducesProblem(400));
        Summary(s => s.Summary = "Full text search over the Elberfelder verses, ranked (literal hits first, then lemma hits), with the matching words marked and the hits per book.");
    }

    public override async Task HandleAsync(SearchVersesRequest req, CancellationToken ct)
    {
        var result = await _service.SearchAsync(req.Q, req.Page, req.PageSize, req.BookId is 0 ? null : req.BookId, req.Level, ct);
        if (result is null)
        {
            await Send.ErrorsAsync(400, ct);
            return;
        }

        HttpContext.Response.Headers.CacheControl = "public, max-age=300";
        await Send.OkAsync(result, ct);
    }
}

public class SearchVersesValidator : Validator<SearchVersesRequest>
{
    public SearchVersesValidator()
    {
        Include(new SearchVersesRequestValidator());
    }
}
