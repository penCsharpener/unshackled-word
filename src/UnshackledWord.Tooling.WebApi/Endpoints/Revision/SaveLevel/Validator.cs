using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.WebApi.Revision;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Revision.SaveLevel;

/// <summary>Shape rules of a preset; uniqueness, the default rule and "level in use" need the database and live in <see cref="RevisionLevelService"/>.</summary>
public sealed class Validator : Validator<SaveRevisionLevelRequest>
{
    public Validator()
    {
        RuleFor(x => x.Level).InclusiveBetween(1, 1000);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30).Matches("^[a-z0-9][a-z0-9-]*$").WithMessage("code may only contain a-z, 0-9 and '-'.");
        RuleFor(x => x.NameDe).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DescriptionDe).MaximumLength(2000);
        RuleFor(x => x.DescriptionEn).MaximumLength(2000);
    }
}
