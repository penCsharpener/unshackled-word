using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using UnshackledWord.Domain.Models.Grammar;

namespace UnshackledWord.Application.Features.Lxx;

/// <summary>A strictly parsed Robinson-style Greek morphology code (see <see cref="GreekMorphologyCode"/>).</summary>
public sealed record GreekMorphologyParse
{
    /// <summary>The normalised code: trimmed, upper case, crasis part as <c>" + G1234"</c>.</summary>
    public required string Code { get; init; }

    /// <summary>First segment: <c>N</c>, <c>V</c>, <c>P</c> … or a word class like <c>PREP</c>.</summary>
    public required string Head { get; init; }

    /// <summary>Part of speech, tense, voice, mood, person, case, number, gender and degree as contract keys (<see cref="MorphologyKey"/>), from <see cref="GreekMorphologyDecoder"/>.</summary>
    public required MorphologyKeys Keys { get; init; }

    /// <summary>Pronoun heads: personal, reflexive, possessive, reciprocal, demonstrative, interrogative, correlative, correlativeOrInterrogative, relative, indefinite.</summary>
    public string? PronounKind { get; init; }

    /// <summary>Indeclinable forms: properName (<c>PRI</c>), letter (<c>LI</c>), numeral (<c>NUI</c>), other (<c>OI</c>).</summary>
    public string? Indeclinable { get; init; }

    /// <summary>Verbs: the <c>2</c> of second aorist, second future, second perfect (<c>V-2AAI-3S</c>).</summary>
    public bool SecondForm { get; init; }

    /// <summary>Verbs: voice <c>D</c>, <c>O</c> or <c>N</c> (middle, passive, middle or passive deponent).</summary>
    public bool Deponent { get; init; }

    /// <summary>Possessive pronouns (<c>S-1PASF</c>): number of the possessor as contract key; the case/number/gender in <see cref="Keys"/> belong to the thing possessed.</summary>
    public string? PossessorNumber { get; init; }

    /// <summary>STEP/Robinson suffixes after the main parse in their order, e.g. <c>["LG"]</c> for <c>N-GSF-LG</c>, <c>["ATT"]</c>.</summary>
    public IReadOnlyList<string> Suffixes { get; init; } = [];

    /// <summary>Second Strong's number of a crasis or compound (<c>PREP + G1537</c>), e.g. <c>G1537</c>.</summary>
    public string? CrasisStrongs { get; init; }

    /// <summary>"Nomen Dativ Singular feminin".</summary>
    public required string German { get; init; }

    /// <summary>"noun dative singular feminine".</summary>
    public required string English { get; init; }
}

/// <summary>
/// Strict parser and validator for the Robinson-style codes of STEP's Greek NT (<c>StepGreekWords.Morphology</c>, roadmap 0028.5), the code set
/// LXX tags use: <c>V-AAI-3S</c>, <c>V-2RAP-NSM</c>, <c>N-DSF</c>, <c>N-PRI</c>, <c>P-1GS</c>, <c>F-3ASM</c>, <c>S-1PASF</c>, <c>T-NSM</c>,
/// <c>CONJ</c>, <c>PRT-N</c>, suffixes such as <c>-C</c>, <c>-S</c>, <c>-N</c>, <c>-I</c>, <c>-T</c>, <c>-L</c>, <c>-LG</c>, <c>-P</c>, <c>-PG</c>,
/// <c>-LI</c>, <c>-ATT</c>, <c>-ARAM</c>, <c>-HEB</c>, <c>-K</c> and a crasis part <c>+ G2532</c>. Unlike the lenient
/// <see cref="GreekMorphologyDecoder"/> (display), every segment must be understood: <c>V-XYZ</c>, <c>N-DS</c> or <c>N-DSF-Q</c> are rejected. The
/// decoder supplies the keys, this class the validation, the extras (pronoun kind, deponent, possessor …) and the German/English description.
/// Pure, thread-safe.
/// </summary>
public static partial class GreekMorphologyCode
{
    private static readonly HashSet<string> WordClasses = ["ADV", "CONJ", "COND", "PREP", "PRT", "INJ", "HEB", "ARAM"];

