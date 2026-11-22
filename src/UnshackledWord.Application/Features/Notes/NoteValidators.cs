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
            .WithMessage("'{PropertyValue}' is not a note kind (chapter, verses, verse, words).")
            .Must(kind => NoteKinds.Supported.Contains(kind))
            .When(x => NoteKinds.All.Contains(x.Kind), ApplyConditionTo.CurrentValidator)
            .WithMessage("Notes of kind '{PropertyValue}' are not supported yet; use chapter or verse.");
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

        When(x => x.Kind is NoteKinds.Chapter or NoteKinds.Verse, () =>
        {
            RuleFor(x => x.StartPosition).Null().WithMessage("Word positions belong to words notes only.");
            RuleFor(x => x.EndPosition).Null().WithMessage("Word positions belong to words notes only.");
        });
    }
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
