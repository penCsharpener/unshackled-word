namespace UnshackledWord.Domain.Models.Grammar;

/// <summary>One abbreviation as stored in Elb1871Morphology, with its full-length labels.</summary>
public sealed record ValueLabel(string Code, string LabelDe, string LabelEn);

public sealed record VerbFormRule(string VerbForm, IReadOnlyList<string> Fields);

/// <summary>Fields applicable to a part of speech; ByVerbForm is set only for verbs.</summary>
public sealed record ApplicabilityRule(string PartOfSpeech, IReadOnlyList<string> Fields, IReadOnlyList<VerbFormRule>? ByVerbForm = null);

public sealed record FieldValueMap(string Field, IReadOnlyList<ValueLabel> Values)
{
    public bool Contains(string code) => Values.Any(v => v.Code == code);
}

/// <summary>
/// Single source of truth for the abbreviated values in "Elb1871Morphology" (UD-style tags written by the
/// lemmatizer/AI worker, plus STTS in <c>Stts</c>). Field names are the camelCase JSON property names.
/// </summary>
public static class ElbLemmaValueMaps
{
    private static readonly IReadOnlyList<VerbFormRule> VerbFormRules =
    [
        new("Fin", [Tense, Person, Number, Mood]),
        new("Inf", []),
        new("Part", [Tense, Case, Gender, Number, Degree]),
    ];

    /// <summary>
    /// Which morphology fields apply per part of speech (UD tag). For verbs the top-level fields hold only the
    /// verb form; the rest depends on the form (<see cref="ApplicabilityRule.ByVerbForm"/>).
    /// </summary>
    public static readonly IReadOnlyList<ApplicabilityRule> Rules =
    [
        new("VERB", [VerbForm], VerbFormRules),
        new("AUX", [VerbForm], VerbFormRules),
        new("NOUN", [Number, Case, Gender]),
        new("PROPN", [Number, Case, Gender]),
        new("ADJ", [Degree, Number, Case, Gender]),
        new("ADV", [Degree]),
        new("DET", [Number, Case, Gender]),
        new("PRON", [Person, Number, Case, Gender]),
        new("ADP", []),
        new("CCONJ", []),
        new("SCONJ", []),
        new("INTJ", []),
        new("NUM", []),
        new("PART", []),
        new("X", []),
    ];


    public const string PartOfSpeech = "partOfSpeech";
    public const string Degree = "degree";
    public const string VerbForm = "verbForm";
    public const string Tense = "tense";
    public const string Person = "person";
    public const string Number = "number";
    public const string Mood = "mood";
    public const string Case = "case";
    public const string Gender = "gender";
    public const string Stts = "stts";

