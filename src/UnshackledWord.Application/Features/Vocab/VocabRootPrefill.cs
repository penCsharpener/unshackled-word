using UnshackledWord.Domain.Models.Vocab;

namespace UnshackledWord.Application.Features.Vocab;

/// <summary>A Greek entry to split: its main key and the Abbott-Smith lexicon text of that key.</summary>
public sealed record VocabRootGreekEntry(string MainKey, string? Lexicon);

/// <summary>One lexicon lemma a root can resolve to (<c>InVocab</c>: the key belongs to a vocab entry, whose lemma wins as display).</summary>
public sealed record VocabRootLexiconLemma(string Key, string Normalized, string Display, bool InVocab);

/// <summary>A Hebrew or Aramaic verb entry (<c>Pos</c> H:V / A:V): its main key and lemma.</summary>
public sealed record VocabRootHebrewVerb(string MainKey, string Lemma);

/// <summary>A resolved Greek compound: <c>ἐκπορεύομαι</c> becomes prefixes <c>ἐκ</c> and root <c>πορεύομαι</c> (G4198).</summary>
public sealed record VocabRootGreekSplit(string MainKey, string RootKey, string RootLemma, string Prefixes, string[] PrefixKeys);

/// <summary>A Greek entry that has a hyphenated headword but could not be resolved to exactly one key per part.</summary>
public sealed record VocabRootUnresolved(string MainKey, string Headword, string Reason);

/// <summary>A Hebrew verb as its own root; <c>Homograph</c> counts verbs with the same consonants in key order.</summary>
public sealed record VocabRootHebrewRoot(string MainKey, string Root, short Homograph);

public sealed record VocabRootGreekResult(IReadOnlyList<VocabRootGreekSplit> Splits, IReadOnlyList<VocabRootUnresolved> Unresolved);

/// <summary>
/// Deterministic root prefill (roadmap 0030.2): Greek compounds from the hyphenated Abbott-Smith headword and a closed table of the
/// proper prepositions in their elided and assimilated forms, Hebrew/Aramaic verbs as their own consonant root. Pure logic: the caller
/// loads the entries and the lexicon and writes the result. A row exists only when every part resolves to exactly one key.
/// </summary>
public static class VocabRootPrefill
{
    private sealed record Preposition(string Key, string Dictionary);

    private static readonly Preposition Ana = new("G0303", "ἀνά");
    private static readonly Preposition Anti = new("G0473", "ἀντί");
    private static readonly Preposition Apo = new("G0575", "ἀπό");
    private static readonly Preposition Dia = new("G1223", "διά");
    private static readonly Preposition Eis = new("G1519", "εἰς");
    private static readonly Preposition Ek = new("G1537", "ἐκ");
    private static readonly Preposition En = new("G1722", "ἐν");
    private static readonly Preposition Epi = new("G1909", "ἐπί");
    private static readonly Preposition Kata = new("G2596", "κατά");
    private static readonly Preposition Meta = new("G3326", "μετά");
    private static readonly Preposition Para = new("G3844", "παρά");
    private static readonly Preposition Peri = new("G4012", "περί");
    private static readonly Preposition Pro = new("G4253", "πρό");
    private static readonly Preposition Pros = new("G4314", "πρός");
    private static readonly Preposition Syn = new("G4862", "σύν");
    private static readonly Preposition Hyper = new("G5228", "ὑπέρ");
    private static readonly Preposition Hypo = new("G5259G", "ὑπό");

