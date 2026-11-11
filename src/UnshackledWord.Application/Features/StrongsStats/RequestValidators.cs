using FluentValidation;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetStrongsOccurrences;
using UnshackledWord.Domain.WebApi.Elberfelder.GetStrongsSummary;
using UnshackledWord.Domain.WebApi.Elberfelder.GetWordStats;

namespace UnshackledWord.Application.Features.StrongsStats;

// The rules live here (not in the endpoint classes) so they can be unit-tested; the endpoints include them.

public sealed class GetStrongsSummaryRequestValidator : AbstractValidator<GetStrongsSummaryRequest>
{
    public GetStrongsSummaryRequestValidator()
    {
        RuleFor(x => x.Strongs)
            .Must(s => StrongsNumber.TryNormalize(s, out _, out _))
            .WithMessage("Strong's number must look like 'G26' or 'H1254A'.");
    }
}

public sealed class GetWordStatsRequestValidator : AbstractValidator<GetWordStatsRequest>
{
    public GetWordStatsRequestValidator()
    {
        RuleFor(x => x.Word)
            .NotEmpty()
            .MaximumLength(GetWordStatsRequest.MaxWordLength)
            .Matches(@"^[\p{L}'-]+$")
            .WithMessage("Word must be a single German word.");
        RuleFor(x => x.Testament)
            .Must(t => WordStatsService.ResolveTestament(t) is not null)
            .WithMessage("Testament must be 'all', 'ot' or 'nt'.");
    }
}

public sealed class GetStrongsOccurrencesRequestValidator : AbstractValidator<GetStrongsOccurrencesRequest>
{
    public GetStrongsOccurrencesRequestValidator()
    {
        RuleFor(x => x.Strongs)
            .Must(s => StrongsNumber.TryNormalize(s, out _, out _))
            .WithMessage("Strong's number must look like 'G26' or 'H1254A'.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, GetStrongsOccurrencesRequest.MaxPageSize);
        RuleFor(x => x.Book).InclusiveBetween(1, 66).When(x => x.Book is not null);
        RuleFor(x => x.Rendering).Must(r => r is { Length: >= 1 and <= 50 }).When(x => x.Rendering is not null)
            .WithMessage("Rendering must be 1 to 50 characters.");
    }
}
