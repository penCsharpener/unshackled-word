namespace UnshackledWord.Tooling.SeedDb.Services.StatisticalRestorationGnt;

public class ConjugationParser
{
    public static ConjugationKey ParseKey(string key)
    {
        var parts = key.ToCharArray();
        var result = new ConjugationKey();

        var mood = parts[0] switch
        {
            'I' => "Indicative",
            'P' => "Participle",
            'N' => "Infinitive",
            'M' => "Imperative",
            'S' => "Subjunctive",
            'O' => "Optative",
            _ => null
        };

        result.Mood = mood;

        var tense = parts[1] switch
        {
            'A' => "Aorist",
            'P' => "Present",
            'I' => "Imperfect",
            'F' => "Future",
            'E' => "Perfect",
            'L' => "Pluperfect",
            'U' => "Future Perfect",
            _ => null
        };

        result.Tense = tense;

        var modus = parts[2] switch
        {
            'A' => "active",
            'P' => "passive",
            'M' => "medium",
            _ => null
        };

        result.Mode = modus;

        var person = parts[3] switch
        {
            '1' => "1st",
            '2' => "2nd",
            '3' => "3rd",
            _ => null
        };

        result.Person = person;

        var @case = parts[4] switch
        {
            'N' => "Nominative",
            'G' => "Genitive",
            'D' => "Dative",
            'A' => "Accusative",
            'V' => "Vocative",
            _ => null
        };

        result.Case = @case;

        var gender = parts[5] switch
        {
            'M' => "masculine",
            'F' => "feminine",
            'N' => "neuter",
            _ => null
        };

        result.Gender = gender;

        var number = parts[6] switch
        {
            'S' => "singular",
            'P' => "plural",
            _ => null
        };

        result.Number = number;

        return result;
    }
}

public sealed class ConjugationKey
{
    public string? Case { get; set; }
    public string? Gender { get; set; }
    public string? Number { get; set; }
    public string? Person { get; set; }
    public string? Mode { get; set; }
    public string? Tense { get; set; }
    public string? Mood { get; set; }

    public override string ToString()
    {
        return $"{Mood}-{Tense}-{Mode}-{Person}-{Case}-{Gender}-{Number}";
    }
}
