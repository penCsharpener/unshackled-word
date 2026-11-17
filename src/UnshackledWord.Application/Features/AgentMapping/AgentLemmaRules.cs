using System.Text.RegularExpressions;

namespace UnshackledWord.Application.Features.AgentMapping;

/// <summary>Pure lemma checks of 05-lemmatisation.md and 06-validation.md ("Lemmas"), usable without a database.</summary>
public static class AgentLemmaRules
{
    public const int MaxLemmaLength = 40;

    /// <summary>Multi-word lemmas that may contain a space ("listed in the instructions"); none yet.</summary>
    public static readonly IReadOnlySet<string> MultiWordLemmas = new HashSet<string>(StringComparer.Ordinal);

    private static readonly Regex WellFormed = new(@"^[\p{L}\p{M}0-9]+(?:-[\p{L}\p{M}0-9]+)*$", RegexOptions.Compiled);

    /// <summary>Common irregular verb forms (form, lemma) of modern German and Elberfelder 1871 that share no 2-letter prefix with their lemma.</summary>
    private static readonly HashSet<(string Form, string Lemma)> Irregular = BuildIrregular();

    /// <summary>Null when the lemma is acceptable, else the reason: not empty, no whitespace (except listed multi-word lemmas), no punctuation, at most 40 characters.</summary>
    public static string? CheckWellFormed(string? lemma)
    {
        if (string.IsNullOrWhiteSpace(lemma))
        {
            return "lemma is empty.";
        }

        if (lemma.Length > MaxLemmaLength)
        {
            return $"lemma '{lemma}' is longer than {MaxLemmaLength} characters.";
        }

        if (MultiWordLemmas.Contains(lemma))
        {
            return null;
        }

        if (lemma.Any(char.IsWhiteSpace))
        {
            return $"lemma '{lemma}' contains whitespace.";
        }

        return WellFormed.IsMatch(lemma) ? null : $"lemma '{lemma}' contains punctuation or other characters that are not letters, digits or an inner hyphen.";
    }

    public static bool IsVerb(string? stts, string? pos) =>
        (stts is not null && stts.StartsWith('V')) || pos is "VERB" or "AUX";

    public static bool IsNounTag(string? stts) => stts is "NN" or "NE";

    /// <summary>Hard rule: nouns and names start with a capital letter, other words do not (unless a name: part of speech <c>PROPN</c>).</summary>
    public static string? CheckCapitalisation(string lemma, string? stts, string? pos)
    {
        var upper = char.IsUpper(lemma[0]);
        if (IsNounTag(stts))
        {
            return upper ? null : $"lemma '{lemma}' of a noun ({stts}) must start with a capital letter.";
        }

        return upper && pos != "PROPN" && stts is not null ? $"lemma '{lemma}' is capitalised but the word is not a noun or name ({stts})." : null;
    }

    public static int CommonPrefix(string a, string b)
    {
        var n = 0;
        while (n < a.Length && n < b.Length && char.ToLowerInvariant(a[n]) == char.ToLowerInvariant(b[n]))
        {
            n++;
        }

        return n;
    }

    /// <summary>Soft rule: a verb form and its lemma share a prefix of two or more letters, or the pair is a known irregular form.</summary>
    public static bool FormFitsLemma(string form, string lemma) =>
        CommonPrefix(form, lemma) >= 2 || Irregular.Contains((form.ToLowerInvariant(), lemma.ToLowerInvariant()));

    /// <summary>Soft rule: a verb lemma is an infinitive (ends in -en, -eln, -ern, -n; all end in <c>n</c>).</summary>
    public static bool LooksLikeInfinitive(string lemma) => lemma.EndsWith('n');

    /// <summary>How many tokens left and right of a word count as its neighbours for the particle rule.</summary>
    public const int ParticleNeighbourhood = 2;

