using UnshackledWord.Domain.Models.Settings;

namespace UnshackledWord.Domain.Models.Vocab;

/// <summary>One German rendering row of an entry (renderings summed over all its Strong's keys), grouped per German key and STTS tag.</summary>
public sealed record VocabRenderingCandidate(string GermanKey, string German, string? GermanPos, int LinkCount);

/// <summary>Word class of a vocab entry, derived from <c>VocabEntries.Pos</c> (STEP morphology).</summary>
public enum VocabEntryClass
{
    Content,
    Article,
    Conjunction,
    Pronoun,
    Particle,
    Preposition,
    Interjection
}

/// <summary>A row of <c>VocabMeanings</c> as it is stored.</summary>
public sealed record VocabMeaningRow(int Id, string German, string? GermanKey, int Rank, int? Count, decimal? Share, bool IsAccepted, string Origin, bool IsPinned)
{
    /// <summary>Derived and unpinned rows are the ones re-derivation replaces; manual and pinned rows always survive.</summary>
    public bool IsReplaceable => Origin == VocabMeaningSelector.OriginDerived && !IsPinned;
}

/// <summary>A meaning the derivation wants to have.</summary>
public sealed record VocabDerivedMeaning(string German, string GermanKey, int Rank, int Count, decimal Share);

/// <summary>What to do with one entry's meaning rows.</summary>
public sealed class VocabMeaningPlan
{
    public List<VocabDerivedMeaning> Insert { get; init; } = [];

    /// <summary>Existing replaceable rows kept by German, with new rank/count/share/key (only listed when something differs).</summary>
    public List<(int Id, VocabDerivedMeaning Meaning)> Update { get; init; } = [];

    public List<int> DeleteIds { get; init; } = [];

    /// <summary>Accepted German texts in rank order before and after.</summary>
    public List<string> BeforeAccepted { get; init; } = [];

    public List<string> AfterAccepted { get; init; } = [];

    public bool HasWrites => Insert.Count > 0 || Update.Count > 0 || DeleteIds.Count > 0;

    /// <summary>The set of accepted German texts changed; this is what bumps <c>ContentVersion</c> (order alone does not).</summary>
    public bool AcceptedSetChanged => !BeforeAccepted.ToHashSet(StringComparer.Ordinal).SetEquals(AfterAccepted);

    /// <summary>
    /// Bumps <c>ContentVersion</c> and writes a change row: the accepted set changed and there were accepted meanings before. The very first
    /// meanings of an entry bump nothing, since no learner can have a card for an entry that had no meaning.
    /// </summary>
    public bool BumpsVersion => BeforeAccepted.Count > 0 && AcceptedSetChanged;

    public string? OldTop => BeforeAccepted.FirstOrDefault();

    public string? NewTop => AfterAccepted.FirstOrDefault();

    public bool TopChanged => !string.Equals(OldTop, NewTop, StringComparison.Ordinal);
}

/// <summary>
/// Pure selection logic of the meaning derivation (roadmap 0022.3, docs/roadmaps/0022-vocab-trainer/03-vocab-model.md "Deriving meanings").
/// Keys that are German function words (by STTS tag) are dropped first, unless that would leave the entry without any key (articles,
/// "und"): then the unfiltered set is used. Share = link count of the key / head links of the (filtered) set. Keys need
/// <c>Share >= MeaningMinShare</c> and <c>LinkCount >= MeaningMinCount</c>; at most <c>MeaningMax</c> derived meanings. Manual and
/// pinned rows are never touched and keep their rank; a derived meaning whose German equals such a row is skipped; derived meanings
/// take the lowest free ranks in order of link count (ties by German key).
/// </summary>
public static class VocabMeaningSelector
{
    public const string OriginDerived = "derived";
    public const string OriginManual = "manual";

    /// <summary>STTS tags of German function words that are not meanings: auxiliaries (VA*), article, personal pronoun, coordinating conjunction, reflexive pronoun, separable verb prefix.</summary>
    public static readonly IReadOnlySet<string> FunctionWordTags = new HashSet<string>(StringComparer.Ordinal)
    {
        "VAFIN", "VAIMP", "VAINF", "VAPP", "ART", "PPER", "KON", "PRF", "PTKVZ"
    };