    private static readonly Dictionary<char, string> PronounKinds = new()
    {
        ['P'] = "personal",
        ['F'] = "reflexive",
        ['S'] = "possessive",
        ['C'] = "reciprocal",
        ['D'] = "demonstrative",
        ['I'] = "interrogative",
        ['K'] = "correlative",
        ['Q'] = "correlativeOrInterrogative",
        ['R'] = "relative",
        ['X'] = "indefinite",
    };

    private static readonly Dictionary<string, string> Indeclinables = new()
    {
        ["PRI"] = "properName",
        ["LI"] = "letter",
        ["NUI"] = "numeral",
        ["OI"] = "other",
    };

    /// <summary>Suffixes allowed after a word class (<c>ADV-C</c>, <c>PRT-N</c>, <c>INJ-HEB</c>).</summary>
    private static readonly HashSet<string> WordClassSuffixes = ["C", "S", "N", "I", "T", "K", "ATT", "ARAM", "HEB", "ABB"];

    /// <summary>Suffixes allowed after a nominal parse (<c>N-GSF-LG</c>, <c>A-NSM-C</c>, <c>N-NSN-LI</c>).</summary>
    private static readonly HashSet<string> NominalSuffixes = ["C", "S", "N", "I", "T", "K", "L", "LG", "P", "PG", "LI", "ATT", "ARAM", "HEB", "ABB"];

    /// <summary>Suffixes allowed after a verb parse (<c>V-AOI-2P-ATT</c>, <c>V-AAM-2S-ARAM</c>).</summary>
    private static readonly HashSet<string> VerbSuffixes = ["ATT", "ARAM", "HEB", "M", "C", "AP", "ABB"];

    [GeneratedRegex("^(?<main>[A-Z0-9-]+?)(?:\\s*\\+\\s*(?<crasis>G[0-9]{4}[A-Z]?))?$")]
    private static partial Regex CodePattern();

    [GeneratedRegex("^(?<second>2)?(?<tense>[PIFARL])(?<voice>[AMPEDON])(?<mood>[ISOMNP])$")]
    private static partial Regex VerbPattern();

    [GeneratedRegex("^[NGDAV][SP][MFN]$")]
    private static partial Regex CaseNumberGender();

    [GeneratedRegex("^[123][SP]$")]
    private static partial Regex PersonNumber();

    [GeneratedRegex("^[12][NGDAV][SP]$")]
    private static partial Regex PersonCaseNumber();

    [GeneratedRegex("^[123][NGDAV][SP][MFN]$")]
    private static partial Regex PersonCaseNumberGender();

    [GeneratedRegex("^[123][SP][NGDAV][SP][MFN]$")]
    private static partial Regex PossessivePattern();

    public static bool IsValid(string? code) => TryParse(code, out _, out _);

    /// <summary>The parse, or null when the code is invalid.</summary>
    public static GreekMorphologyParse? Parse(string? code) => TryParse(code, out var parse, out _) ? parse : null;

