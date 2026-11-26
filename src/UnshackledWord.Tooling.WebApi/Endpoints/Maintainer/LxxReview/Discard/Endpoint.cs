using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.Lxx.Review;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentReview;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.LxxReview.Discard;

[RequiresLogin]
public sealed class Endpoint : Endpoint<DiscardLxxUnitRequest, DiscardLxxUnitResponse>
{
    private readonly LxxReviewService _service;

    public Endpoint(LxxReviewService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("lxx/review/discard");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "LXX review (roadmap 0028.7f): discards the open AI proposal of a unit (status discarded, one LxxTagAudit row with action reject). Tags and nt-form proposals stay as they are. 404 unknown unit, 409 when the proposal is not open or not the one shown.");
    }

    public override async Task HandleAsync(DiscardLxxUnitRequest req, CancellationToken ct)
    {
        var reviewer = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _service.DiscardAsync(req, reviewer, ct);
        if (result.Outcome != AgentReviewOutcome.Ok)
        {
            foreach (var error in result.Errors)
            {
                AddError(error);
            }

            await Send.ErrorsAsync(ReviewHttp.StatusOf(result.Outcome), ct);

            return;
        }

        await Send.OkAsync(result.Value!, ct);
    }
}

public sealed class Validator : Validator<DiscardLxxUnitRequest>
{
    public Validator()
    {
        RuleFor(x => x.LxxVerseId).GreaterThan(0);
        RuleFor(x => x.ProposalId).GreaterThan(0);
    }
}
