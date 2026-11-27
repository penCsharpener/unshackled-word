using FastEndpoints;
using UnshackledWord.Application.Features.VerseSearch;
using UnshackledWord.Domain.WebApi.Elberfelder.SearchVerses;
using UnshackledWord.Tooling.WebApi.RateLimiting;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Elberfelder.SearchVerses.Chapter;

/// <summary>Matched word positions of a verse query in one chapter, for marking in the reader (roadmap 0029.10); public read in the search rate-limit group.</summary>
[RateLimitGroup(RateLimitGroups.Search)]
public class Endpoint : Endpoint<SearchVersesChapterRequest, SearchVersesChapterResponse>
{
    private readonly IVerseSearchService _service;

    public Endpoint(IVerseSearchService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("search/verses/chapter/{bibleBookId}/{chapter}");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Description(b => b.Produces<SearchVersesChapterResponse>(200).ProducesProblem(400));
        Summary(s => s.Summary = "Verses of one chapter that match the whole verse query, with the positions of the words that match (positions of the text the level shows; revised: true = positions of the revision variant).");
    }

    public override async Task HandleAsync(SearchVersesChapterRequest req, CancellationToken ct)
    {
        var result = await _service.ChapterAsync(req.Q, req.BibleBookId, req.Chapter, req.Level, ct);
        if (result is null)
        {
            await Send.ErrorsAsync(400, ct);
            return;
        }

        HttpContext.Response.Headers.CacheControl = "public, max-age=60";
        await Send.OkAsync(result, ct);
    }
}

public class SearchVersesChapterValidator : Validator<SearchVersesChapterRequest>
{
    public SearchVersesChapterValidator()
    {
        Include(new SearchVersesChapterRequestValidator());
    }
}
