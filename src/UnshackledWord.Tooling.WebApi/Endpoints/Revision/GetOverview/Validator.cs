using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.WebApi.Revision;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Revision.GetOverview;

public sealed class Validator : Validator<RevisionOverviewRequest>
{
    public Validator()
    {
        RuleFor(x => x.List).Must(l => RevisionWorkListNames.All.Contains(l)).When(x => x.List is not null)
            .WithMessage($"list must be one of: {string.Join(", ", RevisionWorkListNames.All)}.");
        RuleFor(x => x.Level).InclusiveBetween(1, 1000).When(x => x.Level.HasValue);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, RevisionOverviewService.MaxPageSize);
    }
}