    /// <summary>Parses <paramref name="code"/> (case-insensitive, surrounding blanks ignored); <paramref name="error"/> says why it failed.</summary>
    public static bool TryParse(string? code, [NotNullWhen(true)] out GreekMorphologyParse? parse, [NotNullWhen(false)] out string? error)
    {
        parse = null;
        if (string.IsNullOrWhiteSpace(code))
        {
            error = "empty code";
            return false;
        }

        var match = CodePattern().Match(code.Trim().ToUpperInvariant());
        if (!match.Success)
        {
            error = $"'{code.Trim()}' is not a morphology code";
            return false;
        }

        var main = match.Groups["main"].Value;
        var crasis = match.Groups["crasis"].Success ? match.Groups["crasis"].Value : null;
        var segments = main.Split('-');
        if (segments.Any(s => s.Length == 0))
        {
            error = $"'{main}' has an empty segment";
            return false;
        }

        var head = segments[0];
        var rest = segments[1..];
        Draft? draft;
        if (WordClasses.Contains(head))
        {
            draft = Suffixed(new Draft(), rest, WordClassSuffixes, out error);
        }
        else if (head == "V")
        {
            draft = ParseVerb(rest, out error);
        }
        else if (head is "N" or "A" or "T")
        {
            draft = ParseNominal(head[0], rest, out error);
        }
        else if (head.Length == 1 && PronounKinds.TryGetValue(head[0], out var kind))
        {
            draft = ParsePronoun(head[0], kind, rest, out error);
        }
        else
        {
            draft = null;
            error = $"unknown part of speech '{head}'";
        }

        if (draft is null)
        {
            error ??= $"'{main}' is not a morphology code";
            return false;
        }

        var normalised = crasis is null ? main : $"{main} + {crasis}";
        var keys = GreekMorphologyDecoder.Decode(normalised);
        var result = new GreekMorphologyParse
        {
            Code = normalised,
            Head = head,
            Keys = keys,
            PronounKind = draft.PronounKind,
            Indeclinable = draft.Indeclinable,
            SecondForm = draft.SecondForm,
            Deponent = draft.Deponent,
            PossessorNumber = draft.PossessorNumber,
            Suffixes = draft.Suffixes,
            CrasisStrongs = crasis,
            German = "",
            English = "",
        };
        parse = result with { German = Describe(result, German), English = Describe(result, English) };
        error = null;
        return true;
    }

    private sealed record Draft
    {
        public string? PronounKind { get; init; }
        public string? Indeclinable { get; init; }
        public bool SecondForm { get; init; }
        public bool Deponent { get; init; }
        public string? PossessorNumber { get; init; }
        public IReadOnlyList<string> Suffixes { get; init; } = [];
    }

    private static Draft? ParseVerb(string[] rest, out string? error)
    {
        var tmv = rest.Length > 0 ? VerbPattern().Match(rest[0]) : Match.Empty;
        if (!tmv.Success)
        {
            error = rest.Length == 0 ? "verb without tense, voice and mood" : $"'{rest[0]}' is not tense + voice + mood";
            return null;
        }

        var draft = new Draft { SecondForm = tmv.Groups["second"].Success, Deponent = tmv.Groups["voice"].Value is "D" or "O" or "N" };
        var mood = tmv.Groups["mood"].Value[0];
        var tail = rest[1..];
        switch (mood)
        {
            case 'N':
                break;
            case 'P':
                if (tail.Length == 0 || !CaseNumberGender().IsMatch(tail[0]))
                {
                    error = "a participle needs case + number + gender (V-AAP-NSM)";
                    return null;
                }

                tail = tail[1..];
                break;
            default:
                if (tail.Length == 0 || !PersonNumber().IsMatch(tail[0]))
                {
                    error = "a finite verb needs person + number (V-AAI-3S)";
                    return null;
                }

                tail = tail[1..];
                break;
        }

        return Suffixed(draft, tail, VerbSuffixes, out error);
    }

    private static Draft? ParseNominal(char head, string[] rest, out string? error)
    {
        if (rest.Length == 0)
        {
            error = $"'{head}' needs case + number + gender";
            return null;
        }

        var first = rest[0];
        if (Indeclinables.TryGetValue(first, out var indeclinable) && head != 'T')
        {
            var allowed = head == 'N' ? first is "PRI" or "LI" or "OI" : first is "NUI" or "OI";
            if (!allowed)
            {
                error = $"'{head}-{first}' is not an indeclinable form";
                return null;
            }

            return Suffixed(new Draft { Indeclinable = indeclinable }, rest[1..], NominalSuffixes, out error);
        }

        if (!CaseNumberGender().IsMatch(first))
        {
            error = $"'{first}' is not case + number + gender";
            return null;
        }

        return Suffixed(new Draft(), rest[1..], NominalSuffixes, out error);
    }

