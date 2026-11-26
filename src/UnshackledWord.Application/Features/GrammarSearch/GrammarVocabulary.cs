using System.Globalization;
using System.Text;
using UnshackledWord.Domain.Models.Grammar;

namespace UnshackledWord.Application.Features.GrammarSearch;

/// <summary>Languages a key applies to.</summary>
[Flags]
public enum GrammarLanguages
{
    None = 0,
    Greek = 1,
    Hebrew = 2,
    Aramaic = 4,
    Original = Greek | Hebrew | Aramaic,
}

/// <summary>How a key is evaluated.</summary>
public enum GrammarKeyKind
{
    /// <summary>One part of the decoded <see cref="MorphologyKeys"/> (<see cref="GrammarKeyDef.Selector"/>) compared with the value ids.</summary>
    Morphology,

    /// <summary><c>Sprache</c>: the language of the word.</summary>
    Language,

    /// <summary><c>Code</c>: raw code pattern with <c>*</c> as wildcard (no value list).</summary>
    Code,

    /// <summary><c>Strong</c> / <c>Lemma</c>: forces how a word token is read; opens a slot.</summary>
    Word,
}

/// <summary>One value of a key. <see cref="Id"/> is the <see cref="MorphologyKey"/> constant (or a language id).</summary>
public sealed record GrammarValueDef(string Id, string De, string En, IReadOnlyList<string>? Aliases = null);

/// <summary>One search key. <see cref="GermanPrefix"/> says whether <c>dt.</c> may precede it (applied to the German word).</summary>
public sealed class GrammarKeyDef
{
    public required string Name { get; init; }
    public IReadOnlyList<string> Aliases { get; init; } = [];
    public required GrammarKeyKind Kind { get; init; }
    public required GrammarLanguages Languages { get; init; }
    public bool GermanPrefix { get; init; }
    public Func<MorphologyKeys, string?>? Selector { get; init; }
    public IReadOnlyList<GrammarValueDef> Values { get; init; } = [];
}

/// <summary>
/// The search vocabulary (keys, aliases, values) of roadmap 0029. German labels equal <c>de.ts</c> <c>grammar.*</c> (a test enforces it in row 0029.3);
/// English labels are data only. This row holds the Greek keys; Hebrew keys and the <c>dt.</c> keys are added by later rows through the same types.
/// </summary>
public static class GrammarVocabulary
{
    public const string GermanPrefix = "dt.";

    public const string LanguageGreek = "greek";
    public const string LanguageHebrew = "hebrew";
    public const string LanguageAramaic = "aramaic";

    private static GrammarValueDef V(string id, string de, string en) => new(id, de, en);

    /// <summary>Case, umlaut and ß folded text used to match keys, aliases and values.</summary>
    public static string Fold(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var sb = new StringBuilder(text.Length);
        foreach (var c in text.Trim().ToLowerInvariant().Replace("ß", "ss", StringComparison.Ordinal).Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        }

        return sb.ToString();
    }

    /// <summary>Switched on by roadmap row 0029.7, when the search can apply German filters.</summary>
    public static readonly bool GermanKeysEnabled = false;

