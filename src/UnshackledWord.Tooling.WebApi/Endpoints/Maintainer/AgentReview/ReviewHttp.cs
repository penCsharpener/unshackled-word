using FluentValidation;
using UnshackledWord.Application.Features.AgentMapping;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentReview;

public static class ReviewHttp
{
    /// <summary>404 unknown proposal, 409 not open or "veraltet", 400 a hard rule fails, 403 mapping editing switched off.</summary>
    public static int StatusOf(AgentReviewOutcome outcome) => outcome switch
    {
        AgentReviewOutcome.NotFound => StatusCodes.Status404NotFound,
        AgentReviewOutcome.NotOpen => StatusCodes.Status409Conflict,
        AgentReviewOutcome.Stale => StatusCodes.Status409Conflict,
        AgentReviewOutcome.Invalid => StatusCodes.Status400BadRequest,
        AgentReviewOutcome.Forbidden => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status200OK,
    };
}

/// <summary>One validator for every endpoint whose request is just the proposal id (FastEndpoints allows one validator per request type).</summary>
public sealed class AgentProposalIdValidator : FastEndpoints.Validator<UnshackledWord.Domain.WebApi.Maintainer.AgentProposalIdRequest>
{
    public AgentProposalIdValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}
