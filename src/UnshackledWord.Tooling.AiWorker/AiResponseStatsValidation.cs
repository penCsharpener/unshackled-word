using FastEndpoints;
using FluentValidation;
using UnshackledWord.Domain.Extensions;

namespace UnshackledWord.Tooling.AiWorker;

public sealed class AiResponseStatsValidation : Validator<AiResponseStats>
{
    public AiResponseStatsValidation()
    {
        RuleFor(x => x.FaultyStepIds).Must(x => x.Length == 0).WithMessage((model, ints) => $"AI assigned StepIds which don't exist in the source data: {ints.JoinStrings(",")} {model.VerseRange}");
        RuleFor(x => x.FaultyElbWordIds).Must(x => x.Length == 0).WithMessage((model, ints) => $"AI assigned ElbWordIds which don't exist in the source data: {ints.JoinStrings(",")} {model.VerseRange}");
        RuleFor(x => x.FaultyHebRefIds).Must(x => x.Length == 0).WithMessage((model, ints) => $"AI produced wrong HebRefIds: {ints.JoinStrings(",")} {model.VerseRange}");
        RuleFor(x => x.OverassignedRows).Must(x => x.Length < 20).WithMessage((model, ints) => $"AI produced overassigned rows for ElbWordIds: {ints.JoinStrings(",")}. {model.VerseRange}");
        RuleFor(x => x.WronglyAssignedGermanWordParts).Must(x => x.Count == 0).WithMessage((model, ints) => $"AI wrongly split German words: {ints.JoinStrings(",")}. {model.VerseRange}");
    }
}