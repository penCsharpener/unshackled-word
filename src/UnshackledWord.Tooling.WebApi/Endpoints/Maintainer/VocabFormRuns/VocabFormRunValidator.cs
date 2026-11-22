using FluentValidation;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabFormRuns;

public sealed class VocabFormRunRangeValidator : AbstractValidator<VocabFormRunRangeRequest>
{
    public VocabFormRunRangeValidator()
    {
        RuleFor(x => x.Course).NotEmpty().MaximumLength(30);
        RuleFor(x => x.FromRank).GreaterThanOrEqualTo(1);
        RuleFor(x => x.ToRank).GreaterThanOrEqualTo(x => x.FromRank).When(x => x.ToRank is not null);
        RuleFor(x => x.Limit).InclusiveBetween(1, 2000);
        RuleFor(x => x.Profile).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Model).MaximumLength(200);
    }
}
