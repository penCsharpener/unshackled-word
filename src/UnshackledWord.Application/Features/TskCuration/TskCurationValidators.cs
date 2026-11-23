using FluentValidation;
using UnshackledWord.Domain.WebApi.Tsk;

namespace UnshackledWord.Application.Features.TskCuration;

/// <summary>Shape of <see cref="SaveTskCurationRequest"/> (roadmap 0024.3). Whether the link exists in TSK is checked by <see cref="TskCurationService"/> (404).</summary>
public sealed class SaveTskCurationRequestValidator : AbstractValidator<SaveTskCurationRequest>
{
    public const int MaxCommentLength = 500;

    public SaveTskCurationRequestValidator()
    {
        RuleFor(x => x.SourceHebRefId).Must(IsVerse).WithMessage("sourceHebRefId must be a verse (book * 1,000,000 + chapter * 1,000 + verse).");
        RuleFor(x => x.TargetStartHebRefId).Must(IsVerse).WithMessage("targetStartHebRefId must be a verse (book * 1,000,000 + chapter * 1,000 + verse).");
        RuleFor(x => x.TargetEndHebRefId)
            .Must((req, end) => end is not { } last || (last > req.TargetStartHebRefId && last / 1000 == req.TargetStartHebRefId / 1000 && IsVerse(last)))
            .WithMessage("targetEndHebRefId must be a later verse of the target's chapter (omit it for a single verse).");
        RuleFor(x => x.Rating)
            .Must(rating => string.IsNullOrWhiteSpace(rating) || TskRatings.All.Contains(rating.Trim()))
            .WithMessage("rating must be 'highlight', 'hide' or null.");
        RuleFor(x => x.Comment)
            .Must(comment => TskCurationService.NormaliseComment(comment) is not { Length: > MaxCommentLength })
            .WithMessage($"comment must have at most {MaxCommentLength} characters.");
    }

    private static bool IsVerse(int hebRefId)
    {
        var (book, chapter, verse) = (hebRefId / 1_000_000, hebRefId / 1_000 % 1_000, hebRefId % 1_000);
        return book is >= 1 and <= 66 && chapter >= 1 && verse >= 1;
    }
}
