namespace UnshackledWord.Domain.Models.Grammar;

/// <summary>Result of decoding a whole Hebrew/Aramaic morphology code such as <c>HR/Ncfsa</c>.</summary>
/// <param name="Root">Keys of the root segment (last segment that is not a suffix); empty when the code is not understood.</param>
/// <param name="Segments">Keys per segment in word order (prefixes, root, suffixes).</param>
/// <param name="SegmentCodes">The raw segment codes without the language letter, same order as <paramref name="Segments"/>.</param>
/// <param name="RootIndex">Index of the root segment, -1 when there is none.</param>
/// <param name="IsAramaic">The code starts with <c>A</c>.</param>
public sealed record HebrewMorphologyResult(
    MorphologyKeys Root,
    IReadOnlyList<MorphologyKeys> Segments,
    IReadOnlyList<string> SegmentCodes,
    int RootIndex,
    bool IsAramaic)
{
    public static readonly HebrewMorphologyResult Empty = new(MorphologyKeys.Empty, [], [], -1, false);
}

/// <summary>Decodes OSHB-style morphology codes (as in STEP TAHOT) into contract keys. Pure, never throws.</summary>
public static class HebrewMorphologyDecoder
{
    public static HebrewMorphologyResult Decode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return HebrewMorphologyResult.Empty;
        }

        var text = code.Trim();
        var language = char.ToUpperInvariant(text[0]);
        if (language is not ('H' or 'A'))
        {
            return HebrewMorphologyResult.Empty;
        }

        var codes = text[1..].Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (codes.Length == 0)
        {
            return HebrewMorphologyResult.Empty;
        }

        var segments = codes.Select(DecodeSegment).ToList();
        var rootIndex = -1;
        for (var i = codes.Length - 1; i >= 0; i--)
        {
            if (codes[i][0] != 'S')
            {
                rootIndex = i;
                break;
            }
        }

        return new HebrewMorphologyResult(
            rootIndex >= 0 ? segments[rootIndex] : MorphologyKeys.Empty,
            segments,
            codes,
            rootIndex,
            language == 'A');
    }

    /// <summary>Decodes one segment without the language letter, e.g. <c>Ncfsa</c>, <c>Vqw3ms</c>, <c>Sp3ms</c>.</summary>
    public static MorphologyKeys DecodeSegment(string? segment)
    {
        if (string.IsNullOrEmpty(segment))
        {
            return MorphologyKeys.Empty;
        }

        var type = segment[0];
        var rest = segment[1..];
        switch (type)
        {
            case 'C':
                return new MorphologyKeys { PartOfSpeech = MorphologyKey.Conjunction };
            case 'D':
                return new MorphologyKeys { PartOfSpeech = MorphologyKey.Adverb };
            case 'R':
                return new MorphologyKeys { PartOfSpeech = MorphologyKey.Preposition };
            case 'T':
                return new MorphologyKeys { PartOfSpeech = rest.StartsWith('d') ? MorphologyKey.Article : MorphologyKey.Particle };
            case 'A':
                return ParseTail(new MorphologyKeys { PartOfSpeech = MorphologyKey.Adjective }, Skip(rest));
            case 'N':
                return ParseTail(
                    new MorphologyKeys { PartOfSpeech = rest.StartsWith('p') ? MorphologyKey.NounProper : MorphologyKey.Noun },
                    Skip(rest));
            case 'P':
                return ParseTail(new MorphologyKeys { PartOfSpeech = MorphologyKey.Pronoun }, Skip(rest));
            case 'S':
                return ParseTail(new MorphologyKeys { PartOfSpeech = MorphologyKey.Suffix }, Skip(rest));
            case 'V':
                return DecodeVerb(rest);
            default:
                return MorphologyKeys.Empty;
        }
    }

    private static string Skip(string rest) => rest.Length > 0 ? rest[1..] : rest;

    private static MorphologyKeys DecodeVerb(string rest)
    {
        var keys = new MorphologyKeys { PartOfSpeech = MorphologyKey.Verb };
        if (rest.Length == 0)
        {
            return keys;
        }

        keys = keys with
        {
            Stem = rest[0] switch
            {
                'q' => MorphologyKey.Qal,
                'N' => MorphologyKey.Niphal,
                'p' => MorphologyKey.Piel,
                'P' => MorphologyKey.Pual,
                'h' => MorphologyKey.Hiphil,
                'H' => MorphologyKey.Hophal,
                't' => MorphologyKey.Hithpael,
                _ => MorphologyKey.Other
            }
        };
        if (rest.Length == 1)
        {
            return keys;
        }

        keys = keys with
        {
            HebrewForm = rest[1] switch
            {
                'p' => MorphologyKey.Perfect,
                'q' => MorphologyKey.SequentialPerfect,
                'i' => MorphologyKey.Imperfect,
                'w' => MorphologyKey.SequentialImperfect,
                'h' => MorphologyKey.Cohortative,
                'j' => MorphologyKey.Jussive,
                'v' => MorphologyKey.Imperative,
                'r' => MorphologyKey.ParticipleActive,
                's' => MorphologyKey.ParticiplePassive,
                'a' => MorphologyKey.InfinitiveAbsolute,
                'c' => MorphologyKey.InfinitiveConstruct,
                _ => null
            }
        };

        return ParseTail(keys, rest[2..]);
    }

    /// <summary>Reads optional person digit, gender, number and state in that order.</summary>
    private static MorphologyKeys ParseTail(MorphologyKeys keys, string tail)
    {
        var i = 0;
        if (i < tail.Length && tail[i] is '1' or '2' or '3')
        {
            keys = keys with { Person = tail[i] == '1' ? MorphologyKey.First : tail[i] == '2' ? MorphologyKey.Second : MorphologyKey.Third };
            i++;
        }

        if (i < tail.Length && tail[i] is 'm' or 'f' or 'b' or 'c')
        {
            keys = keys with
            {
                Gender = tail[i] switch
                {
                    'm' => MorphologyKey.Masculine,
                    'f' => MorphologyKey.Feminine,
                    _ => MorphologyKey.Common
                }
            };
            i++;
        }

        if (i < tail.Length && tail[i] is 's' or 'p' or 'd')
        {
            keys = keys with
            {
                Number = tail[i] switch
                {
                    's' => MorphologyKey.Singular,
                    'p' => MorphologyKey.Plural,
                    _ => MorphologyKey.Dual
                }
            };
            i++;
        }

        if (i < tail.Length && tail[i] is 'a' or 'c' or 'd')
        {
            keys = keys with
            {
                State = tail[i] switch
                {
                    'a' => MorphologyKey.Absolute,
                    'c' => MorphologyKey.Construct,
                    _ => MorphologyKey.Determined
                }
            };
        }

        return keys;
    }
}