    private static Draft? ParsePronoun(char head, string kind, string[] rest, out string? error)
    {
        var first = rest.Length > 0 ? rest[0] : "";
        var draft = new Draft { PronounKind = kind };
        var ok = head switch
        {
            // personal: 1st/2nd person without gender (P-1GS), 3rd person (αὐτός) with gender (P-GSM)
            'P' => PersonCaseNumber().IsMatch(first) || CaseNumberGender().IsMatch(first),
            'F' => PersonCaseNumberGender().IsMatch(first),
            'S' => PossessivePattern().IsMatch(first),
            _ => CaseNumberGender().IsMatch(first),
        };
        if (!ok)
        {
            error = head switch
            {
                'P' => $"'{first}' is not person + case + number (P-1GS) or case + number + gender (P-GSM)",
                'F' => $"'{first}' is not person + case + number + gender (F-3ASM)",
                'S' => $"'{first}' is not person + possessor number + case + number + gender (S-1PASF)",
                _ => $"'{first}' is not case + number + gender",
            };
            return null;
        }

        if (head == 'S')
        {
            draft = draft with { PossessorNumber = first[1] == 'S' ? MorphologyKey.Singular : MorphologyKey.Plural };
        }

        return Suffixed(draft, rest[1..], NominalSuffixes, out error);
    }

    private static Draft? Suffixed(Draft draft, string[] suffixes, HashSet<string> allowed, out string? error)
    {
        foreach (var suffix in suffixes)
        {
            if (!allowed.Contains(suffix))
            {
                error = $"unknown suffix '-{suffix}'";
                return null;
            }
        }

        if (suffixes.Distinct().Count() != suffixes.Length)
        {
            error = "a suffix appears twice";
            return null;
        }

        error = null;
        return draft with { Suffixes = suffixes };
    }

    // ---- description ----------------------------------------------------------------------------------------------------------------

    private sealed record Labels(
        Dictionary<string, string> Heads,
        Dictionary<string, string> PronounKinds,
        Dictionary<string, string> Indeclinables,
        Dictionary<string, string> Keys,
        Dictionary<string, string> Suffixes,
        Func<string, string> SecondForm,
        string Deponent,
        Func<string, string> Possessor,
        Func<string, string> Crasis);

