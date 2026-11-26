using System.Text.RegularExpressions;
using UnshackledWord.Application.Features.Lxx;
using UnshackledWord.Domain.Extensions;

namespace UnshackledWord.Application.Features.AgentMapping.Lxx;

/// <summary>
/// The Greek TBESG entries by Strong's key (<c>G0746</c>, <c>G1487G</c>, <c>G20969</c>), for the LXX validator and the package candidates. Pure, thread-safe after
/// <see cref="Build"/>.
/// </summary>
public sealed partial class LxxLexicon
{
    private readonly Dictionary<string, List<LxxLexiconEntry>> _byStrongs;

    private LxxLexicon(Dictionary<string, List<LxxLexiconEntry>> byStrongs, IReadOnlyList<LxxLexiconEntry> all)
    {
        _byStrongs = byStrongs;
        All = all;
    }

    public IReadOnlyList<LxxLexiconEntry> All { get; }

    public static LxxLexicon Build(IEnumerable<LxxLexiconEntry> entries)
    {
        // TBESG writes accents as oxia (U+1F71 …); NFC turns them into the tonos the STEP words and the stored tags use.
        var all = entries.Where(e => !string.IsNullOrWhiteSpace(e.Strongs) && !string.IsNullOrWhiteSpace(e.Lemma))
            .Select(e => e with { Lemma = e.Lemma.Trim().Normalize(System.Text.NormalizationForm.FormC) })
            .ToList();
        var byStrongs = all.GroupBy(e => e.Strongs, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        return new LxxLexicon(byStrongs, all);
    }

    public bool Contains(string strongs) => _byStrongs.ContainsKey(strongs);

    public IReadOnlyList<LxxLexiconEntry> Entries(string strongs) => _byStrongs.TryGetValue(strongs, out var list) ? list : [];

    public LxxLexiconEntry? First(string strongs) => _byStrongs.TryGetValue(strongs, out var list) ? list[0] : null;

    /// <summary>
    /// The TBESG lemma of <paramref name="strongs"/> that <paramref name="lemma"/> names, or null. Both sides compare in
    /// <see cref="LxxFormNormalizer"/> form (case, grave/acute and punctuation do not matter); a lemma written as a list (<c>ἔπω, ἐρῶ, εἶπον</c>, as some
    /// STEP lemmas are) matches when one of its parts does.
    /// </summary>
    public string? MatchLemma(string strongs, string? lemma)
    {
        if (string.IsNullOrWhiteSpace(lemma) || !_byStrongs.TryGetValue(strongs, out var entries))
        {
            return null;
        }

        var parts = Parts(lemma);
        return entries.FirstOrDefault(e => Parts(e.Lemma).Any(parts.Contains))?.Lemma;
    }

    /// <summary>
    /// The key format of an LXX tag: <c>G</c>, four or five digits (TBESG numbers LXX-only words from G10000 on), optional capital letter. A model
    /// answer like <c>g746</c> is normalised to <c>G0746</c>; anything else returns null.
    /// </summary>
    public static string? NormalizeStrongs(string? strongs)
    {
        if (string.IsNullOrWhiteSpace(strongs) || StrongsRegex().Match(strongs.Trim()) is not { Success: true } match)
        {
            return null;
        }

        var digits = match.Groups["n"].Value.TrimStart('0');
        if (digits.Length > 5)
        {
            return null;
        }

        return "G" + digits.PadLeft(4, '0') + match.Groups["x"].Value.ToUpperInvariant();
    }

    /// <summary>The comparison form of a lemma: letters and marks only, grave as acute, lower case, NFC.</summary>
    public static string Key(string? lemma) => LxxFormNormalizer.Normalize(lemma);

    /// <summary>Without accents and breathings, final sigma as σ: the stem-matching form (<c>ΑΡΧΗ</c> and <c>ἀρχῇ</c> both give <c>αρχη</c>).</summary>
    public static string Bare(string? word) => (LxxFormNormalizer.Normalize(word).RemoveGreekDiacritics() ?? string.Empty).Replace('ς', 'σ');

    private static HashSet<string> Parts(string lemma) =>
        lemma.Split([',', '/', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Key).Where(k => k.Length > 0).ToHashSet(StringComparer.Ordinal);

    [GeneratedRegex("^[Gg](?<n>[0-9]{1,6})(?<x>[A-Za-z]?)$")]
    private static partial Regex StrongsRegex();
}

/// <summary>
/// TBESG candidates for an LXX word (roadmap 0028.7), at most <see cref="MaxCandidates"/>, cheap and deterministic:
/// <list type="number">
/// <item>NT forms: the Strong's numbers the same running-text form carries in the NT (<see cref="LxxFormNormalizer"/>), most frequent first;</item>
/// <item>the same without accents and breathings (Swete's unaccented capitals <c>ΑΡΧΗ</c>, a different accent);</item>
/// <item>NT stem: NT forms (without accents) sharing the longest prefix of at least <see cref="MinNtStem"/> letters with the form, e.g.
/// <c>ἐλάβοσαν</c> → <c>ἔλαβον</c> (λαμβάνω), <c>κατῴκησαν</c> → <c>κατῴκησεν</c> (κατοικέω): augments and stem changes are already inflected there;</item>
/// <item>lemma stem: TBESG lemmas sharing the longest prefix (at least <see cref="MinStem"/> letters, without accents) with the form, and with the form
/// minus a leading augment <c>ε</c>; ties by NT frequency of the Strong's, then shorter lemma.</item>
/// </list>
/// Only Strong's numbers TBESG has are offered. One sorted array and two dictionaries; no per-word database call.
/// </summary>
public sealed class LxxCandidateIndex
{
    public const int MaxCandidates = 5;
    public const int MinStem = 3;
    public const int MinNtStem = 4;
    private const int MaxStem = 10;

    private readonly LxxLexicon _lexicon;
    private readonly Dictionary<string, List<(string Strongs, int Count)>> _exact;
    private readonly Dictionary<string, List<(string Strongs, int Count)>> _bare;
    private readonly Dictionary<string, int> _frequency;
    private readonly (string Bare, LxxLexiconEntry Entry)[] _stems;
    private readonly (string Bare, string Strongs, int Count)[] _ntStems;

    public LxxCandidateIndex(LxxLexicon lexicon, IEnumerable<LxxNtFormCount> ntForms)
    {
        _lexicon = lexicon;
        var forms = ntForms.Where(f => lexicon.Contains(f.Strongs)).ToList();
        _exact = Group(forms, f => LxxFormNormalizer.Normalize(f.Greek));
        _bare = Group(forms, f => LxxLexicon.Bare(f.Greek));
        _frequency = forms.GroupBy(f => f.Strongs, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Sum(f => f.Count), StringComparer.Ordinal);
        _ntStems = forms
            .Select(f => (Bare: LxxLexicon.Bare(f.Greek), f.Strongs, f.Count))
            .Where(x => x.Bare.Length > 0)
            .GroupBy(x => (x.Bare, x.Strongs))
            .Select(g => (g.Key.Bare, g.Key.Strongs, Count: g.Sum(x => x.Count)))
            .OrderBy(x => x.Bare, StringComparer.Ordinal)
            .ToArray();
        _stems = lexicon.All
            .Select(e => (Bare: LxxLexicon.Bare(e.Lemma.Split(',')[0]), Entry: e))
            .Where(x => x.Bare.Length > 0)
            .OrderBy(x => x.Bare, StringComparer.Ordinal)
            .ToArray();
    }

    public LxxLexicon Lexicon => _lexicon;

    public IReadOnlyList<LxxLexiconEntry> Candidates(string greek)
    {
        var result = new List<LxxLexiconEntry>(MaxCandidates);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Add(string strongs)
        {
            if (result.Count < MaxCandidates && seen.Add(strongs) && _lexicon.First(strongs) is { } entry)
            {
                result.Add(entry);
            }
        }

        foreach (var (strongs, _) in _exact.GetValueOrDefault(LxxFormNormalizer.Normalize(greek)) ?? [])
        {
            Add(strongs);
        }

        var bare = LxxLexicon.Bare(greek);
        foreach (var (strongs, _) in _bare.GetValueOrDefault(bare) ?? [])
        {
            Add(strongs);
        }

        AddNtStems(bare, Add);
        AddStems(bare, result, seen);
        if (bare.Length > MinStem + 1 && bare[0] == 'ε')
        {
            AddStems(bare[1..], result, seen);
        }

        return result;
    }

    private void AddNtStems(string bare, Action<string> add)
    {
        for (var length = Math.Min(bare.Length, 2 * MaxStem); length >= MinNtStem; length--)
        {
            var hits = Range(_ntStems, x => x.Bare, bare[..length]);
            if (hits.Count == 0)
            {
                continue;
            }

            foreach (var strongs in hits.GroupBy(h => h.Strongs, StringComparer.Ordinal)
                         .OrderByDescending(g => g.Sum(h => h.Count))
                         .ThenBy(g => g.Key, StringComparer.Ordinal)
                         .Select(g => g.Key))
            {
                add(strongs);
            }

            return;
        }
    }

    private void AddStems(string bare, List<LxxLexiconEntry> result, HashSet<string> seen)
    {
        if (result.Count >= MaxCandidates || bare.Length == 0)
        {
            return;
        }

        // A short word (καί, ἐν) matches only a lemma equal to it; a longer one the longest shared prefix of MinStem letters or more.
        var longest = Math.Min(bare.Length, MaxStem);
        var shortest = bare.Length < MinStem ? bare.Length : MinStem;
        for (var length = longest; length >= shortest; length--)
        {
            var prefix = bare[..length];
            var hits = Range(_stems, x => x.Bare, prefix);
            if (bare.Length < MinStem)
            {
                hits = hits.Where(h => h.Bare == bare).ToList();
            }

            if (hits.Count == 0)
            {
                continue;
            }

            foreach (var hit in hits
                         .OrderByDescending(h => _frequency.GetValueOrDefault(h.Entry.Strongs))
                         .ThenBy(h => h.Bare.Length)
                         .ThenBy(h => h.Entry.Strongs, StringComparer.Ordinal))
            {
                if (result.Count >= MaxCandidates)
                {
                    return;
                }

                if (seen.Add(hit.Entry.Strongs))
                {
                    result.Add(hit.Entry);
                }
            }

            return;
        }
    }

    /// <summary>The entries of an array sorted by <paramref name="key"/> (ordinal) whose key starts with <paramref name="prefix"/>: binary search, then a scan.</summary>
    private static List<T> Range<T>(T[] sorted, Func<T, string> key, string prefix)
    {
        var lo = 0;
        var hi = sorted.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (string.CompareOrdinal(key(sorted[mid]), prefix) < 0)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        var hits = new List<T>();
        for (var i = lo; i < sorted.Length && key(sorted[i]).StartsWith(prefix, StringComparison.Ordinal); i++)
        {
            hits.Add(sorted[i]);
        }

        return hits;
    }

    private static Dictionary<string, List<(string Strongs, int Count)>> Group(IEnumerable<LxxNtFormCount> forms, Func<LxxNtFormCount, string> key) =>
        forms.Select(f => (Key: key(f), f.Strongs, f.Count))
            .Where(x => x.Key.Length > 0)
            .GroupBy(x => x.Key, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(x => x.Strongs, StringComparer.Ordinal)
                    .Select(s => (Strongs: s.Key, Count: s.Sum(x => x.Count)))
                    .OrderByDescending(s => s.Count)
                    .ThenBy(s => s.Strongs, StringComparer.Ordinal)
                    .ToList(),
                StringComparer.Ordinal);
}
