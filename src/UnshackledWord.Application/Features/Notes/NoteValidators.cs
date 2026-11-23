using FluentValidation;
using UnshackledWord.Domain.WebApi.Notes;

namespace UnshackledWord.Application.Features.Notes;

/// <summary>Shape of an anchor (roadmap 0023): which fields each kind fills, refs inside the given book and chapter. Whether the verse exists is checked by <see cref="NoteService"/>.</summary>
public sealed class NoteAnchorValidator : AbstractValidator<NoteAnchorDto>
{
    public const int MaxBookId = 66;
    public const int MaxChapter = 150;

    public NoteAnchorValidator()
    {
        RuleFor(x => x.Kind)
            .Must(kind => NoteKinds.All.Contains(kind))
            .WithMessage("'{PropertyValue}' is not a note kind (chapter, verses, verse, words).");
        RuleFor(x => x.BookId).InclusiveBetween(1, MaxBookId);
        RuleFor(x => x.Chapter).InclusiveBetween(1, MaxChapter);

        When(x => x.Kind == NoteKinds.Chapter, () =>
        {
            RuleFor(x => x.StartHebRefId).Null().WithMessage("A chapter note has no verse.");
            RuleFor(x => x.EndHebRefId).Null().WithMessage("A chapter note has no verse.");
        });

        When(x => x.Kind == NoteKinds.Verse, () =>
        {
            RuleFor(x => x.StartHebRefId).NotNull().WithMessage("A verse note needs startHebRefId.");
            RuleFor(x => x.StartHebRefId!.Value)
                .Must((anchor, start) => HebRef.Book(start) == anchor.BookId && HebRef.Chapter(start) == anchor.Chapter && HebRef.Verse(start) >= 1)
                .When(x => x.StartHebRefId is not null)
                .WithName("StartHebRefId")
                .WithMessage("startHebRefId must be a verse of the given book and chapter.");
            RuleFor(x => x.EndHebRefId)
                .Must((anchor, end) => end is null || end == anchor.StartHebRefId)
                .WithMessage("A verse note ends where it starts (omit endHebRefId or repeat startHebRefId).");
        });

        When(x => x.Kind == NoteKinds.Verses, () =>
        {
            RuleFor(x => x.StartHebRefId).NotNull().WithMessage("A verses note needs startHebRefId.");
            RuleFor(x => x.EndHebRefId).NotNull().WithMessage("A verses note needs endHebRefId.");
            RuleFor(x => x.StartHebRefId!.Value)
                .Must((anchor, start) => InChapter(anchor, start))
                .When(x => x.StartHebRefId is not null)
                .WithName("StartHebRefId")
                .WithMessage("startHebRefId must be a verse of the given book and chapter.");
            RuleFor(x => x.EndHebRefId!.Value)
                .Must((anchor, end) => InChapter(anchor, end))
                .When(x => x.EndHebRefId is not null)
                .WithName("EndHebRefId")
                .WithMessage("endHebRefId must be a verse of the given book and chapter (a range never crosses a chapter).");
            RuleFor(x => x.EndHebRefId!.Value)
                .Must((anchor, end) => anchor.StartHebRefId is null || end > anchor.StartHebRefId)
                .When(x => x.EndHebRefId is not null)
                .WithName("EndHebRefId")
                .WithMessage("endHebRefId must be after startHebRefId (one verse is a verse note).");
        });

        When(x => x.Kind == NoteKinds.Words, () =>
        {
            RuleFor(x => x.StartHebRefId).NotNull().WithMessage("A words note needs startHebRefId.");
            RuleFor(x => x.StartHebRefId!.Value)
                .Must((anchor, start) => InChapter(anchor, start))
                .When(x => x.StartHebRefId is not null)
                .WithName("StartHebRefId")
                .WithMessage("startHebRefId must be a verse of the given book and chapter.");
            RuleFor(x => x.EndHebRefId)
                .Must((anchor, end) => end is null || end == anchor.StartHebRefId)
                .WithMessage("A words note stays inside one verse (omit endHebRefId or repeat startHebRefId).");
            RuleFor(x => x.StartPosition).NotNull().GreaterThanOrEqualTo(1).WithMessage("A words note needs startPosition >= 1.");
            RuleFor(x => x.EndPosition).NotNull().WithMessage("A words note needs endPosition.");
            RuleFor(x => x.EndPosition!.Value)
                .GreaterThanOrEqualTo(x => x.StartPosition ?? 1)
                .When(x => x.EndPosition is not null)
                .WithName("EndPosition")
                .WithMessage("endPosition must not be before startPosition.");
        });

        When(x => x.Kind is NoteKinds.Chapter or NoteKinds.Verse or NoteKinds.Verses, () =>
        {
            RuleFor(x => x.StartPosition).Null().WithMessage("Word positions belong to words notes only.");
            RuleFor(x => x.EndPosition).Null().WithMessage("Word positions belong to words notes only.");
        });
    }

    private static bool InChapter(NoteAnchorDto anchor, int hebRefId) =>
        HebRef.Book(hebRefId) == anchor.BookId && HebRef.Chapter(hebRefId) == anchor.Chapter && HebRef.Verse(hebRefId) >= 1;
}

public static class NoteBody
{
    public const int MaxLength = 20_000;
}

public sealed class CreateNoteRequestValidator : AbstractValidator<CreateNoteRequest>
{
    public CreateNoteRequestValidator()
    {
        RuleFor(x => x.Anchor).NotNull().SetValidator(new NoteAnchorValidator());
        RuleFor(x => x.BodyMarkdown).NotEmpty().MaximumLength(NoteBody.MaxLength);
    }
}

public sealed class UpdateNoteRequestValidator : AbstractValidator<UpdateNoteRequest>
{
    public UpdateNoteRequestValidator()
    {
        RuleFor(x => x.Anchor!).SetValidator(new NoteAnchorValidator()).When(x => x.Anchor is not null);
        RuleFor(x => x.BodyMarkdown).NotEmpty().MaximumLength(NoteBody.MaxLength);
    }
}

public sealed class ListMyNotesRequestValidator : AbstractValidator<ListMyNotesRequest>
{
    public const int MaxTake = 50;
    public const int MaxQueryLength = 200;

    public ListMyNotesRequestValidator()
    {
        RuleFor(x => x.Book!.Value).InclusiveBetween(1, NoteAnchorValidator.MaxBookId).When(x => x.Book is not null).WithName("Book");
        RuleFor(x => x.Visibility)
            .Must(v => v is NoteVisibilities.Private or NoteVisibilities.Internal or NoteVisibilities.Public)
            .When(x => x.Visibility is not null)
            .WithMessage("'{PropertyValue}' is not a visibility (private, internal, public).");
        RuleFor(x => x.Q).MaximumLength(MaxQueryLength);
        RuleFor(x => x.Skip).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Take).InclusiveBetween(1, MaxTake);
    }
}

public sealed class SetNoteVisibilityRequestValidator : AbstractValidator<SetNoteVisibilityRequest>
{
    public SetNoteVisibilityRequestValidator()
    {
        RuleFor(x => x.Visibility)
            .Must(v => v is NoteVisibilities.Private or NoteVisibilities.Internal or NoteVisibilities.Public)
            .WithMessage("'{PropertyValue}' is not a visibility (private, internal, public).");
    }
}

public sealed class NoteReviewActionRequestValidator : AbstractValidator<NoteReviewActionRequest>
{
    public const int MaxReasonLength = 500;

    public NoteReviewActionRequestValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(MaxReasonLength);
    }
}