    /// <summary>
    /// Normalized prefix form (accents dropped, see <see cref="VocabText.Normalize"/>) to preposition. The 18th proper preposition,
    /// ἀμφί, has no Strong's number, so a compound with it cannot resolve and goes to the AI run.
    /// </summary>
    private static readonly Dictionary<string, Preposition> Prepositions = new(StringComparer.Ordinal)
    {
        ["ανα"] = Ana, ["αν"] = Ana,
        ["αντι"] = Anti, ["αντ"] = Anti, ["ανθ"] = Anti,
        ["απο"] = Apo, ["απ"] = Apo, ["αφ"] = Apo,
        ["δια"] = Dia, ["δι"] = Dia,
        ["εισ"] = Eis,
        ["εκ"] = Ek, ["εξ"] = Ek,
        ["εν"] = En, ["εμ"] = En, ["εγ"] = En, ["ελ"] = En,
        ["επι"] = Epi, ["επ"] = Epi, ["εφ"] = Epi,
        ["κατα"] = Kata, ["κατ"] = Kata, ["καθ"] = Kata,
        ["μετα"] = Meta, ["μετ"] = Meta, ["μεθ"] = Meta,
        ["παρα"] = Para, ["παρ"] = Para,
        ["περι"] = Peri, ["περ"] = Peri,
        ["προ"] = Pro,
        ["προσ"] = Pros,
        ["συν"] = Syn, ["συμ"] = Syn, ["συλ"] = Syn, ["συγ"] = Syn, ["συσ"] = Syn, ["συ"] = Syn,
        ["υπερ"] = Hyper,
        ["υπο"] = Hypo, ["υπ"] = Hypo, ["υφ"] = Hypo,
    };

    /// <summary>The closed preposition list (key and dictionary form) ordered by dictionary form, for the prefix picker of the editor (roadmap 0030.5f).</summary>
    public static IReadOnlyList<(string Key, string Dictionary)> PrepositionList() =>
        Prepositions.Values.Distinct().OrderBy(p => VocabText.Normalize(p.Dictionary), StringComparer.Ordinal).Select(p => (p.Key, p.Dictionary)).ToList();

    /// <summary>True when the key is one of the proper prepositions of the closed table (the only keys a prefix may have, roadmap 0030.4).</summary>
    public static bool IsPrepositionKey(string? key) => key is not null && Prepositions.Values.Any(p => p.Key == key);

    /// <summary>Dictionary form of a preposition key (<c>G4862</c> gives <c>σύν</c>); null for any other key.</summary>
    public static string? PrepositionDictionaryForm(string? key) => key is null ? null : Prepositions.Values.FirstOrDefault(p => p.Key == key)?.Dictionary;

    /// <summary>The normalized written forms of a preposition (<c>συν</c>, <c>συμ</c>, <c>συλ</c> ...) as they appear in front of a root, longest first.</summary>
    public static IReadOnlyList<string> PrepositionForms(string? key) =>
        Prepositions.Where(p => p.Value.Key == key).Select(p => p.Key).OrderByDescending(f => f.Length).ToList();

    /// <summary>
    /// The headword of an Abbott-Smith lexicon text (<c>&lt;b&gt;ἐκ-πορεύω &lt;/b&gt;</c>), up to the first comma, tag or space (a hyphen after a
    /// space belongs to an ending, <c>ἀνθύπατος -ου</c>, or to a variant, <c>ἀπο-τίνω (or -τίω)</c>); null when there is none.
    /// </summary>
    public static string? Headword(string? lexicon)
    {
        if (string.IsNullOrWhiteSpace(lexicon))
        {
            return null;
        }

        var match = System.Text.RegularExpressions.Regex.Match(lexicon, @"^<b>\s*([^<,]*)");

        return match.Success ? match.Groups[1].Value.Trim().Split(' ', 2)[0] : null;
    }

    /// <summary>Resolves the hyphenated Greek headwords; entries without a hyphen are not touched (Abbott-Smith is not consistent, the AI decides).</summary>
    public static VocabRootGreekResult ResolveGreek(IEnumerable<VocabRootGreekEntry> entries, IReadOnlyCollection<VocabRootLexiconLemma> lexicon)
    {
        var byNormalized = lexicon.ToLookup(l => l.Normalized, StringComparer.Ordinal);
        var splits = new List<VocabRootGreekSplit>();
        var unresolved = new List<VocabRootUnresolved>();

        foreach (var entry in entries)
        {
            var headword = Headword(entry.Lexicon);

            if (headword is null || !headword.Contains('-'))
            {
                continue;
            }

            var result = ResolveHeadword(entry.MainKey, headword, byNormalized, out var reason);

            if (result is not null)
            {
                splits.Add(result);
            }
            else
            {
                unresolved.Add(new VocabRootUnresolved(entry.MainKey, headword, reason));
            }
        }

        return new VocabRootGreekResult(splits, unresolved);
    }

