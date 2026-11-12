namespace UnshackledWord.Domain.Models.DataQuality;

/// <summary>
/// Which original-word classes a German part of speech (UD tag) may legitimately be aligned with, per language.
/// Greek classes are the STEP morphology prefixes (N V A T P R D ADV CONJ PREP PRT COND INJ …), Hebrew classes the
/// grammar letter of the main morpheme (N V A R C D P T S).
/// </summary>
public static class PosClassRules
{
    public const string Greek = "G";
    public const string Hebrew = "H";

    public static readonly string[] ContentGermanPos = ["NOUN", "PROPN", "VERB", "ADJ"];

    /// <summary>Hebrew prefixes and suffixes make German function words map to the host word: skip those in PoS/minority checks.</summary>
    public static readonly string[] HebrewFunctionWordPos = ["DET", "ADP", "CCONJ", "SCONJ", "PRON", "AUX", "PART"];

    public static readonly string[] GreekContentClasses = ["N", "V", "A", "ADV"];
    public static readonly string[] HebrewContentClasses = ["N", "V", "A"];

    /// <summary>Greek classes of function words (article, conjunction, preposition, particle, conditional).</summary>
    public static readonly string[] GreekFunctionClasses = ["T", "CONJ", "COND", "PREP", "PRT"];

    public static readonly string[] HebrewFunctionClasses = ["C", "R", "T"];

    private static readonly Dictionary<string, string[]> GreekExpected = new()
    {
        ["NOUN"] = ["N", "A"],
        ["PROPN"] = ["N", "A"],
        ["VERB"] = ["V"],
        ["AUX"] = ["V"],
        ["ADJ"] = ["A", "N"],
        ["ADV"] = ["ADV", "A", "PRT", "CONJ"],
        ["DET"] = ["T", "D", "P", "R", "S", "X", "K", "I", "Q", "A"],
        ["PRON"] = ["P", "R", "D", "F", "S", "X", "I", "Q", "K", "C", "T"],
        ["ADP"] = ["PREP", "ADV", "CONJ"],
        ["CCONJ"] = ["CONJ", "PRT", "COND", "ADV"],
        ["SCONJ"] = ["CONJ", "COND", "R", "ADV", "PRT", "I"],
        ["PART"] = ["PRT", "ADV", "CONJ", "COND", "INJ", "T"],
        ["NUM"] = ["A", "N", "ADV"],
        ["INTJ"] = ["INJ", "PRT", "ADV", "N"],
    };

    private static readonly Dictionary<string, string[]> HebrewExpected = new()
    {
        ["NOUN"] = ["N", "A"],
        ["PROPN"] = ["N", "A"],
        ["VERB"] = ["V"],
        ["AUX"] = ["V"],
        ["ADJ"] = ["A", "N", "V"],
        ["ADV"] = ["D", "T", "N", "A", "R"],
        ["DET"] = ["T", "P", "D", "N", "A"],
        ["PRON"] = ["P", "S", "D", "T", "N"],
        ["ADP"] = ["R", "N", "D", "T"],
        ["CCONJ"] = ["C", "T", "D"],
        ["SCONJ"] = ["C", "T", "R", "D", "P"],
        ["PART"] = ["T", "D", "C", "R", "N"],
        ["NUM"] = ["N", "A"],
        ["INTJ"] = ["T", "D", "N"],
    };

    /// <summary>Pos values that are never compared (tagger's catch-all).</summary>
    public static bool IsExempt(string? pos) => string.IsNullOrEmpty(pos) || pos == "X";

    public static bool IsContentGermanPos(string? pos) => pos is not null && ContentGermanPos.Contains(pos);

    public static bool IsHebrewFunctionWord(string? lang, string? pos) =>
        lang == Hebrew && pos is not null && HebrewFunctionWordPos.Contains(pos);

    public static bool IsContentClass(string? lang, string? cls) =>
        cls is not null && (lang == Greek ? GreekContentClasses : HebrewContentClasses).Contains(cls);

    public static bool IsFunctionClass(string? lang, string? cls) =>
        cls is not null && (lang == Greek ? GreekFunctionClasses : HebrewFunctionClasses).Contains(cls);

    /// <summary>
    /// True when the pair is plausible, or not comparable (exempt PoS, Hebrew function word, missing class).
    /// </summary>
    public static bool IsExpected(string? lang, string? pos, string? cls)
    {
        if (IsExempt(pos) || string.IsNullOrEmpty(cls) || IsHebrewFunctionWord(lang, pos)) return true;
        var table = lang == Greek ? GreekExpected : HebrewExpected;
        return table.TryGetValue(pos!, out var classes) && classes.Contains(cls);
    }

    /// <summary>All plausible "lang|pos|class" triples, for SQL <c>= ANY(@expected)</c>.</summary>
    public static string[] ExpectedKeys() =>
    [
        .. GreekExpected.SelectMany(p => p.Value.Select(c => $"{Greek}|{p.Key}|{c}")),
        .. HebrewExpected.SelectMany(p => p.Value.Select(c => $"{Hebrew}|{p.Key}|{c}")),
    ];

    /// <summary>
    /// M4: a German content word aligned to a function class (article, conjunction, preposition, particle), or, NT only,
    /// a German article/preposition aligned to a Greek noun or verb. Hebrew function words are never compared.
    /// </summary>
    public static bool IsContentFunctionMismatch(string? lang, string? pos, string? cls)
    {
        if (IsExempt(pos) || string.IsNullOrEmpty(cls) || IsHebrewFunctionWord(lang, pos)) return false;
        if (IsContentGermanPos(pos) && IsFunctionClass(lang, cls)) return true;
        return lang == Greek && pos is "DET" or "ADP" && cls is "N" or "V";
    }

    /// <summary>All "lang|pos|class" triples that are M4 findings, for SQL <c>= ANY(@keys)</c>.</summary>
    public static string[] ContentFunctionKeys() =>
    [
        .. ContentGermanPos.SelectMany(p => GreekFunctionClasses.Select(c => $"{Greek}|{p}|{c}")),
        .. ContentGermanPos.SelectMany(p => HebrewFunctionClasses.Select(c => $"{Hebrew}|{p}|{c}")),
        .. new[] { "DET", "ADP" }.SelectMany(p => new[] { "N", "V" }.Select(c => $"{Greek}|{p}|{c}")),
    ];

    /// <summary>L4: pairs of PoS that legitimately share a lemma (both orders).</summary>
    public static string[] AllowedPosPairs() =>
    [
        "DET|PRON", "PRON|DET", "ADJ|ADV", "ADV|ADJ", "VERB|AUX", "AUX|VERB",
    ];

    /// <summary>Proper-noun flag: a German PROPN against a non-proper original word, or the reverse for content nouns.</summary>
    public static bool IsProperNounConflict(string? pos, bool properNoun) =>
        pos == "PROPN" ? !properNoun : pos == "NOUN" && properNoun;
}
