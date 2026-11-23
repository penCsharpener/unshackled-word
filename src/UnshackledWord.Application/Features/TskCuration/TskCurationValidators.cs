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
            .Must((req, end) => IsRangeEnd(req.TargetStartHebRefId, end))
            .WithMessage("targetEndHebRefId must be a later verse of the target's chapter (omit it for a single verse).");
        RuleFor(x => x.Rating)
            .Must(rating => string.IsNullOrWhiteSpace(rating) || TskRatings.All.Contains(rating.Trim()))
            .WithMessage("rating must be 'highlight', 'hide' or null.");
        RuleFor(x => x.Comment)
            .Must(comment => TskCurationService.NormaliseComment(comment) is not { Length: > MaxCommentLength })
            .WithMessage($"comment must have at most {MaxCommentLength} characters.");
    }

    public static bool IsVerse(int hebRefId)
    {
        var (book, chapter, verse) = (hebRefId / 1_000_000, hebRefId / 1_000 % 1_000, hebRefId % 1_000);
        return book is >= 1 and <= 66 && chapter >= 1 && verse >= 1;
    }

    /// <summary>Null (one verse) or a later verse of the start's chapter.</summary>
    public static bool IsRangeEnd(int start, int? end) => end is not { } last || (last > start && last / 1000 == start / 1000 && IsVerse(last));
}

/// <summary>
/// Shape of <see cref="AddTskCurationRequest"/> (roadmap 0024.4): a source verse id, a target that parses to one verse or a range within one chapter
/// (<see cref="TskAddedTarget"/>), a comment of at most 500 characters. Source existence and duplicates are checked by <see cref="TskCurationService"/>.
/// </summary>
public sealed class AddTskCurationRequestValidator : AbstractValidator<AddTskCurationRequest>
{
    public const int MaxReferenceLength = 100;

    public AddTskCurationRequestValidator()
    {
        RuleFor(x => x.SourceHebRefId).Must(SaveTskCurationRequestValidator.IsVerse)
            .WithMessage("sourceHebRefId must be a verse (book * 1,000,000 + chapter * 1,000 + verse).");
        RuleFor(x => x.Reference)
            .Cascade(CascadeMode.Stop)
            .Must(reference => reference is not { Length: > MaxReferenceLength })
            .WithMessage($"reference must have at most {MaxReferenceLength} characters.")
            .Must(reference => TskAddedTarget.Parse(reference).Error is null)
            .WithMessage((_, reference) => TskAddedTarget.Parse(reference).Error!);
        RuleFor(x => x.Comment)
            .Must(comment => TskCurationService.NormaliseComment(comment) is not { Length: > SaveTskCurationRequestValidator.MaxCommentLength })
            .WithMessage($"comment must have at most {SaveTskCurationRequestValidator.MaxCommentLength} characters.");
    }
}

/// <summary>Shape of <see cref="DeleteTskCurationRequest"/> (roadmap 0024.4): the same key rules as <see cref="SaveTskCurationRequestValidator"/>.</summary>
public sealed class DeleteTskCurationRequestValidator : AbstractValidator<DeleteTskCurationRequest>
{
    public DeleteTskCurationRequestValidator()
    {
        RuleFor(x => x.SourceHebRefId).Must(SaveTskCurationRequestValidator.IsVerse)
            .WithMessage("sourceHebRefId must be a verse (book * 1,000,000 + chapter * 1,000 + verse).");
        RuleFor(x => x.TargetStartHebRefId).Must(SaveTskCurationRequestValidator.IsVerse)
            .WithMessage("targetStartHebRefId must be a verse (book * 1,000,000 + chapter * 1,000 + verse).");
        RuleFor(x => x.TargetEndHebRefId)
            .Must((req, end) => SaveTskCurationRequestValidator.IsRangeEnd(req.TargetStartHebRefId, end))
            .WithMessage("targetEndHebRefId must be a later verse of the target's chapter (omit it for a single verse).");
    }
}

