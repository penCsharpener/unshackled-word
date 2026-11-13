namespace UnshackledWord.Application.Features.Seed.Morphology;

/// <summary>The eight feature columns of <c>Elb1871Morphology</c>; null = column is NULL.</summary>
public sealed record MorphologyFeatureValues(
    string? Degree,
    string? VerbForm,
    string? Tense,
    string? Person,
    string? Number,
    string? Mood,
    string? Case,
    string? Gender);

/// <summary>Conversion between the eight feature columns and the single <c>Feats</c> column of the seed.</summary>
public static class MorphologyFeatures
{
    /// <summary>The feature names in the order they are written: alphabetical, as in Universal Dependencies.</summary>
    public static readonly IReadOnlyList<string> KeyOrder = ["Case", "Degree", "Gender", "Mood", "Number", "Person", "Tense", "VerbForm"];

    /// <summary><c>Case=Nom|Gender=Masc|Number=Sing</c>; null and empty columns are left out; empty string when there is none.</summary>
    public static string Format(MorphologyFeatureValues values)
    {
        var parts = new List<string>(KeyOrder.Count);

        Add(parts, "Case", values.Case);
        Add(parts, "Degree", values.Degree);
        Add(parts, "Gender", values.Gender);
        Add(parts, "Mood", values.Mood);
        Add(parts, "Number", values.Number);
        Add(parts, "Person", values.Person);
        Add(parts, "Tense", values.Tense);
        Add(parts, "VerbForm", values.VerbForm);

        return string.Join('|', parts);
    }

    /// <summary>Inverse of <see cref="Format"/>; unknown keys are ignored (a newer seed may carry more features).</summary>
    public static MorphologyFeatureValues Parse(string? feats)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!string.IsNullOrEmpty(feats))
        {
            foreach (var part in feats.Split('|', StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = part.IndexOf('=');

                if (separator > 0)
                {
                    map[part[..separator]] = part[(separator + 1)..];
                }
            }
        }

        return new MorphologyFeatureValues(
            Degree: map.GetValueOrDefault("Degree"),
            VerbForm: map.GetValueOrDefault("VerbForm"),
            Tense: map.GetValueOrDefault("Tense"),
            Person: map.GetValueOrDefault("Person"),
            Number: map.GetValueOrDefault("Number"),
            Mood: map.GetValueOrDefault("Mood"),
            Case: map.GetValueOrDefault("Case"),
            Gender: map.GetValueOrDefault("Gender"));
    }

    private static void Add(List<string> parts, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            parts.Add($"{key}={value}");
        }
    }
}
