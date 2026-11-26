using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.Lxx.Review;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentReview;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.LxxReview.Morphology;

[RequiresLogin]
public sealed class Endpoint : Endpoint<CheckLxxMorphologyRequest, CheckLxxMorphologyResponse>
{
    public override void Configure()
    {
        Get("lxx/morphology");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "LXX review (roadmap 0028.7f): live check of a Robinson parse code (query code, because crasis codes contain spaces and +). Always 200; valid=false carries the reason.");
    }

    public override async Task HandleAsync(CheckLxxMorphologyRequest req, CancellationToken ct)
    {
        await Send.OkAsync(LxxReviewService.CheckMorphology(req.Code), ct);
    }
}

public sealed class Validator : Validator<CheckLxxMorphologyRequest>
{
    public Validator()
    {
        RuleFor(x => x.Code).MaximumLength(40);
    }
}
