using FluentValidation;
using UnshackledWord.Domain.Models.Grammar;
using UnshackledWord.Domain.WebApi.Lemmas;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Lemmas;

/// <summary>Shared rules for the editable lemma/morphology fields; codes must exist in <see cref="ElbLemmaValueMaps"/>.</summary>
public sealed class LemmaEditFieldsValidator : AbstractValidator<LemmaEditFields>
{
    public const int MaxLemmaLength = 255; // Elb1871Morphology."Lemma" is varchar(255)

    public LemmaEditFieldsValidator()
    {
        RuleFor(x => x.Lemma).NotEmpty().Must(l => l.Trim().Length > 0).MaximumLength(MaxLemmaLength);
        RuleFor(x => x.PartOfSpeech).NotEmpty().Must(c => ElbLemmaValueMaps.IsValid(ElbLemmaValueMaps.PartOfSpeech, c)).WithMessage("Unknown part of speech code.");
        OptionalCode(x => x.Stts, ElbLemmaValueMaps.Stts);
        OptionalCode(x => x.Degree, ElbLemmaValueMaps.Degree);
        OptionalCode(x => x.VerbForm, ElbLemmaValueMaps.VerbForm);
        OptionalCode(x => x.Tense, ElbLemmaValueMaps.Tense);
        OptionalCode(x => x.Person, ElbLemmaValueMaps.Person);
        OptionalCode(x => x.Number, ElbLemmaValueMaps.Number);
        OptionalCode(x => x.Mood, ElbLemmaValueMaps.Mood);
        OptionalCode(x => x.Case, ElbLemmaValueMaps.Case);
        OptionalCode(x => x.Gender, ElbLemmaValueMaps.Gender);
    }

    private void OptionalCode(System.Linq.Expressions.Expression<Func<LemmaEditFields, string?>> property, string field)
    {
        RuleFor(property)
            .Must(c => ElbLemmaValueMaps.IsValid(field, c))
            .When(x => property.Compile()(x) is not null)
            .WithMessage($"Unknown {field} code.");
    }
}
