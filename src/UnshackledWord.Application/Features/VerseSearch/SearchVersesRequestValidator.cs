using FluentValidation;
using UnshackledWord.Domain.WebApi.Elberfelder.SearchVerses;

namespace UnshackledWord.Application.Features.VerseSearch;

// The rules live here (not in the endpoint class) so they can be unit-tested; the endpoint includes them.
public sealed class SearchVersesRequestValidator : AbstractValidator<SearchVersesRequest>
{
    public SearchVersesRequestValidator()
    {
        RuleFor(x => x.Q)
            .Must(q => !string.IsNullOrWhiteSpace(q))
            .WithMessage("Query must not be empty.")
            .Must(q => q.Trim().Length <= SearchVersesRequest.MaxQueryLength)
            .WithMessage($"Query may have at most {SearchVersesRequest.MaxQueryLength} characters.");
        RuleFor(x => VerseSearchQueryParser.Parse(x.Q))
            .Must(q => q.HasPositive)
            .WithName(nameof(SearchVersesRequest.Q))
            .WithMessage("Query must contain at least one word that is not excluded.")
            .Must(q => q.WordCount <= SearchVersesRequest.MaxWords)
            .WithMessage($"Query may contain at most {SearchVersesRequest.MaxWords} words.")
            .Must(q => q.AllWords.All(w => w.Length <= SearchVersesRequest.MaxWordLength))
            .WithMessage($"A word may have at most {SearchVersesRequest.MaxWordLength} characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Q) && x.Q.Trim().Length <= SearchVersesRequest.MaxQueryLength);
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(SearchVersesRequest.MinPageSize, SearchVersesRequest.MaxPageSize);
        RuleFor(x => x.Level).InclusiveBetween(0, 1000);
        // An empty ?bookId= is bound as 0 by the request binder and means "no book filter".
        RuleFor(x => x.BookId).Must(id => id is null or 0 or (>= 1 and <= 66)).WithMessage("'Book Id' must be between 1 and 66.");
    }
}