    public static readonly IReadOnlyList<GrammarKeyDef> Keys =
    [
        new()
        {
            Name = "Wortart", Aliases = ["Wa"], Kind = GrammarKeyKind.Morphology, Languages = GrammarLanguages.Original, GermanPrefix = true,
            Selector = k => k.PartOfSpeech,
            Values =
            [
                V(MorphologyKey.Noun, "Substantiv", "Noun"), V(MorphologyKey.NounProper, "Eigenname", "Proper name"), V(MorphologyKey.Verb, "Verb", "Verb"),
                V(MorphologyKey.Adjective, "Adjektiv", "Adjective"), V(MorphologyKey.Adverb, "Adverb", "Adverb"), V(MorphologyKey.Article, "Artikel", "Article"),
                V(MorphologyKey.Pronoun, "Pronomen", "Pronoun"), V(MorphologyKey.Preposition, "Präposition", "Preposition"),
                V(MorphologyKey.Conjunction, "Konjunktion", "Conjunction"), V(MorphologyKey.Particle, "Partikel", "Particle"),
                V(MorphologyKey.Interjection, "Interjektion", "Interjection"), V(MorphologyKey.Indeclinable, "indeklinabel", "Indeclinable"),
            ],
        },
        new()
        {
            Name = "Tempus", Aliases = ["Zeit"], Kind = GrammarKeyKind.Morphology, Languages = GrammarLanguages.Greek, GermanPrefix = true,
            Selector = k => k.Tense,
            Values =
            [
                V(MorphologyKey.Present, "Präsens", "Present"), V(MorphologyKey.Imperfect, "Imperfekt", "Imperfect"), V(MorphologyKey.Future, "Futur", "Future"),
                V(MorphologyKey.Aorist, "Aorist", "Aorist"), V(MorphologyKey.Perfect, "Perfekt", "Perfect"), V(MorphologyKey.Pluperfect, "Plusquamperfekt", "Pluperfect"),
                V(MorphologyKey.FuturePerfect, "Futur II", "Future perfect"),
            ],
        },
        new()
        {
            Name = "Diathese", Aliases = ["GenusVerbi"], Kind = GrammarKeyKind.Morphology, Languages = GrammarLanguages.Greek,
            Selector = k => k.Voice,
            Values =
            [
                V(MorphologyKey.Active, "Aktiv", "Active"), V(MorphologyKey.Middle, "Medium", "Middle"), V(MorphologyKey.Passive, "Passiv", "Passive"),
                V(MorphologyKey.MiddleOrPassive, "Medium oder Passiv", "Middle or passive"),
            ],
        },
        new()
        {
            Name = "Modus", Kind = GrammarKeyKind.Morphology, Languages = GrammarLanguages.Greek, GermanPrefix = true,
            Selector = k => k.Mood,
            Values =
            [
                V(MorphologyKey.Indicative, "Indikativ", "Indicative"), V(MorphologyKey.Subjunctive, "Konjunktiv", "Subjunctive"), V(MorphologyKey.Optative, "Optativ", "Optative"),
                V(MorphologyKey.Imperative, "Imperativ", "Imperative"), V(MorphologyKey.Infinitive, "Infinitiv", "Infinitive"), V(MorphologyKey.Participle, "Partizip", "Participle"),
            ],
        },
        new()
        {
            Name = "Person", Kind = GrammarKeyKind.Morphology, Languages = GrammarLanguages.Original, GermanPrefix = true,
            Selector = k => k.Person,
            Values = [V(MorphologyKey.First, "1. Person", "1st person"), V(MorphologyKey.Second, "2. Person", "2nd person"), V(MorphologyKey.Third, "3. Person", "3rd person")],
        },
        new()
        {
            Name = "Kasus", Aliases = ["Fall"], Kind = GrammarKeyKind.Morphology, Languages = GrammarLanguages.Greek,
            Selector = k => k.Case,
            Values =
            [
                V(MorphologyKey.Nominative, "Nominativ", "Nominative"), V(MorphologyKey.Genitive, "Genitiv", "Genitive"), V(MorphologyKey.Dative, "Dativ", "Dative"),
                V(MorphologyKey.Accusative, "Akkusativ", "Accusative"), V(MorphologyKey.Vocative, "Vokativ", "Vocative"),
            ],
        },
        new()
        {
            Name = "Numerus", Aliases = ["Zahl"], Kind = GrammarKeyKind.Morphology, Languages = GrammarLanguages.Original, GermanPrefix = true,
            Selector = k => k.Number,
            Values = [V(MorphologyKey.Singular, "Singular", "Singular"), V(MorphologyKey.Dual, "Dual", "Dual"), V(MorphologyKey.Plural, "Plural", "Plural")],
        },
        new()
        {
            Name = "Geschlecht", Aliases = ["Genus"], Kind = GrammarKeyKind.Morphology, Languages = GrammarLanguages.Original, GermanPrefix = true,
            Selector = k => k.Gender,
            Values =
            [
                V(MorphologyKey.Masculine, "maskulin", "Masculine"), V(MorphologyKey.Feminine, "feminin", "Feminine"), V(MorphologyKey.Neuter, "neutrum", "Neuter"),
                V(MorphologyKey.Common, "beiderlei", "Common"),
            ],
        },
        new()
        {
            Name = "Steigerung", Kind = GrammarKeyKind.Morphology, Languages = GrammarLanguages.Greek, GermanPrefix = true,
            Selector = k => k.Degree,
            Values = [V(MorphologyKey.Comparative, "Komparativ", "Comparative"), V(MorphologyKey.Superlative, "Superlativ", "Superlative")],
        },
        new()
        {
            Name = "Sprache", Kind = GrammarKeyKind.Language, Languages = GrammarLanguages.Original,
            Values = [V(LanguageGreek, "Griechisch", "Greek"), V(LanguageHebrew, "Hebräisch", "Hebrew"), V(LanguageAramaic, "Aramäisch", "Aramaic")],
        },
        new() { Name = "Code", Kind = GrammarKeyKind.Code, Languages = GrammarLanguages.Original },
        new() { Name = "Strong", Kind = GrammarKeyKind.Word, Languages = GrammarLanguages.Original },
        new() { Name = "Lemma", Kind = GrammarKeyKind.Word, Languages = GrammarLanguages.Original },
    ];

    /// <summary>Key by folded name or alias; null when unknown. <paramref name="german"/> keys (<c>dt.</c>) are not offered yet and never match.</summary>
    public static GrammarKeyDef? FindKey(string name, bool german)
    {
        // The German word is not searchable before roadmap row 0029.7: every dt. key is unknown until then.
        if (german && !GermanKeysEnabled) return null;
        var folded = Fold(name);
        foreach (var key in Keys)
        {
            if (german && !key.GermanPrefix) continue;
            if (Fold(key.Name) == folded || key.Aliases.Any(a => Fold(a) == folded)) return key;
        }

        return null;
    }

    /// <summary>Key names (not aliases) that can be typed, for the candidate list of an unknownKey error.</summary>
    public static IReadOnlyList<string> KeyNames(bool german) => [.. Keys.Where(k => !german || (GermanKeysEnabled && k.GermanPrefix)).Select(k => german ? GermanPrefix + k.Name : k.Name)];
}
