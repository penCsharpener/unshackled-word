using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentReview.EditMerge;

[RequiresLogin]
public sealed class Endpoint : Endpoint<EditMergeAgentProposalRequest, MergeAgentProposalResponse>
{
    private readonly IAgentReviewService _service;

    public Endpoint(IAgentReviewService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("agent-proposals/{id}/edit-merge");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Merges the maintainer's edited mapping (links) and lemmas instead of the proposal (status edited-merged). The caller is author and reviewer of what is written, because a human changed the result. Same hard rules and status codes as merge.");
    }

    public override async Task HandleAsync(EditMergeAgentProposalRequest req, CancellationToken ct)
    {
        var reviewer = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _service.EditMergeAsync(req, reviewer, ct);
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

public sealed class Validator : Validator<EditMergeAgentProposalRequest>
{
    public const int MaxLinks = 2000;
    public const int MaxLemmas = 500;

    public Validator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Links).NotNull().Must(l => l.Count <= MaxLinks).WithMessage($"At most {MaxLinks} links.");
        RuleFor(x => x.Lemmas).NotNull().Must(l => l.Count <= MaxLemmas).WithMessage($"At most {MaxLemmas} lemma edits.");
        RuleFor(x => x.Lemmas).Must(l => l.Select(e => e.ElbWordId).Distinct().Count() == l.Count).WithMessage("Duplicate elbWordId in lemmas.");
    }
}
