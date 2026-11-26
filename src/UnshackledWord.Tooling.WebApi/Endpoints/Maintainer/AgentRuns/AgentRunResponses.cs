using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.AgentMapping;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.AgentRuns;

public static class AgentRunResponses
{
    /// <summary>400 for bad input, 404 for an unknown run or an empty range, 409 for a run that is in the wrong state.</summary>
    public static int StatusOf(AgentRunPrepareOutcome outcome) => outcome switch
    {
        AgentRunPrepareOutcome.NotFound => StatusCodes.Status404NotFound,
        AgentRunPrepareOutcome.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest,
    };
}

public sealed class AgentRunRangeValidator : AbstractValidator<AgentRunRangeRequest>
{
    public AgentRunRangeValidator()
    {
        RuleFor(x => x.Book).InclusiveBetween(1, 66);
        RuleFor(x => x.FromChapter).GreaterThan(0);
        RuleFor(x => x.FromVerse).GreaterThan(0);
        RuleFor(x => x.ToChapter).GreaterThanOrEqualTo(x => x.FromChapter).When(x => x.ToChapter is not null);
        RuleFor(x => x.ToVerse).GreaterThan(0).When(x => x.ToVerse is not null);
        RuleFor(x => x.Profile).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Model).MaximumLength(200);
        RuleFor(x => x.Mode).Must(m => AgentModes.TryParse(m, out _)).WithMessage(AgentModes.ValidModesMessage);
    }
}