    public static bool IsFunctionWordTag(string? stts) => stts is not null && FunctionWordTags.Contains(stts);

    private static readonly IReadOnlyDictionary<VocabEntryClass, Func<string, bool>> ClassTags = new Dictionary<VocabEntryClass, Func<string, bool>>
    {
        [VocabEntryClass.Article] = t => t == "ART",
        [VocabEntryClass.Conjunction] = t => t is "KON" or "KOUS" or "KOUI" or "KOKOM",
        [VocabEntryClass.Pronoun] = t => t is "PPER" or "PRF" or "PPOSAT" or "PDS" or "PDAT" or "PIS" or "PIAT" or "PIDAT" or "PRELS" or "PRELAT" or "PWS" or "PWAT" or "PWAV" or "PAV" or "PROAV",
        // Hebrew particles (אֵת "with") are rendered as prepositions; the separable verb prefix PTKVZ is never a meaning
        [VocabEntryClass.Particle] = t => (t.StartsWith("PTK", StringComparison.Ordinal) && t != "PTKVZ") || t is "APPR" or "APPRART",
        [VocabEntryClass.Preposition] = t => t is "APPR" or "APPRART" or "APPO" or "APZR",
        [VocabEntryClass.Interjection] = t => t == "ITJ"
    };

    /// <summary>
    /// Maps <c>VocabEntries.Pos</c> (STEP morphology such as <c>G:N-M</c>, <c>G:CONJ</c>, <c>H:Prep</c>, <c>G:PRT-N+G:CONJ</c>, <c>N:N-F-P</c>) to a word
    /// class by its first part. Nouns, proper names, verbs, adjectives, adverbs and anything unknown or empty are content words.
    /// </summary>
    public static VocabEntryClass ClassifyEntry(string? pos)
    {
        if (string.IsNullOrWhiteSpace(pos))
        {
            return VocabEntryClass.Content;
        }

        var first = pos.Split([' ', '/', '+'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        var code = (first.Length > 2 && first[1] == ':' ? first[2..] : first).ToUpperInvariant();

        return code switch
        {
            "T" => VocabEntryClass.Article,
            _ when code.StartsWith("CONJ", StringComparison.Ordinal) || code is "C-" or "COND" or "K" => VocabEntryClass.Conjunction,
            _ when code.StartsWith("PREP", StringComparison.Ordinal) => VocabEntryClass.Preposition,
            _ when code.StartsWith("PRT", StringComparison.Ordinal) || code is "PART" or "NEG" or "Q" => VocabEntryClass.Particle,
            "INJ" or "INTJ" or "I" or "INTG" => VocabEntryClass.Interjection,
            _ when code is "D" or "R" or "P" or "X" or "DEMP" or "RELP" or "INDP" || code.StartsWith("P-", StringComparison.Ordinal) || code.StartsWith("F-", StringComparison.Ordinal)
                || code.StartsWith("PERP", StringComparison.Ordinal) || code is "PREFIX" or "SUFFIX" || IsHebrewSuffixCode(code) => VocabEntryClass.Pronoun,
            _ => VocabEntryClass.Content
        };
    }

    // Hebrew pronominal suffix morphemes: Sp2m, Ps3f, Op1c, Os3m, Ss3f ...
    private static bool IsHebrewSuffixCode(string code) =>
        code.Length == 4 && code[0] is 'S' or 'P' or 'O' && code[1] is 'S' or 'P' && char.IsDigit(code[2]);

    /// <summary>Nouns keep their capital; every other word class gets a lower-case first letter (sentence-start capitals in the live text).</summary>
    public static string DisplayText(string german, string? stts) =>
        stts is null or "NN" or "NE" || german.Length == 0 ? german : char.ToLowerInvariant(german[0]) + german[1..];

    /// <summary>
    /// Content entries drop the German function-word tags (falling back to everything when nothing else is left). Function-word entries
    /// keep only the tags of their own class (falling back to everything), so καί gets "und" and ὁ gets "der".
    /// </summary>
    public static List<VocabRenderingCandidate> FilterByClass(IReadOnlyList<VocabRenderingCandidate> candidates, VocabEntryClass entryClass)
    {
        var kept = entryClass == VocabEntryClass.Content
            ? candidates.Where(c => !IsFunctionWordTag(c.GermanPos)).ToList()
            : candidates.Where(c => c.GermanPos is not null && ClassTags[entryClass](c.GermanPos)).ToList();

        return kept.Count > 0 ? kept : candidates.ToList();
    }

    public static List<VocabDerivedMeaning> Derive(IReadOnlyList<VocabRenderingCandidate> candidates, VocabOptions options, IReadOnlyCollection<VocabMeaningRow> protectedRows, VocabEntryClass entryClass = VocabEntryClass.Content)
    {
        // one candidate per German key: links summed, display text of the most frequent row
        var pool = FilterByClass(candidates, entryClass)
            .GroupBy(c => c.GermanKey, StringComparer.Ordinal)
            .Select(g => new VocabRenderingCandidate(g.Key, DisplayText(g.OrderByDescending(c => c.LinkCount).First().German, g.OrderByDescending(c => c.LinkCount).First().GermanPos), null, g.Sum(c => c.LinkCount)))
            .ToList();

        var total = pool.Sum(c => c.LinkCount);
        if (total <= 0)
        {
            return [];
        }

        var taken = protectedRows.Select(r => r.German).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var usedRanks = protectedRows.Select(r => r.Rank).ToHashSet();
        var result = new List<VocabDerivedMeaning>();
        var nextRank = 1;

        foreach (var c in pool.OrderByDescending(c => c.LinkCount).ThenBy(c => c.GermanKey, StringComparer.Ordinal))
        {
            var share = Math.Round(c.LinkCount * 100m / total, 2);
            if (share < options.MeaningMinShare || c.LinkCount < options.MeaningMinCount)
            {
                continue;
            }

            if (result.Count >= options.MeaningMax)
            {
                break;
            }

            if (!taken.Add(c.German))
            {
                continue;
            }

            while (usedRanks.Contains(nextRank))
            {
                nextRank++;
            }

            result.Add(new VocabDerivedMeaning(c.German, c.GermanKey, nextRank, c.LinkCount, share));
            nextRank++;
        }

        if (result.Count == 0)
        {
            // Nothing reaches the thresholds (hapax, rare words): the single top key stands in as the one meaning.
            var top = pool.Where(c => c.LinkCount >= 1 && !taken.Contains(c.German)).OrderByDescending(c => c.LinkCount).ThenBy(c => c.GermanKey, StringComparer.Ordinal).FirstOrDefault();
            if (top is not null)
            {
                while (usedRanks.Contains(nextRank))
                {
                    nextRank++;
                }

                result.Add(new VocabDerivedMeaning(top.German, top.GermanKey, nextRank, top.LinkCount, Math.Round(top.LinkCount * 100m / total, 2)));
            }
        }

        return result;
    }

    public static VocabMeaningPlan Plan(IReadOnlyList<VocabMeaningRow> existing, IReadOnlyList<VocabRenderingCandidate> candidates, VocabOptions options, VocabEntryClass entryClass = VocabEntryClass.Content)
    {
        var protectedRows = existing.Where(r => !r.IsReplaceable).ToList();
        var wanted = Derive(candidates, options, protectedRows, entryClass);
        var replaceable = existing.Where(r => r.IsReplaceable).ToDictionary(r => r.German, StringComparer.Ordinal);
        var plan = new VocabMeaningPlan();

        foreach (var w in wanted)
        {
            if (!replaceable.Remove(w.German, out var old))
            {
                plan.Insert.Add(w);
            }
            else if (old.Rank != w.Rank || old.Count != w.Count || old.Share != w.Share || old.GermanKey != w.GermanKey || !old.IsAccepted)
            {
                plan.Update.Add((old.Id, w));
            }
        }

        plan.DeleteIds.AddRange(replaceable.Values.Select(r => r.Id));

        plan.BeforeAccepted.AddRange(existing.Where(r => r.IsAccepted).OrderBy(r => r.Rank).ThenBy(r => r.Id).Select(r => r.German));
        plan.AfterAccepted.AddRange(protectedRows.Where(r => r.IsAccepted).Select(r => (r.Rank, r.German))
            .Concat(wanted.Select(w => (w.Rank, w.German)))
            .OrderBy(x => x.Rank).Select(x => x.German));

        return plan;
    }
}
