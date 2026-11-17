namespace UnshackledWord.Application.Features.AgentMapping;

/// <summary>What the lemma lexicon says about one German word (05-lemmatisation.md).</summary>
public sealed class LemmaResolution
{
    /// <summary>The lemma shown to the model: the stored one, or the single trusted lemma of the form.</summary>
    public string? Lemma { get; init; }

    /// <summary><see cref="AgentLemmaState"/>; null = only a LemmaSharp candidate.</summary>
    public string? State { get; init; }

    /// <summary>Further lemmas the form has (trusted ones first, then by frequency); empty when the form is unambiguous.</summary>
    public IReadOnlyList<string> Candidates { get; init; } = [];
}

/// <summary>
/// Form-to-lemma table of one package, built once per run from three sources in priority order: (1) human edits, (2) merged AI proposals
/// (their edits carry the AI user as author and count as trusted too), (3) LemmaSharp output, which is a candidate only.
/// </summary>
public sealed class LemmaLexicon
{
    private const int MaxCandidates = 3;

    private readonly Dictionary<string, List<AgentFormLemmaRow>> _byForm;

    private LemmaLexicon(Dictionary<string, List<AgentFormLemmaRow>> byForm)
    {
        _byForm = byForm;
    }

    public static LemmaLexicon Build(IEnumerable<AgentFormLemmaRow> rows) =>
        new(rows.GroupBy(r => r.Form.ToLowerInvariant()).ToDictionary(g => g.Key, g => g.ToList()));

    /// <summary>Lemmas that a person decided (or a maintainer merged) for words with this form, current values only.</summary>
    public IReadOnlyList<string> TrustedLemmas(string form) =>
        _byForm.TryGetValue(form.ToLowerInvariant(), out var rows)
            ? rows.Where(r => r.Trusted).OrderByDescending(r => r.Count).Select(r => r.Lemma).Distinct().ToList()
            : [];

    /// <summary>
    /// <paramref name="humanEditedExact"/>: this very word has a human edit, so its stored lemma is final (<c>fixed</c>). A form with exactly one
    /// trusted lemma is a <c>hint</c> (04-work-package.md; 05-lemmatisation.md calls it fixed, the table wins because homographs exist). Everything
    /// else keeps the LemmaSharp lemma and lists the other lemmas of the form as candidates.
    /// </summary>
    public LemmaResolution Resolve(string form, string? storedLemma, bool humanEditedExact)
    {
        var key = form.ToLowerInvariant();
        var all = _byForm.TryGetValue(key, out var rows) ? rows : [];
        var trusted = TrustedLemmas(key);

        if (humanEditedExact)
        {
            return new LemmaResolution { Lemma = storedLemma, State = AgentLemmaState.Fixed, Candidates = [] };
        }

        if (trusted.Count == 1)
        {
            return new LemmaResolution { Lemma = trusted[0], State = AgentLemmaState.Hint, Candidates = [] };
        }

        var shown = trusted.Count > 1 ? null : storedLemma;
        var others = trusted
            .Concat(all.Where(r => !r.Trusted).OrderByDescending(r => r.Count).Select(r => r.Lemma))
            .Where(l => !string.Equals(l, shown, StringComparison.Ordinal))
            .Distinct()
            .Take(MaxCandidates)
            .ToList();

        return new LemmaResolution
        {
            Lemma = shown ?? (others.Count > 0 ? others[0] : storedLemma),
            State = null,
            Candidates = shown is null && others.Count > 0 ? others.Skip(1).ToList() : others,
        };
    }

    /// <summary>Lemmas that people decided for words of this form (soft rule "differs from a human-edited lemma elsewhere").</summary>
    public IReadOnlySet<string> HumanLemmas(string form) => TrustedLemmas(form).ToHashSet(StringComparer.Ordinal);
}
