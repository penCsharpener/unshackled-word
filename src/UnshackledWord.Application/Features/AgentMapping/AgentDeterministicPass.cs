using System.Text.RegularExpressions;

namespace UnshackledWord.Application.Features.AgentMapping;

/// <summary>German token of a verse draft with the outcome of the deterministic pass.</summary>
public sealed class DraftGerman
{
    public required AgentGermanWordRow Row { get; init; }
    public string Status { get; set; } = AgentTokenStatus.Open;
    public string? LemmaState { get; set; }
    public string? Lemma { get; set; }
    public IReadOnlyList<string> Candidates { get; set; } = [];
}

public sealed class DraftSource
{
    public required AgentSourceWordRow Row { get; init; }
    public bool Exempt { get; init; }
    public bool IsName { get; init; }

    /// <summary>Lemma mode, Hebrew: the morphemes of the word (punctuation left out).</summary>
    public IReadOnlyList<AgentMorphemeMap>? Morphemes { get; init; }
}

/// <summary>A link in real ids. <see cref="SourceId"/> null + <see cref="ParentId"/> set = added word.</summary>
public sealed class DraftLink
{
    public int GermanId { get; init; }
    public int? SourceId { get; init; }
    public int? ParentId { get; init; }
    public int? PartOrder { get; init; }
    public string? Part { get; init; }
    public bool Fixed { get; init; }

    /// <summary>Lemma mode: role, morpheme, part lemma and multi-head flag of the link (see <see cref="AgentLinkMap"/>).</summary>
    public string? Role { get; init; }

    public string? Strongs { get; init; }
    public string? PartLemma { get; init; }
    public bool MultiHead { get; init; }
    public bool Decided { get; init; }
    public string? StoredRole { get; init; }
    public string? StoredStrongs { get; init; }
    public bool Open { get; init; }
}

public sealed class VerseDraft
{
    public int Chapter { get; init; }
    public int Verse { get; init; }
    public required string Ref { get; init; }
    public required string Hash { get; init; }
    public int RemovedPunctuation { get; init; }
    public required IReadOnlyList<DraftGerman> German { get; init; }
    public required IReadOnlyList<DraftSource> Source { get; init; }
    public required IReadOnlyList<DraftLink> Links { get; init; }

    /// <summary>Lemma mode: why the verse is sent (<see cref="AgentLemmaWork"/> reason codes); null in the mapping modes.</summary>
    public IReadOnlyList<string>? Work { get; init; }

    /// <summary>
    /// Nothing is left for the model, the verse is not sent. Mapping modes: links and lemmas of every token are settled. Lemma mode: no
    /// work reason (06-ai-runs.md).
    /// </summary>
    public bool FullyFixed => Work is not null ? Work.Count == 0 : German.All(g => g.Status == AgentTokenStatus.Fixed && g.LemmaState == AgentLemmaState.Fixed);
}

/// <summary>Confirmed lemma-to-Strongs counts (human links only), the data of the statistics and proper-name rules.</summary>
public sealed class ConfirmedStats
{
    public static readonly ConfirmedStats Empty = new([]);

    private readonly Dictionary<string, Dictionary<string, int>> _byLemma;

    public ConfirmedStats(IEnumerable<AgentConfirmedPairRow> rows)
    {
        _byLemma = rows
            .GroupBy(r => r.Lemma.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.GroupBy(r => r.Strongs).ToDictionary(x => x.Key, x => x.Sum(r => r.Count)));
    }

    public int Count(string? lemma, string? strongs) =>
        lemma is not null && strongs is not null && _byLemma.TryGetValue(lemma.ToLowerInvariant(), out var map) && map.TryGetValue(strongs, out var n) ? n : 0;

    public int Total(string? lemma) => lemma is not null && _byLemma.TryGetValue(lemma.ToLowerInvariant(), out var map) ? map.Values.Sum() : 0;
}

