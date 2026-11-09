namespace UnshackledWord.Domain.Models.Grammar;

/// <summary>Decodes Robinson morphology codes as used by TAGNT (e.g. <c>N-NSM</c>, <c>V-2AAI-3S</c>) into contract keys. Pure, never throws.</summary>
public static class GreekMorphologyDecoder
{
    public static MorphologyKeys Decode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return MorphologyKeys.Empty;
        }

        var parts = code.Trim().ToUpperInvariant().Split('-');
        var head = parts[0];
        var tail = parts.Skip(1).ToArray();

        switch (head)
        {
            case "ADV":
                return new MorphologyKeys { PartOfSpeech = MorphologyKey.Adverb };
            case "CONJ":
                return new MorphologyKeys { PartOfSpeech = MorphologyKey.Conjunction };
            case "PREP":
                return new MorphologyKeys { PartOfSpeech = MorphologyKey.Preposition };
            case "PRT":
                return new MorphologyKeys { PartOfSpeech = MorphologyKey.Particle };
            case "INJ":
                return new MorphologyKeys { PartOfSpeech = MorphologyKey.Interjection };
            case "COND":
                return new MorphologyKeys { PartOfSpeech = MorphologyKey.Conjunction };
        }

        if (head.Length != 1)
        {
            return MorphologyKeys.Empty;
        }

        return head[0] switch
        {
            'N' => DecodeNominal(MorphologyKey.Noun, tail),
            'A' => DecodeNominal(MorphologyKey.Adjective, tail),
            'T' => DecodeNominal(MorphologyKey.Article, tail),
            'V' => DecodeVerb(tail),
            'P' or 'R' or 'C' or 'D' or 'K' or 'I' or 'X' or 'Q' or 'F' or 'S' => DecodeNominal(MorphologyKey.Pronoun, tail),
            _ => MorphologyKeys.Empty
        };
    }

    private static MorphologyKeys DecodeNominal(string partOfSpeech, string[] tail)
    {
        if (tail.Length == 0)
        {
            return new MorphologyKeys { PartOfSpeech = partOfSpeech };
        }

        // Indeclinable forms: N-PRI (proper name), N-LI, N-OI, A-NUI ...
        var first = tail[0];
        if (first == "PRI" && partOfSpeech == MorphologyKey.Noun)
        {
            return new MorphologyKeys { PartOfSpeech = MorphologyKey.NounProper };
        }

        if (first.EndsWith('I') && first.Length >= 2 && first != "PRI" && !IsCaseNumberGender(first))
        {
            return new MorphologyKeys { PartOfSpeech = MorphologyKey.Indeclinable };
        }

        var keys = new MorphologyKeys { PartOfSpeech = partOfSpeech };

        // Personal pronouns: P-1NS (person + case + number); also P-1GS etc.
        if (first.Length == 3 && first[0] is '1' or '2' or '3' && char.IsLetter(first[1]))
        {
            keys = keys with
            {
                Person = PersonOf(first[0]),
                Case = CaseOf(first[1]),
                Number = NumberOf(first[2]),
            };
        }
        else if (first.Length == 3)
        {
            keys = keys with
            {
                Case = CaseOf(first[0]),
                Number = NumberOf(first[1]),
                Gender = GenderOf(first[2]),
            };
        }
        else if (first.Length == 2)
        {
            // Pronouns without gender (e.g. reflexive/reciprocal) : case + number
            keys = keys with { Case = CaseOf(first[0]), Number = NumberOf(first[1]) };
        }

        if (tail.Length > 1)
        {
            keys = tail[1] switch
            {
                "C" => keys with { Degree = MorphologyKey.Comparative },
                "S" => keys with { Degree = MorphologyKey.Superlative },
                _ => keys
            };
        }

        return keys;
    }

    private static bool IsCaseNumberGender(string value)
    {
        return value.Length == 3 && CaseOf(value[0]) is not null && NumberOf(value[1]) is not null && GenderOf(value[2]) is not null;
    }

    private static MorphologyKeys DecodeVerb(string[] tail)
    {
        var keys = new MorphologyKeys { PartOfSpeech = MorphologyKey.Verb };
        if (tail.Length == 0)
        {
            return keys;
        }

        var tmv = tail[0];
        if (tmv.Length > 0 && tmv[0] == '2')
        {
            tmv = tmv[1..];
        }

        if (tmv.Length != 3)
        {
            return keys;
        }

        var tense = tmv[0] switch
        {
            'P' => MorphologyKey.Present,
            'I' => MorphologyKey.Imperfect,
            'F' => MorphologyKey.Future,
            'A' => MorphologyKey.Aorist,
            'R' => MorphologyKey.Perfect,
            'L' => MorphologyKey.Pluperfect,
            _ => null
        };
        var voice = tmv[1] switch
        {
            'A' => MorphologyKey.Active,
            'M' or 'D' => MorphologyKey.Middle,
            'P' or 'O' => MorphologyKey.Passive,
            'E' or 'N' => MorphologyKey.MiddleOrPassive,
            _ => null
        };
        var mood = tmv[2] switch
        {
            'I' => MorphologyKey.Indicative,
            'S' => MorphologyKey.Subjunctive,
            'O' => MorphologyKey.Optative,
            'M' => MorphologyKey.Imperative,
            'N' => MorphologyKey.Infinitive,
            'P' => MorphologyKey.Participle,
            _ => null
        };
        keys = keys with { Tense = tense, Voice = voice, Mood = mood };

        if (tail.Length > 1)
        {
            var rest = tail[1];
            if (rest.Length == 2 && rest[0] is '1' or '2' or '3')
            {
                keys = keys with { Person = PersonOf(rest[0]), Number = NumberOf(rest[1]) };
            }
            else if (rest.Length == 3)
            {
                keys = keys with { Case = CaseOf(rest[0]), Number = NumberOf(rest[1]), Gender = GenderOf(rest[2]) };
            }
        }

        return keys;
    }

    private static string? PersonOf(char c) => c switch
    {
        '1' => MorphologyKey.First,
        '2' => MorphologyKey.Second,
        '3' => MorphologyKey.Third,
        _ => null
    };

    private static string? CaseOf(char c) => c switch
    {
        'N' => MorphologyKey.Nominative,
        'G' => MorphologyKey.Genitive,
        'D' => MorphologyKey.Dative,
        'A' => MorphologyKey.Accusative,
        'V' => MorphologyKey.Vocative,
        _ => null
    };

    private static string? NumberOf(char c) => c switch
    {
        'S' => MorphologyKey.Singular,
        'P' => MorphologyKey.Plural,
        _ => null
    };

    private static string? GenderOf(char c) => c switch
    {
        'M' => MorphologyKey.Masculine,
        'F' => MorphologyKey.Feminine,
        'N' => MorphologyKey.Neuter,
        _ => null
    };
}
