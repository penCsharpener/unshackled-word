using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.Revision;
using UnshackledWord.Domain.WebApi.Revision;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Revision.SaveVerse;

/// <summary>Shape rules only; "the level exists" and "the verse exists" need the database and live in <see cref="RevisionService"/>.</summary>
public sealed class Validator : Validator<SaveRevisionVerseRequest>
{
    public const int MaxTextLength = 2000;
    public const int MaxTokens = 300;
    public const int MaxNoteLength = 500;

    public Validator()
    {
        RuleFor(x => x.BibleBookId).InclusiveBetween(1, 66);
        RuleFor(x => x.ChapterId).InclusiveBetween(1, 999);
        RuleFor(x => x.VerseId).InclusiveBetween(0, 999);
        RuleFor(x => x.Level).InclusiveBetween(1, 1000);
        RuleFor(x => x.Text).NotEmpty()
            .Must(t => t.Trim().Length <= MaxTextLength).WithMessage($"text must be 1-{MaxTextLength} characters after trimming.")
            .Must(t => RevisionTokenizer.Tokenize(t).Count <= MaxTokens).WithMessage($"text must have at most {MaxTokens} words.");
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(1).When(x => x.ExpectedVersion.HasValue);
        RuleFor(x => x.Note).MaximumLength(MaxNoteLength);
    }
}
