using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.Lxx.Review;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentReview;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.LxxReview.Chapter;

[RequiresLogin]
public sealed class Endpoint : Endpoint<GetLxxReviewChapterRequest, LxxReviewChapterResponse>
{
    private readonly LxxReviewService _service;

    public Endpoint(LxxReviewService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("lxx/review/{bibleBookId}/{chapter}");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "LXX review (roadmap 0028.7f): every unit of one Rahlfs chapter in Swete order with words, current tag (any status), stored links, the unit's Hebrew words, the open AI proposal and its per-word answers and flags. 404 when the book has no such chapter.");
    }

    public override async Task HandleAsync(GetLxxReviewChapterRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        var response = await _service.GetChapterAsync(req.BibleBookId, req.Chapter, ct);
        if (response.Units.Count == 0)
        {
            AddError("No LXX verses in this chapter.");
            await Send.ErrorsAsync(StatusCodes.Status404NotFound, ct);

            return;
        }

        await Send.OkAsync(response, ct);
    }
}

public sealed class Validator : Validator<GetLxxReviewChapterRequest>
{
    public Validator()
    {
        RuleFor(x => x.BibleBookId).InclusiveBetween(1, 39);
        RuleFor(x => x.Chapter).GreaterThan(0);
    }
}