/// <summary>
/// The deterministic pass of 04-work-package.md, one verse at a time and without database access. Whatever an algorithm can decide it decides;
/// the model gets the rest. Rule order for a token's link: existing human-confirmed link, proper name, statistics, article.
/// </summary>
public static class AgentDeterministicPass
{
    private static readonly Regex GreekExempt = new(@"^(T-|CONJ|PRT)", RegexOptions.Compiled);
    private static readonly Regex HebrewExempt = new(@"^H(Td|To|C)$", RegexOptions.Compiled);
    private static readonly Regex GreekName = new(@"^N-PRI$|-[PL]$", RegexOptions.Compiled);
    private static readonly Regex HebrewName = new(@"(^H|/)Np", RegexOptions.Compiled);
    private static readonly Regex GreekArticle = new(@"^T-", RegexOptions.Compiled);
    private static readonly Regex HebrewArticle = new(@"(^H|/)Td", RegexOptions.Compiled);

    /// <summary>Punctuation-only tokens (no letter and no digit) are not mapped and removed from the package.</summary>
    public static bool IsPunctuationOnly(AgentGermanWordRow word) =>
        string.IsNullOrWhiteSpace(word.Plain) || !word.Plain.Any(char.IsLetterOrDigit);

    public static bool IsArticleOrParticle(string? morphology, bool hebrew) =>
        morphology is not null && (hebrew ? HebrewExempt : GreekExempt).IsMatch(morphology);

    public static bool IsName(string? morphology, bool hebrew) =>
        morphology is not null && (hebrew ? HebrewName : GreekName).IsMatch(morphology);

    public static bool IsArticle(string? morphology, bool hebrew) =>
        morphology is not null && (hebrew ? HebrewArticle : GreekArticle).IsMatch(morphology);

    public static VerseDraft Run(
        int chapter,
        int verse,
        string verseRef,
        string hash,
        IReadOnlyList<AgentGermanWordRow> germanWords,
        IReadOnlyList<AgentSourceWordRow> sourceWords,
        IReadOnlyList<AgentMappingRow> mappings,
        LemmaLexicon lexicon,
        ConfirmedStats stats,
        AgentMappingOptions options,
        AgentMode mode,
        bool hebrew)
    {
        var tokens = germanWords
            .Where(w => !IsPunctuationOnly(w))
            .OrderBy(w => w.PositionInVerse).ThenBy(w => w.Id)
            .Select(w => new DraftGerman { Row = w })
            .ToList();
        var source = sourceWords
            .OrderBy(w => w.Position).ThenBy(w => w.Id)
            .Select(w => new DraftSource { Row = w, Exempt = IsArticleOrParticle(w.Morphology, hebrew), IsName = IsName(w.Morphology, hebrew) })
            .ToList();

        var tokenIds = tokens.Select(t => t.Row.Id).ToHashSet();
        var sourceIds = source.Select(s => s.Row.Id).ToHashSet();
        var links = new List<DraftLink>();

        foreach (var token in tokens)
        {
            var lemma = lexicon.Resolve(token.Row.Plain, token.Row.Lemma, token.Row.LemmaHumanEdited);
            token.Lemma = lemma.Lemma;
            token.LemmaState = lemma.State;
            token.Candidates = lemma.Candidates;
        }

        ExistingLinks(tokens, mappings, tokenIds, sourceIds, mode, links);
        ProperNames(tokens, source, stats, options, links);
        Statistics(tokens, source, stats, options, links);
        Articles(tokens, source, hebrew, links);

        foreach (var token in tokens)
        {
            var mine = links.Where(l => l.GermanId == token.Row.Id).ToList();
            token.Status = mine.Count == 0 ? AgentTokenStatus.Open : mine.All(l => l.Fixed) ? AgentTokenStatus.Fixed : AgentTokenStatus.Hint;
        }

        return new VerseDraft
        {
            Chapter = chapter,
            Verse = verse,
            Ref = verseRef,
            Hash = hash,
            RemovedPunctuation = germanWords.Count - tokens.Count,
            German = tokens,
            Source = source,
            Links = links,
        };
    }

