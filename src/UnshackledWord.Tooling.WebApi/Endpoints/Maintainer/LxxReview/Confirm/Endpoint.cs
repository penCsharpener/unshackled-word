using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.Lxx.Review;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentReview;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.LxxReview.Confirm;

[RequiresLogin]
public sealed class Endpoint : Endpoint<ConfirmLxxUnitRequest, ConfirmLxxUnitResponse>
{
    private readonly LxxReviewService _service;

    public Endpoint(LxxReviewService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("lxx/review/confirm");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "LXX review (roadmap 0028.7f): confirms one unit as the maintainer shows it (final tag and Hebrew links per word). One transaction writes confirmed LxxWordTags (source ai, nt-form or human by the source rule), replaces the words' links, appends one LxxTagAudit row (confirm, or edit when a confirmed tag or link changed) and closes the AI proposal (merged, or edited-merged). 400 when a rule fails (Strong's not in TBESG, lemma mismatch, bad parse, Hebrew word outside the verse), 404 unknown unit, 409 when the open proposal is not the one shown.");
    }

    public override async Task HandleAsync(ConfirmLxxUnitRequest req, CancellationToken ct)
    {
        var reviewer = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _service.ConfirmAsync(req, reviewer, ct);
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

public sealed class Validator : Validator<ConfirmLxxUnitRequest>
{
    public Validator()
    {
        RuleFor(x => x.LxxVerseId).GreaterThan(0);
        RuleFor(x => x.ProposalId).GreaterThan(0).When(x => x.ProposalId is not null);
        RuleFor(x => x.Words).NotNull().Must(w => w.Count <= 2000).WithMessage("Too many words.");
        RuleForEach(x => x.Words).ChildRules(w => w.RuleFor(i => i.LxxWordId).GreaterThan(0));
    }
}