/// <summary>
/// Shape of the word-link keys (roadmap 0024.5): a source verse and 1–100 distinct target keys (same rules as <see cref="SaveTskCurationRequestValidator"/>).
/// Whether each key is a link of the source is checked by <see cref="TskCurationService"/> (404).
/// </summary>
public static class TskWordLinkKeyRules
{
    public const int MaxKeys = 100;

    public const string SourceMessage = "sourceHebRefId must be a verse (book * 1,000,000 + chapter * 1,000 + verse).";
    public const string CountMessage = "keys must list 1 to 100 links.";
    public const string KeyMessage = "Every key needs a targetStartHebRefId verse and, for a range, a later targetEndHebRefId of the same chapter (omit it for a single verse).";
    public const string DuplicateMessage = "keys must not list a link twice.";

    public static bool CountOk(List<TskLinkTargetKey>? keys) => keys is { Count: >= 1 and <= MaxKeys };

    public static bool KeysOk(List<TskLinkTargetKey>? keys) =>
        keys is not null && keys.All(key => key is not null && SaveTskCurationRequestValidator.IsVerse(key.TargetStartHebRefId)
                                            && SaveTskCurationRequestValidator.IsRangeEnd(key.TargetStartHebRefId, key.TargetEndHebRefId));

    public static bool Distinct(List<TskLinkTargetKey>? keys) =>
        keys is not null && keys.Select(key => (key.TargetStartHebRefId, key.TargetEndHebRefId)).Distinct().Count() == keys.Count;
}

/// <summary>Shape of <see cref="SaveTskWordLinkRequest"/> (roadmap 0024.5): keys as <see cref="TskWordLinkKeyRules"/>, <c>1 ≤ startPosition ≤ endPosition</c>. That the positions exist is checked by the service (400).</summary>
public sealed class SaveTskWordLinkRequestValidator : AbstractValidator<SaveTskWordLinkRequest>
{
    public SaveTskWordLinkRequestValidator()
    {
        RuleFor(x => x.SourceHebRefId).Must(SaveTskCurationRequestValidator.IsVerse).WithMessage(TskWordLinkKeyRules.SourceMessage);
        RuleFor(x => x.Keys)
            .Cascade(CascadeMode.Stop)
            .Must(TskWordLinkKeyRules.CountOk).WithMessage(TskWordLinkKeyRules.CountMessage)
            .Must(TskWordLinkKeyRules.KeysOk).WithMessage(TskWordLinkKeyRules.KeyMessage)
            .Must(TskWordLinkKeyRules.Distinct).WithMessage(TskWordLinkKeyRules.DuplicateMessage);
        RuleFor(x => x.StartPosition).GreaterThanOrEqualTo(1).WithMessage("startPosition must be at least 1.");
        RuleFor(x => x.EndPosition).GreaterThanOrEqualTo(x => x.StartPosition).WithMessage("endPosition must not be before startPosition.");
    }
}

/// <summary>Shape of <see cref="DeleteTskWordLinkRequest"/> (roadmap 0024.5).</summary>
public sealed class DeleteTskWordLinkRequestValidator : AbstractValidator<DeleteTskWordLinkRequest>
{
    public DeleteTskWordLinkRequestValidator()
    {
        RuleFor(x => x.SourceHebRefId).Must(SaveTskCurationRequestValidator.IsVerse).WithMessage(TskWordLinkKeyRules.SourceMessage);
        RuleFor(x => x.Keys)
            .Cascade(CascadeMode.Stop)
            .Must(TskWordLinkKeyRules.CountOk).WithMessage(TskWordLinkKeyRules.CountMessage)
            .Must(TskWordLinkKeyRules.KeysOk).WithMessage(TskWordLinkKeyRules.KeyMessage)
            .Must(TskWordLinkKeyRules.Distinct).WithMessage(TskWordLinkKeyRules.DuplicateMessage);
    }
}