    /// <summary>
    /// Existing mapping rows. "Confirmed" = written by a non-AI user (<c>CreatedByUserId</c>); there is no explicit confirmation flag yet.
    /// fill-open: every existing link is context (<c>fixed</c>). redo: every existing link (seed, AI and human-confirmed alike) comes back as a
    /// <c>hint</c> (links and added words); nothing becomes <c>fixed</c> from existing data.
    /// </summary>
    private static void ExistingLinks(List<DraftGerman> tokens, IReadOnlyList<AgentMappingRow> mappings, HashSet<int> tokenIds, HashSet<int> sourceIds, AgentMode mode, List<DraftLink> links)
    {
        foreach (var token in tokens)
        {
            var rows = mappings
                .Where(m => m.ElbWordId == token.Row.Id)
                .Where(m => m.IsAddedWord ? m.ParentElbWordId is { } p && p != token.Row.Id && tokenIds.Contains(p) : m.StepWordId is { } s && sourceIds.Contains(s))
                .ToList();

            foreach (var row in rows)
            {
                var hasPart = row.PartOrder is >= 1 && !string.IsNullOrWhiteSpace(row.GermanWordPart);
                links.Add(new DraftLink
                {
                    GermanId = token.Row.Id,
                    SourceId = row.IsAddedWord ? null : row.StepWordId,
                    ParentId = row.IsAddedWord ? row.ParentElbWordId : null,
                    PartOrder = hasPart ? row.PartOrder : null,
                    Part = hasPart ? row.GermanWordPart!.Trim() : null,
                    Fixed = mode == AgentMode.FillOpen,
                });
            }
        }
    }

    private static bool IsOpen(DraftGerman token, List<DraftLink> links) => links.All(l => l.GermanId != token.Row.Id);

    /// <summary>
    /// A German name (STTS <c>NE</c>) and a name in the original (morphology) whose Strongs number has been confirmed for this lemma at least
    /// <see cref="AgentMappingOptions.NameMinConfirmations"/> times, with exactly one such German token and one such original word in the verse: fixed.
    /// The "known German form" has no source besides confirmed links yet, so the rule stays silent until people have confirmed some.
    /// </summary>
    private static void ProperNames(List<DraftGerman> tokens, List<DraftSource> source, ConfirmedStats stats, AgentMappingOptions options, List<DraftLink> links)
    {
        foreach (var token in tokens.Where(t => t.Row.Stts == "NE" && IsOpen(t, links)))
        {
            var lemma = token.Lemma ?? token.Row.Lemma;
            var sameLemma = tokens.Count(t => string.Equals(t.Lemma ?? t.Row.Lemma, lemma, StringComparison.OrdinalIgnoreCase) && t.Row.Stts == "NE");
            var candidates = source.Where(s => s.IsName && stats.Count(lemma, s.Row.Strongs) >= options.NameMinConfirmations).ToList();
            if (sameLemma == 1 && candidates.Count == 1)
            {
                links.Add(new DraftLink { GermanId = token.Row.Id, SourceId = candidates[0].Row.Id, Fixed = true });
            }
        }
    }

    /// <summary>Exactly one original word of the verse whose Strongs has been confirmed for this German lemma at least K times, and no other with a confirmation: hint.</summary>
    private static void Statistics(List<DraftGerman> tokens, List<DraftSource> source, ConfirmedStats stats, AgentMappingOptions options, List<DraftLink> links)
    {
        foreach (var token in tokens.Where(t => IsOpen(t, links)))
        {
            var lemma = token.Lemma ?? token.Row.Lemma;
            var seen = source.Where(s => stats.Count(lemma, s.Row.Strongs) > 0).ToList();
            if (seen.Count == 1 && stats.Count(lemma, seen[0].Row.Strongs) >= options.MinConfirmations)
            {
                links.Add(new DraftLink { GermanId = token.Row.Id, SourceId = seen[0].Row.Id, Fixed = false });
            }
        }
    }

    /// <summary>A German article (STTS <c>ART</c>) directly before a noun, in a verse where the original has no article at all: hint "added word", parent = the noun.</summary>
    private static void Articles(List<DraftGerman> tokens, List<DraftSource> source, bool hebrew, List<DraftLink> links)
    {
        if (source.Any(s => IsArticle(s.Row.Morphology, hebrew)))
        {
            return;
        }

        for (var i = 0; i < tokens.Count - 1; i++)
        {
            var article = tokens[i];
            var next = tokens[i + 1];
            if (article.Row.Stts == "ART" && next.Row.Stts is "NN" or "NE" && IsOpen(article, links))
            {
                links.Add(new DraftLink { GermanId = article.Row.Id, ParentId = next.Row.Id, Fixed = false });
            }
        }
    }
}