    private static VocabRootGreekSplit? ResolveHeadword(string mainKey, string headword, ILookup<string, VocabRootLexiconLemma> lexicon, out string reason)
    {
        var parts = headword.Split('-', StringSplitOptions.TrimEntries);

        if (parts.Length < 2 || parts.Any(p => p.Length == 0))
        {
            reason = "empty part";

            return null;
        }

        var prefixes = new List<Preposition>();

        foreach (var part in parts[..^1])
        {
            if (!Prepositions.TryGetValue(VocabText.Normalize(part), out var preposition))
            {
                reason = $"no preposition: {part}";

                return null;
            }

            prefixes.Add(preposition);
        }

        var root = ResolveRoot(parts[^1], lexicon, out reason);

        if (root is null)
        {
            return null;
        }

        if (root.Key == mainKey)
        {
            reason = "root is the word itself";

            return null;
        }

        reason = string.Empty;

        return new VocabRootGreekSplit(
            mainKey,
            root.Key,
            root.Display,
            string.Join(" + ", prefixes.Select(p => p.Dictionary)),
            prefixes.Select(p => p.Key).ToArray());
    }

    /// <summary>
    /// The lemma part as one key. The middle voice is normalized (πορεύω and πορεύομαι are one verb): all forms are looked up, a
    /// match that is also a vocab entry wins, and more than one key left over leaves the part unresolved.
    /// </summary>
    private static VocabRootLexiconLemma? ResolveRoot(string part, ILookup<string, VocabRootLexiconLemma> lexicon, out string reason)
    {
        var normalized = VocabText.Normalize(part);
        var exact = lexicon[normalized].ToList();
        var variants = MiddleVariants(normalized).SelectMany(v => lexicon[v]).ToList();

        foreach (var candidates in new[] { exact, exact.Concat(variants).ToList() })
        {
            var picked = Pick(candidates);

            if (picked is not null)
            {
                reason = string.Empty;

                return picked;
            }
        }

        var all = exact.Concat(variants).Select(c => c.Key).Distinct().Count();
        reason = all == 0 ? $"root not in lexicon: {part}" : $"root ambiguous: {part}";

        return null;
    }

    private static VocabRootLexiconLemma? Pick(List<VocabRootLexiconLemma> candidates)
    {
        var keys = candidates.GroupBy(c => c.Key).Select(g => g.First()).ToList();

        if (keys.Count == 1)
        {
            return keys[0];
        }

        var inVocab = keys.Where(k => k.InVocab).ToList();

        return inVocab.Count == 1 ? inVocab[0] : null;
    }

    private static IEnumerable<string> MiddleVariants(string normalized)
    {
        if (normalized.EndsWith("ομαι", StringComparison.Ordinal))
        {
            yield return normalized[..^4] + "ω";
        }
        else if (normalized.EndsWith('ω'))
        {
            yield return normalized[..^1] + "ομαι";
        }
        else if (normalized.EndsWith("υμι", StringComparison.Ordinal))
        {
            // δείκνυμι (Abbott-Smith) and δεικνύω (STEP) are one verb
            yield return normalized[..^3] + "υω";
        }
    }

    /// <summary>
    /// The verb as its own root: the lemma's consonants (niqqud and cantillation stripped, final forms kept). Two verbs with the same
    /// letters become homograph 1 and 2 in key order.
    /// </summary>
    public static IReadOnlyList<VocabRootHebrewRoot> ResolveHebrew(IEnumerable<VocabRootHebrewVerb> verbs)
    {
        var result = new List<VocabRootHebrewRoot>();

        foreach (var group in verbs
                     .Select(v => (Verb: v, Root: Consonants(v.Lemma)))
                     .Where(x => x.Root.Length > 0)
                     .GroupBy(x => x.Root, StringComparer.Ordinal))
        {
            short homograph = 1;

            foreach (var item in group.OrderBy(x => x.Verb.MainKey, StringComparer.Ordinal))
            {
                result.Add(new VocabRootHebrewRoot(item.Verb.MainKey, group.Key, homograph++));
            }
        }

        return result;
    }

    /// <summary>Hebrew letters only: niqqud and cantillation are combining marks, anything else (maqaf, spaces) is dropped.</summary>
    public static string Consonants(string lemma) =>
        new(VocabText.Normalize(lemma).Where(c => c is >= 'א' and <= 'ת').ToArray());
}
