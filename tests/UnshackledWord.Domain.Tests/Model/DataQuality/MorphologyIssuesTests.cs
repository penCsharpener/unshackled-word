using UnshackledWord.Domain.Models.DataQuality;

namespace UnshackledWord.Domain.Tests.Model.DataQuality;

public class MorphologyIssuesTests
{
    private static MorphologyValues Values(string? pos, string? verbForm = null, string? degree = null, string? tense = null, string? person = null,
        string? number = null, string? mood = null, string? @case = null, string? gender = null) =>
        new(pos, verbForm, degree, tense, person, number, mood, @case, gender);

    [Fact]
    public void Complete_noun_has_no_issue() =>
        MorphologyIssues.Evaluate(Values("NOUN", number: "S", @case: "Nom", gender: "M")).ShouldBeEmpty();

    [Fact]
    public void Noun_without_gender_is_missing_gender() =>
        MorphologyIssues.Evaluate(Values("NOUN", number: "S", @case: "Nom")).ShouldBe([new MorphologyIssue("gender", MorphologyIssues.Missing)]);

    [Fact]
    public void Tense_on_a_noun_does_not_apply() =>
        MorphologyIssues.Evaluate(Values("NOUN", tense: "Past", number: "S", @case: "Nom", gender: "M"))
            .ShouldBe([new MorphologyIssue("tense", MorphologyIssues.NotApplicable)]);

    [Fact]
    public void Adposition_with_case_does_not_apply() =>
        MorphologyIssues.Evaluate(Values("ADP", @case: "Dat")).ShouldBe([new MorphologyIssue("case", MorphologyIssues.NotApplicable)]);

    [Fact]
    public void Finite_verb_with_complete_fields_is_clean() =>
        MorphologyIssues.Evaluate(Values("VERB", verbForm: "Fin", tense: "Past", person: "3", number: "S", mood: "Ind")).ShouldBeEmpty();

    [Fact]
    public void Infinitive_with_case_does_not_apply() =>
        MorphologyIssues.Evaluate(Values("VERB", verbForm: "Inf", @case: "Nom")).ShouldBe([new MorphologyIssue("case", MorphologyIssues.NotApplicable)]);

    [Fact]
    public void Verb_without_verb_form_is_missing_it() =>
        MorphologyIssues.Evaluate(Values("VERB")).ShouldBe([new MorphologyIssue("verbForm", MorphologyIssues.Missing)]);

    [Fact]
    public void Unknown_code_is_invalid() =>
        MorphologyIssues.Evaluate(Values("NOUN", number: "X", @case: "Nom", gender: "M")).ShouldBe([new MorphologyIssue("number", MorphologyIssues.Invalid)]);

    [Fact]
    public void Unknown_pos_is_not_judged() =>
        MorphologyIssues.Evaluate(Values("SYM", @case: "Nom")).ShouldBeEmpty();
}
