using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.Lxx.Review;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentReview;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.LxxReview.ConfirmSafe;

[RequiresLogin]
public sealed class Endpoint : Endpoint<ConfirmLxxSafeRequest, ConfirmLxxSafeResponse>
{
    private readonly LxxReviewService _service;

    public Endpoint(LxxReviewService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("lxx/review/confirm-chapter-safe");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "LXX review (roadmap 0028.7f): confirms only the listed words (the ones the maintainer saw) whose open AI answer has confidence high and that are not confirmed yet; low words, words without an AI answer and nt-form prefills alone stay open. Units whose open proposal is not the one shown are reported as stale. One transaction, one audit row per unit.");
    }

    public override async Task HandleAsync(ConfirmLxxSafeRequest req, CancellationToken ct)
    {
        var reviewer = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _service.ConfirmSafeAsync(req, reviewer, ct);
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

public sealed class Validator : Validator<ConfirmLxxSafeRequest>
{
    public Validator()
    {
        RuleFor(x => x.BibleBookId).InclusiveBetween(1, 39);
        RuleFor(x => x.Chapter).GreaterThan(0);
        RuleFor(x => x.Units).NotNull().Must(u => u.Count <= 500).WithMessage("Too many units.");
        RuleForEach(x => x.Units).ChildRules(u =>
        {
            u.RuleFor(i => i.LxxVerseId).GreaterThan(0);
            u.RuleFor(i => i.ProposalId).GreaterThan(0);
        });
    }
}