    public static readonly IReadOnlyList<FieldValueMap> All =
    [
        new(PartOfSpeech,
        [
            new("ADJ", "adjektiv", "adjective"),
            new("ADP", "präposition", "adposition"),
            new("ADV", "adverb", "adverb"),
            new("AUX", "hilfsverb", "auxiliary verb"),
            new("CCONJ", "nebenordnende konjunktion", "coordinating conjunction"),
            new("DET", "artikel/determinativ", "determiner"),
            new("INTJ", "interjektion", "interjection"),
            new("NOUN", "substantiv", "noun"),
            new("NUM", "zahlwort", "numeral"),
            new("PART", "partikel", "particle"),
            new("PRON", "pronomen", "pronoun"),
            new("PROPN", "eigenname", "proper noun"),
            new("SCONJ", "unterordnende konjunktion", "subordinating conjunction"),
            new("VERB", "verb", "verb"),
            new("X", "sonstiges", "other"),
        ]),
        new(Degree,
        [
            new("Pos", "Positiv", "Positive"),
            new("Cmp", "Komparativ", "Comparative"),
            new("Sup", "Superlativ", "Superlative"),
        ]),
        new(VerbForm,
        [
            new("Fin", "finit", "finite"),
            new("Inf", "Infinitiv", "infinitive"),
            new("Part", "Partizip", "participle"),
        ]),
        new(Tense,
        [
            new("Pres", "Präsens", "present"),
            new("Past", "Präteritum", "past"),
            new("Perf", "Perfekt", "perfect"),
            new("Pqp", "Plusquamperfekt", "pluperfect"),
            new("Fut", "Futur I", "future"),
            new("FutPerf", "Futur II", "future perfect"),
        ]),
        new(Person,
        [
            new("1", "1. Person", "1st person"),
            new("2", "2. Person", "2nd person"),
            new("3", "3. Person", "3rd person"),
        ]),
        new(Number,
        [
            new("S", "Singular", "singular"),
            new("P", "Plural", "plural"),
        ]),
        new(Mood,
        [
            new("Ind", "Indikativ", "indicative"),
            new("Sub", "Konjunktiv", "subjunctive"),
            new("Imp", "Imperativ", "imperative"),
        ]),
        new(Case,
        [
            new("Nom", "Nominativ", "nominative"),
            new("Gen", "Genitiv", "genitive"),
            new("Dat", "Dativ", "dative"),
            new("Acc", "Akkusativ", "accusative"),
        ]),
        new(Gender,
        [
            new("M", "maskulin", "masculine"),
            new("F", "feminin", "feminine"),
            new("N", "neutrum", "neuter"),
        ]),
        new(Stts,
        [
            new("ADJA", "attributives Adjektiv", "attributive adjective"),
            new("ADJD", "adverbiales/prädikatives Adjektiv", "adverbial or predicative adjective"),
            new("ADV", "Adverb", "adverb"),
            new("APPO", "Postposition", "postposition"),
            new("APPR", "Präposition", "preposition"),
            new("APPRART", "Präposition mit Artikel", "preposition with article"),
            new("APZR", "Zirkumposition rechts", "right part of circumposition"),
            new("ART", "Artikel", "article"),
            new("CARD", "Kardinalzahl", "cardinal number"),
            new("FM", "Fremdsprachliches Material", "foreign-language material"),
            new("ITJ", "Interjektion", "interjection"),
            new("KOKOM", "Vergleichskonjunktion", "comparative conjunction"),
            new("KON", "nebenordnende Konjunktion", "coordinating conjunction"),
            new("KOUI", "unterordnende Konjunktion mit zu-Infinitiv", "subordinating conjunction with zu-infinitive"),
            new("KOUS", "unterordnende Konjunktion mit Satz", "subordinating conjunction with clause"),
            new("NE", "Eigenname", "proper noun"),
            new("NN", "normales Nomen", "common noun"),
            new("PDAT", "attributives Demonstrativpronomen", "attributive demonstrative pronoun"),
            new("PDS", "substituierendes Demonstrativpronomen", "substituting demonstrative pronoun"),
            new("PIAT", "attributives Indefinitpronomen", "attributive indefinite pronoun"),
            new("PIS", "substituierendes Indefinitpronomen", "substituting indefinite pronoun"),
            new("PPER", "irreflexives Personalpronomen", "irreflexive personal pronoun"),
            new("PPOSAT", "attributives Possessivpronomen", "attributive possessive pronoun"),
            new("PRF", "reflexives Personalpronomen", "reflexive personal pronoun"),
            new("PROAV", "Pronominaladverb", "pronominal adverb"),
            new("PTKA", "Partikel bei Adjektiv oder Adverb", "particle with adjective or adverb"),
            new("PTKANT", "Antwortpartikel", "answer particle"),
            new("PTKNEG", "Negationspartikel", "negation particle"),
            new("PTKVZ", "abgetrennter Verbzusatz", "separated verb particle"),
            new("PWAT", "attributives Interrogativpronomen", "attributive interrogative pronoun"),
            new("PWAV", "adverbiales Interrogativ-/Relativpronomen", "adverbial interrogative or relative pronoun"),
            new("PWS", "substituierendes Interrogativpronomen", "substituting interrogative pronoun"),
            new("VAFIN", "finites Hilfsverb", "finite auxiliary verb"),
            new("VAINF", "Infinitiv Hilfsverb", "auxiliary verb infinitive"),
            new("VAPP", "Partizip Perfekt Hilfsverb", "auxiliary verb past participle"),
            new("VMFIN", "finites Modalverb", "finite modal verb"),
            new("VMPP", "Partizip Perfekt Modalverb", "modal verb past participle"),
            new("VVFIN", "finites Vollverb", "finite full verb"),
            new("VVIMP", "Imperativ Vollverb", "full verb imperative"),
            new("VVINF", "Infinitiv Vollverb", "full verb infinitive"),
            new("VVIZU", "Infinitiv mit zu", "infinitive with zu"),
            new("VVPP", "Partizip Perfekt Vollverb", "full verb past participle"),
            new("XY", "Nichtwort / Sonderzeichen", "non-word or special character"),
        ]),
    ];

    public static FieldValueMap Get(string field) => All.First(f => f.Field == field);

    public static bool IsValid(string field, string? code) => code is not null && Get(field).Contains(code);
}
