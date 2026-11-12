using FluentValidation;
using UnshackledWord.Domain.Models.Grammar;
using UnshackledWord.Domain.WebApi.Lemmas;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Lemmas;

/// <summary>Source lemmas: 1-20 entries, each 1-255 characters after trimming, no control characters.</summary>
public static class LemmaRules
{
    public static IRuleBuilderOptions<T, string[]> ValidFromLemmas<T>(this IRuleBuilder<T, string[]> rule) =>
        rule.NotEmpty()
            .Must(f => f.Length <= LemmaBulkRules.MaxFromLemmas).WithMessage($"At most {LemmaBulkRules.MaxFromLemmas} source lemmas.")
            .Must(f => f.All(LemmaBulkRules.IsValidLemma)).WithMessage("Each lemma must be 1-255 characters (trimmed) without control characters.");
}

public sealed class BulkFilterValidator : AbstractValidator<BulkFilterRequest>
{
    public BulkFilterValidator()
    {
        RuleFor(x => x.From).ValidFromLemmas();
        RuleFor(x => x.Testament).Must(t => t is LemmaBulkRules.TestamentOld or LemmaBulkRules.TestamentNew).When(x => x.Testament is not null)
            .WithMessage("testament must be 'ot' or 'nt'.");
        RuleFor(x => x.BookId).InclusiveBetween(1, 66).When(x => x.BookId is not null);
        RuleFor(x => x.Testament).Null().When(x => x.BookId is not null).WithMessage("testament and bookId are mutually exclusive.");
        RuleFor(x => x.Chapter).GreaterThan(0).When(x => x.Chapter is not null);
        RuleFor(x => x.BookId).NotNull().When(x => x.Chapter is not null).WithMessage("chapter needs bookId.");
        RuleFor(x => x.PartOfSpeech).Must(c => ElbLemmaValueMaps.IsValid(ElbLemmaValueMaps.PartOfSpeech, c)).When(x => x.PartOfSpeech is not null)
            .WithMessage("Unknown part of speech code.");
        RuleFor(x => x.Original)
            .Must(o => o.Length <= OriginalLanguageRules.MaxOriginalFilters).WithMessage($"At most {OriginalLanguageRules.MaxOriginalFilters} original keys.")
            .Must(o => o.All(OriginalLanguageRules.IsValidOriginalKey)).WithMessage("original must be added, none or a G/H Strong's key like G4680.");
        RuleFor(x => x.PlainWord).MaximumLength(LemmaBulkRules.MaxLemmaLength);
    }
}
