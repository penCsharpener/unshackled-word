namespace UnshackledWord.Domain.Models.DataQuality;

public readonly record struct MorphologyIssue(string Field, string Kind);

public sealed record MorphologyValues(
    string? Pos, string? VerbForm, string? Degree, string? Tense, string? Person, string? Number, string? Mood, string? Case, string? Gender);

/// <summary>
/// L5: morphology fields that do not apply to the part of speech, codes outside the value maps, and fields that are
/// required but missing (noun number/case/gender, finite verb tense/person/number/mood, verb form).
/// Applicability comes from <see cref="Grammar.ElbLemmaValueMaps"/>.
/// </summary>
public static class MorphologyIssues
{
    public const string NotApplicable = "notApplicable";
    public const string Invalid = "invalid";
    public const string Missing = "missing";

    public static IReadOnlyList<MorphologyIssue> Evaluate(MorphologyValues v)
    {
        var rule = Grammar.ElbLemmaValueMaps.Rules.FirstOrDefault(r => r.PartOfSpeech == v.Pos);
        if (rule is null) return [];

        var applicable = new HashSet<string>(rule.Fields);
        var required = new HashSet<string>();
        if (rule.ByVerbForm is not null)
        {
            var form = rule.ByVerbForm.FirstOrDefault(f => f.VerbForm == v.VerbForm);
            if (form is not null) applicable.UnionWith(form.Fields);
            required.Add(Grammar.ElbLemmaValueMaps.VerbForm);
            if (v.VerbForm == "Fin") required.UnionWith(form!.Fields);
        }
        else if (v.Pos is "NOUN")
        {
            required.UnionWith([Grammar.ElbLemmaValueMaps.Number, Grammar.ElbLemmaValueMaps.Case, Grammar.ElbLemmaValueMaps.Gender]);
        }

        var issues = new List<MorphologyIssue>();
        foreach (var (field, value) in Fields(v))
        {
            if (string.IsNullOrEmpty(value))
            {
                if (required.Contains(field)) issues.Add(new MorphologyIssue(field, Missing));
            }
            else if (!applicable.Contains(field)) issues.Add(new MorphologyIssue(field, NotApplicable));
            else if (!Grammar.ElbLemmaValueMaps.IsValid(field, value)) issues.Add(new MorphologyIssue(field, Invalid));
        }

        return issues;
    }

    private static IEnumerable<(string Field, string? Value)> Fields(MorphologyValues v)
    {
        yield return (Grammar.ElbLemmaValueMaps.VerbForm, v.VerbForm);
        yield return (Grammar.ElbLemmaValueMaps.Degree, v.Degree);
        yield return (Grammar.ElbLemmaValueMaps.Tense, v.Tense);
        yield return (Grammar.ElbLemmaValueMaps.Person, v.Person);
        yield return (Grammar.ElbLemmaValueMaps.Number, v.Number);
        yield return (Grammar.ElbLemmaValueMaps.Mood, v.Mood);
        yield return (Grammar.ElbLemmaValueMaps.Case, v.Case);
        yield return (Grammar.ElbLemmaValueMaps.Gender, v.Gender);
    }
}
