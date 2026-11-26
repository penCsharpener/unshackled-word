using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.Lxx.Review;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentReview;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.LxxReview.Lexicon;

[RequiresLogin]
public sealed class Endpoint : Endpoint<SearchLxxLexiconRequest, SearchLxxLexiconResponse>
{
    private readonly LxxReviewService _service;

    public Endpoint(LxxReviewService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("lxx/lexicon");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "LXX review (roadmap 0028.7f): TBESG lookup for the lemma field. Query q = a Strong's number (G746) or a lemma start (accents optional), limit 1..50 (default 20).");
    }

    public override async Task HandleAsync(SearchLxxLexiconRequest req, CancellationToken ct)
    {
        await Send.OkAsync(await _service.SearchLexiconAsync(req.Q, req.Limit, ct), ct);
    }
}

public sealed class Validator : Validator<SearchLxxLexiconRequest>
{
    public Validator()
    {
        RuleFor(x => x.Q).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Limit).InclusiveBetween(1, LxxReviewService.MaxSearchLimit).When(x => x.Limit is not null);
    }
}