    private static readonly Labels German = new(
        Heads: new()
        {
            ["N"] = "Nomen", ["A"] = "Adjektiv", ["T"] = "Artikel", ["V"] = "Verb", ["ADV"] = "Adverb", ["CONJ"] = "Konjunktion",
            ["COND"] = "Konditionalpartikel", ["PREP"] = "Präposition", ["PRT"] = "Partikel", ["INJ"] = "Interjektion",
            ["HEB"] = "hebräisches Wort", ["ARAM"] = "aramäisches Wort",
        },
        PronounKinds: new()
        {
            ["personal"] = "Personalpronomen", ["reflexive"] = "Reflexivpronomen", ["possessive"] = "Possessivpronomen",
            ["reciprocal"] = "Reziprokpronomen", ["demonstrative"] = "Demonstrativpronomen", ["interrogative"] = "Interrogativpronomen",
            ["correlative"] = "Korrelativpronomen", ["correlativeOrInterrogative"] = "Korrelativ- oder Interrogativpronomen",
            ["relative"] = "Relativpronomen", ["indefinite"] = "Indefinitpronomen",
        },
        Indeclinables: new()
        {
            ["properName"] = "Eigenname (indeklinabel)", ["letter"] = "Buchstabe (indeklinabel)", ["numeral"] = "Zahlwort (indeklinabel)",
            ["other"] = "indeklinabel",
        },
        Keys: new()
        {
            [MorphologyKey.Present] = "Präsens", [MorphologyKey.Imperfect] = "Imperfekt", [MorphologyKey.Future] = "Futur",
            [MorphologyKey.Aorist] = "Aorist", [MorphologyKey.Perfect] = "Perfekt", [MorphologyKey.Pluperfect] = "Plusquamperfekt",
            [MorphologyKey.Active] = "Aktiv", [MorphologyKey.Middle] = "Medium", [MorphologyKey.Passive] = "Passiv",
            [MorphologyKey.MiddleOrPassive] = "Medium oder Passiv",
            [MorphologyKey.Indicative] = "Indikativ", [MorphologyKey.Subjunctive] = "Konjunktiv", [MorphologyKey.Optative] = "Optativ",
            [MorphologyKey.Imperative] = "Imperativ", [MorphologyKey.Infinitive] = "Infinitiv", [MorphologyKey.Participle] = "Partizip",
            [MorphologyKey.First] = "1. Person", [MorphologyKey.Second] = "2. Person", [MorphologyKey.Third] = "3. Person",
            [MorphologyKey.Nominative] = "Nominativ", [MorphologyKey.Genitive] = "Genitiv", [MorphologyKey.Dative] = "Dativ",
            [MorphologyKey.Accusative] = "Akkusativ", [MorphologyKey.Vocative] = "Vokativ",
            [MorphologyKey.Singular] = "Singular", [MorphologyKey.Plural] = "Plural",
            [MorphologyKey.Masculine] = "maskulin", [MorphologyKey.Feminine] = "feminin", [MorphologyKey.Neuter] = "neutrum",
            [MorphologyKey.Comparative] = "Komparativ", [MorphologyKey.Superlative] = "Superlativ",
        },
        Suffixes: new()
        {
            ["N"] = "verneint", ["I"] = "fragend", ["T"] = "Titel", ["K"] = "Krasis mit καί", ["L"] = "Ortsname",
            ["LG"] = "Herkunftsbezeichnung (Ort)", ["P"] = "Personenname", ["PG"] = "Herkunftsbezeichnung (Person)", ["LI"] = "Buchstabe",
            ["ATT"] = "attische Form", ["ARAM"] = "aramäisch", ["HEB"] = "hebräisch", ["M"] = "mediale Bedeutung", ["AP"] = "apokopiert",
            ["ABB"] = "abgekürzt",
        },
        SecondForm: tense => $"2. {tense}",
        Deponent: "(Deponens)",
        Possessor: number => $"(Besitzer {number})",
        Crasis: strongs => $"verbunden mit {strongs}");

