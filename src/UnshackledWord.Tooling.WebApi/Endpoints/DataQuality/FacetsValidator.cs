using FluentValidation;
using UnshackledWord.Domain.WebApi.DataQuality;

namespace UnshackledWord.Tooling.WebApi.Endpoints.DataQuality;

/// <summary>Testament and book facets shared by every data quality read.</summary>
public sealed class FacetsValidator : AbstractValidator<DqFacetsRequest>
{
    public FacetsValidator()
    {
        RuleFor(x => x.Testament).Must(t => t is null or "ot" or "nt").WithMessage("testament must be ot or nt.");
        RuleFor(x => x.BookId).InclusiveBetween(1, 66).When(x => x.BookId is not null);
    }
}