    /// <summary>1871 spellings (lower case) that are never accepted as lemmas, with the modern headword (03-lemma-rules.md). Extended as reviews find more.</summary>
    public static readonly IReadOnlyDictionary<string, string> LegacySpellings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["thun"] = "tun", ["thue"] = "tun", ["thut"] = "tun", ["that"] = "tun", ["thaten"] = "tun", ["gethan"] = "tun",
        ["seyn"] = "sein", ["seyd"] = "sein",
        ["gieng"] = "gehen", ["giengen"] = "gehen",
        ["ward"] = "werden", ["wardst"] = "werden",
        ["sahe"] = "sehen", ["sahest"] = "sehen",
        ["giebt"] = "geben", ["giebst"] = "geben",
        ["thür"] = "Tür", ["thüre"] = "Tür", ["thor"] = "Tor", ["thal"] = "Tal", ["thier"] = "Tier", ["theil"] = "Teil",
        ["thräne"] = "Träne", ["muth"] = "Mut", ["noth"] = "Not", ["roth"] = "rot", ["rath"] = "Rat", ["gluth"] = "Glut",
        ["armuth"] = "Armut", ["blüthe"] = "Blüte", ["hülfe"] = "Hilfe", ["bey"] = "bei", ["creutz"] = "Kreuz",
    };

    /// <summary>Contractions of preposition and article and their preposition headword.</summary>
    public static readonly IReadOnlyDictionary<string, string> Contractions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["zum"] = "zu", ["zur"] = "zu", ["im"] = "in", ["ins"] = "in", ["vom"] = "von", ["am"] = "an", ["ans"] = "an",
        ["beim"] = "bei", ["aufs"] = "auf", ["fürs"] = "für", ["ums"] = "um", ["durchs"] = "durch", ["übers"] = "über",
        ["unterm"] = "unter", ["überm"] = "über", ["vorm"] = "vor", ["hinterm"] = "hinter",
    };

    /// <summary>Oblique and plural forms of the personal pronoun and their nominative headword.</summary>
    public static readonly IReadOnlyDictionary<string, string> PersonalPronouns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["mir"] = "ich", ["mich"] = "ich", ["dir"] = "du", ["dich"] = "du", ["ihm"] = "er", ["ihn"] = "er",
        ["ihnen"] = "sie", ["uns"] = "wir", ["euch"] = "ihr",
    };

    /// <summary>
    /// Forms (lower case) of the homograph list of 03-lemma-rules.md, where the model decides tag and lemma together: <c>sein</c> (verb vs.
    /// possessive), <c>Weg</c>/<c>weg</c>, <c>Arm</c>/<c>arm</c>, <c>da</c>, <c>als</c>, <c>daß</c>/<c>das</c>, <c>ward</c>/<c>wart</c>,
    /// <c>Gott</c>/<c>Götter</c>. <c>das</c> counts only when it is not tagged as article (see <see cref="IsHomograph"/>).
    /// </summary>
    public static readonly IReadOnlySet<string> HomographForms = new HashSet<string>(StringComparer.Ordinal)
    {
        "sein", "weg", "wege", "arm", "arme", "armen", "armer", "da", "als", "daß", "dass", "das", "ward", "wart", "gott", "götter",
    };

    /// <summary>
    /// True for a form of <see cref="HomographForms"/>. The article <c>das</c> (STTS <c>ART</c>) is left out: it is in nearly every verse and
    /// only the relative/demonstrative/conjunction <c>das</c> competes with <c>daß</c>.
    /// </summary>
    public static bool IsHomograph(string form, string? stts)
    {
        var key = form.Trim().ToLowerInvariant();
        return HomographForms.Contains(key) && !(key == "das" && stts == "ART");
    }

    /// <summary>Headword of a contraction (<c>zum</c> → <c>zu</c>), null when the form is none.</summary>
    public static string? ContractionHeadword(string form) => Contractions.GetValueOrDefault(form);

    /// <summary>Nominative headword of an oblique personal pronoun (<c>ihm</c> → <c>er</c>, <c>uns</c> → <c>wir</c>), null when the form is none.</summary>
    public static string? PersonalPronounHeadword(string form) => PersonalPronouns.GetValueOrDefault(form);

    /// <summary>Null when the lemma is no known 1871 spelling, else the reason with the modern headword.</summary>
    public static string? CheckLegacySpelling(string lemma) =>
        LegacySpellings.TryGetValue(lemma, out var modern) ? $"lemma '{lemma}' is an 1871 spelling, use the modern headword '{modern}'." : null;

    /// <summary>Null when a contraction (STTS <c>APPRART</c>) carries its preposition as lemma; other tags and non-contractions are not checked.</summary>
    public static string? CheckContraction(string form, string lemma, string? stts)
    {
        if (stts != "APPRART" || ContractionHeadword(form) is not { } headword)
        {
            return null;
        }

        return string.Equals(lemma, headword, StringComparison.Ordinal) ? null : $"lemma of the contraction '{form}' must be the preposition '{headword}', got '{lemma}'.";
    }

    /// <summary>Finite verb forms (STTS <c>VVFIN</c>, <c>VAFIN</c>, <c>VMFIN</c>, imperatives), the only ones that absorb a separable particle.</summary>
    public static bool IsFiniteVerb(string? stts) => stts is not null && stts.StartsWith('V') && (stts.EndsWith("FIN", StringComparison.Ordinal) || stts.EndsWith("IMP", StringComparison.Ordinal));

    /// <summary>
    /// Rule for a <c>PTKVZ</c> token (0018): its lemma is its own text in lower case, and a finite verb of the same verse
    /// carries a lemma that starts with the particle and is longer (<c>auf</c> + <c>aufstehen</c>). Null when fine.
    /// <paramref name="verbs"/> are the lemmas and STTS tags of the other words of the verse.
    /// </summary>
    public static string? CheckParticle(string form, string lemma, IEnumerable<(string? Lemma, string? Stts)> verbs)
    {
        var expected = form.ToLowerInvariant();
        if (!string.Equals(lemma, expected, StringComparison.Ordinal))
        {
            return $"separable particle '{form}' keeps itself as lemma ('{expected}'), got '{lemma}'; the finite verb carries particle and verb ('aufstehen')";
        }

        var partner = verbs.Any(v => IsFiniteVerb(v.Stts) && v.Lemma is { } l && l.Length > expected.Length && l.StartsWith(expected, StringComparison.OrdinalIgnoreCase));
        return partner ? null : $"separable particle '{form}' needs a finite verb in this verse whose lemma starts with '{expected}' (like '{expected}gehen')";
    }

    /// <summary>
    /// Rule of 0018: the lemma of a non-verb never ends with the particle of a neighbouring <c>PTKVZ</c> (no <c>Himmelauf</c> for a noun followed by <c>auf</c>).
    /// Null for verbs and when the lemma equals the particle. <paramref name="neighbourParticles"/> are the lower-case forms of PTKVZ tokens within <see cref="ParticleNeighbourhood"/>.
    /// </summary>
    public static string? CheckAbsorbedParticle(string lemma, string? stts, string? pos, IEnumerable<string> neighbourParticles)
    {
        if (IsVerb(stts, pos) || stts == "PTKVZ")
        {
            return null;
        }

        foreach (var particle in neighbourParticles)
        {
            if (lemma.Length > particle.Length && lemma.EndsWith(particle, StringComparison.OrdinalIgnoreCase))
            {
                return $"lemma '{lemma}' ends with the neighbouring particle '{particle}'; only a finite separable verb absorbs its particle";
            }
        }

        return null;
    }

    private static HashSet<(string, string)> BuildIrregular()
    {
        var pairs = new HashSet<(string, string)>();
        void Add(string lemma, params string[] forms)
        {
            foreach (var form in forms)
            {
                pairs.Add((form, lemma));
            }
        }

        Add("sein", "bin", "bist", "ist", "sind", "seid", "war", "warst", "waren", "wart", "wäre", "wären", "gewesen", "sei", "seien", "wesen");
        Add("werden", "ward", "wurde", "wurden", "wurdest", "wirst", "wird", "geworden", "worden", "würde", "würden");
        Add("haben", "hat", "hast", "hatte", "hatten", "hattest", "hätte", "hätten", "gehabt");
        Add("gehen", "ging", "gingen", "gegangen", "ginget", "gehe");
        Add("kommen", "kam", "kamen", "kamst", "gekommen");
        Add("nehmen", "nahm", "nahmen", "nimmt", "nimm", "genommen");
        Add("geben", "gab", "gaben", "gibt", "gib", "gegeben");
        Add("sehen", "sah", "sahen", "sieht", "siehe", "gesehen");
        Add("stehen", "stand", "standen", "gestanden");
        Add("lassen", "ließ", "ließen", "lässt", "gelassen");
        Add("finden", "fand", "fanden", "gefunden");
        Add("fallen", "fiel", "fielen", "gefallen");
        Add("halten", "hielt", "hielten", "gehalten");
        Add("laufen", "lief", "liefen", "gelaufen");
        Add("rufen", "rief", "riefen", "gerufen");
        Add("bitten", "bat", "baten", "gebeten");
        Add("tun", "tat", "taten", "getan", "thun", "that", "thaten", "gethan", "thut");
        Add("sitzen", "saß", "saßen", "gesessen");
        Add("liegen", "lag", "lagen", "gelegen");
        Add("bringen", "brachte", "brachten", "gebracht");
        Add("denken", "dachte", "dachten", "gedacht");
        Add("wissen", "wusste", "wussten", "weiß", "wisst", "gewusst");
        Add("können", "kann", "kannst", "konnte", "konnten", "gekonnt");
        Add("müssen", "muss", "musste", "mussten");
        Add("dürfen", "darf", "durfte", "durften");
        Add("wollen", "will", "wollte", "wollten", "willst");
        Add("sollen", "soll", "sollte", "sollten");
        Add("mögen", "mag", "mochte", "mochten");
        Add("treten", "trat", "traten", "getreten");
        Add("essen", "aß", "aßen", "isst", "gegessen");
        Add("trinken", "trank", "tranken", "getrunken");
        Add("fahren", "fuhr", "fuhren", "gefahren");
        Add("schlagen", "schlug", "schlugen", "geschlagen");
        Add("sprechen", "sprach", "sprachen", "spricht");
        Add("heißen", "hieß", "hießen");
        Add("stoßen", "stieß", "stießen");
        Add("gelten", "galt", "galten");
        Add("bleiben", "blieb", "blieben");
        Add("beginnen", "begann", "begannen");
        Add("verlieren", "verlor", "verloren");
        Add("ziehen", "zog", "zogen", "gezogen");
        Add("fliehen", "floh", "flohen");
        Add("bieten", "bot", "boten");
        Add("schließen", "schloss", "schlossen");
        Add("tragen", "trug", "trugen", "trägt");
        Add("graben", "grub", "gruben");
        Add("rennen", "rannte", "rannten");
        Add("brennen", "brannte", "brannten");
        Add("senden", "sandte", "sandten");
        Add("kennen", "kannte", "kannten");
        Add("nennen", "nannte", "nannten");
        Add("wenden", "wandte", "wandten");
        return pairs;
    }
}