    private static readonly Labels English = new(
        Heads: new()
        {
            ["N"] = "noun", ["A"] = "adjective", ["T"] = "article", ["V"] = "verb", ["ADV"] = "adverb", ["CONJ"] = "conjunction",
            ["COND"] = "conditional particle", ["PREP"] = "preposition", ["PRT"] = "particle", ["INJ"] = "interjection",
            ["HEB"] = "Hebrew word", ["ARAM"] = "Aramaic word",
        },
        PronounKinds: new()
        {
            ["personal"] = "personal pronoun", ["reflexive"] = "reflexive pronoun", ["possessive"] = "possessive pronoun",
            ["reciprocal"] = "reciprocal pronoun", ["demonstrative"] = "demonstrative pronoun", ["interrogative"] = "interrogative pronoun",
            ["correlative"] = "correlative pronoun", ["correlativeOrInterrogative"] = "correlative or interrogative pronoun",
            ["relative"] = "relative pronoun", ["indefinite"] = "indefinite pronoun",
        },
        Indeclinables: new()
        {
            ["properName"] = "proper noun (indeclinable)", ["letter"] = "letter (indeclinable)", ["numeral"] = "numeral (indeclinable)",
            ["other"] = "indeclinable",
        },
        Keys: new()
        {
            [MorphologyKey.Present] = "present", [MorphologyKey.Imperfect] = "imperfect", [MorphologyKey.Future] = "future",
            [MorphologyKey.Aorist] = "aorist", [MorphologyKey.Perfect] = "perfect", [MorphologyKey.Pluperfect] = "pluperfect",
            [MorphologyKey.Active] = "active", [MorphologyKey.Middle] = "middle", [MorphologyKey.Passive] = "passive",
            [MorphologyKey.MiddleOrPassive] = "middle or passive",
            [MorphologyKey.Indicative] = "indicative", [MorphologyKey.Subjunctive] = "subjunctive", [MorphologyKey.Optative] = "optative",
            [MorphologyKey.Imperative] = "imperative", [MorphologyKey.Infinitive] = "infinitive", [MorphologyKey.Participle] = "participle",
            [MorphologyKey.First] = "1st person", [MorphologyKey.Second] = "2nd person", [MorphologyKey.Third] = "3rd person",
            [MorphologyKey.Nominative] = "nominative", [MorphologyKey.Genitive] = "genitive", [MorphologyKey.Dative] = "dative",
            [MorphologyKey.Accusative] = "accusative", [MorphologyKey.Vocative] = "vocative",
            [MorphologyKey.Singular] = "singular", [MorphologyKey.Plural] = "plural",
            [MorphologyKey.Masculine] = "masculine", [MorphologyKey.Feminine] = "feminine", [MorphologyKey.Neuter] = "neuter",
            [MorphologyKey.Comparative] = "comparative", [MorphologyKey.Superlative] = "superlative",
        },
        Suffixes: new()
        {
            ["N"] = "negative", ["I"] = "interrogative", ["T"] = "title", ["K"] = "crasis with καί", ["L"] = "place name",
            ["LG"] = "gentilic (place)", ["P"] = "person name", ["PG"] = "gentilic (person)", ["LI"] = "letter", ["ATT"] = "Attic form",
            ["ARAM"] = "Aramaic", ["HEB"] = "Hebrew", ["M"] = "middle significance", ["AP"] = "apocopated", ["ABB"] = "abbreviated",
        },
        SecondForm: tense => $"second {tense}",
        Deponent: "(deponent)",
        Possessor: number => $"(possessor {number})",
        Crasis: strongs => $"combined with {strongs}");

    /// <summary>"Verb 2. Aorist Aktiv Indikativ 3. Person Singular", "Nomen Akkusativ Singular maskulin, Ortsname".</summary>
    private static string Describe(GreekMorphologyParse parse, Labels labels)
    {
        var words = new List<string>();
        if (parse.PronounKind is not null)
        {
            words.Add(labels.PronounKinds[parse.PronounKind]);
        }
        else if (parse.Indeclinable is not null)
        {
            words.Add(labels.Indeclinables[parse.Indeclinable]);
        }
        else
        {
            words.Add(labels.Heads[parse.Head]);
        }

        var keys = parse.Keys;
        if (keys.Tense is not null)
        {
            var tense = labels.Keys[keys.Tense];
            words.Add(parse.SecondForm ? labels.SecondForm(tense) : tense);
        }

        Add(keys.Voice);
        if (parse.Deponent)
        {
            words.Add(labels.Deponent);
        }

        Add(keys.Mood);
        Add(keys.Person);
        if (parse.PossessorNumber is not null)
        {
            words.Add(labels.Possessor(labels.Keys[parse.PossessorNumber]));
        }

        Add(keys.Case);
        Add(keys.Number);
        Add(keys.Gender);
        Add(keys.Degree);

        var text = string.Join(' ', words);
        var extras = parse.Suffixes
            .Where(s => s is not ("C" or "S") || keys.Degree is null)
            .Select(s => labels.Suffixes.GetValueOrDefault(s) ?? labels.Keys[s == "C" ? MorphologyKey.Comparative : MorphologyKey.Superlative])
            .ToList();
        if (parse.CrasisStrongs is not null)
        {
            extras.Add(labels.Crasis(parse.CrasisStrongs));
        }

        return extras.Count == 0 ? text : $"{text}, {string.Join(", ", extras)}";

        void Add(string? key)
        {
            if (key is not null)
            {
                words.Add(labels.Keys[key]);
            }
        }
    }
}
