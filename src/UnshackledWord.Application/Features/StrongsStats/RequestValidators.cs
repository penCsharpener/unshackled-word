using FluentValidation;
using UnshackledWord.Domain.Models.Study;
using UnshackledWord.Domain.WebApi.Elberfelder.GetStrongsOccurrences;
using UnshackledWord.Domain.WebApi.Elberfelder.GetStrongsSummary;
using UnshackledWord.Domain.WebApi.Elberfelder.GetWordStats;
using UnshackledWord.Domain.WebApi.Elberfelder.SearchOriginals;

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
        RuleFor(x => x.Pos)
            .MaximumLength(GetWordStatsRequest.MaxPosLength)
            .Matches(@"^[A-Za-z_]+$")
            .When(x => !string.IsNullOrEmpty(x.Pos));
    }
}

public sealed class SearchOriginalsRequestValidator : AbstractValidator<SearchOriginalsRequest>
{
    public SearchOriginalsRequestValidator()
    {
        RuleFor(x => x.Q)
            .NotEmpty()
            .MaximumLength(SearchOriginalsRequest.MaxQueryLength);
        RuleFor(x => SearchTokenizer.Tokenize(x.Q))
            .Must(t => t.Count >= 1)
            .WithName(nameof(SearchOriginalsRequest.Q))
            .WithMessage("Query must contain at least one word.")
            .Must(t => t.Count <= SearchOriginalsRequest.MaxTokens)
            .WithMessage($"Query may contain at most {SearchOriginalsRequest.MaxTokens} different words.")
            .Must(t => t.All(w => w.Length <= SearchOriginalsRequest.MaxTokenLength))
            .WithMessage($"A word may have at most {SearchOriginalsRequest.MaxTokenLength} letters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Q) && x.Q.Length <= SearchOriginalsRequest.MaxQueryLength);
        RuleFor(x => x.Top).InclusiveBetween(1, SearchOriginalsRequest.MaxTop);
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
