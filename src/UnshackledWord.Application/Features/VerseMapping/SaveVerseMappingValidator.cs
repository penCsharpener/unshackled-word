using FluentValidation;
using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;

namespace UnshackledWord.Application.Features.VerseMapping;

/// <summary>A save request together with the ids that belong to the verse; checked by <see cref="SaveVerseMappingValidator"/>.</summary>
public sealed class VerseMappingSaveInput
{
    public required IReadOnlyList<VerseMappingLink> Links { get; init; }
    public required IReadOnlySet<int> GermanWordIds { get; init; }
    public required IReadOnlySet<int> OriginalWordIds { get; init; }
}

public sealed class SaveVerseMappingValidator : AbstractValidator<VerseMappingSaveInput>
{
    public SaveVerseMappingValidator()
    {
        RuleFor(x => x).Custom((input, context) =>
        {
            var seen = new HashSet<(int, int?)>();
            for (var i = 0; i < input.Links.Count; i++)
            {
                var link = input.Links[i];
                var label = $"Link {i + 1} (word {link.ElbWordId})";

                if (!input.GermanWordIds.Contains(link.ElbWordId))
                {
                    context.AddFailure($"{label}: elbWordId does not belong to this verse.");
                }

                if (link.StepWordId is { } stepId && !input.OriginalWordIds.Contains(stepId))
                {
                    context.AddFailure($"{label}: stepWordId {stepId} is not an original word of this verse.");
                }

                if (link.ParentElbWordId is { } parentId)
                {
                    if (!input.GermanWordIds.Contains(parentId))
                    {
                        context.AddFailure($"{label}: parentElbWordId {parentId} does not belong to this verse.");
                    }
                    else if (parentId == link.ElbWordId)
                    {
                        context.AddFailure($"{label}: a word cannot be its own parent.");
                    }
                }

                if (link.IsAddedWord)
                {
                    if (link.ParentElbWordId is null)
                    {
                        context.AddFailure($"{label}: an added word needs a parentElbWordId.");
                    }

                    if (link.StepWordId is not null)
                    {
                        context.AddFailure($"{label}: an added word must not have a stepWordId.");
                    }
                }
                else if (link.StepWordId is null)
                {
                    context.AddFailure($"{label}: a link needs a stepWordId unless it is an added word.");
                }
                else if (link.ParentElbWordId is not null)
                {
                    context.AddFailure($"{label}: only added words have a parentElbWordId.");
                }

                if (!seen.Add((link.ElbWordId, link.StepWordId)))
                {
                    context.AddFailure($"{label}: duplicate (elbWordId, stepWordId) pair.");
                }
            }
        });
    }
}
