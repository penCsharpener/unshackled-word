namespace UnshackledWord.Application.Features.LinkRoles;

/// <summary>
/// The deterministic link-role backfill rules 1–6 of roadmap 0018 (05-data-model.md), pure and per verse. Every rule is evaluated against
/// the input state; a link is decided only when exactly one rule fits (or several fit with the same outcome). Two rules with different
/// outcomes (e.g. Rt 3:4 <c>sich</c>: rule 1 → suffix H9043, rule 4 → unit of the verb) leave the link undecided for the model.
/// </summary>
/// <remarks>
/// "Same morpheme" (rules 2–5) = same source word and same Strong's: Greek the link's Strong's, Hebrew the link's stored morpheme or the
/// main morpheme for the candidate, and the morpheme rule 1 gives for the partner. Added-word rows are ignored. Compound-part links
/// (<c>GermanWordPart</c>) only get rule 1.
/// </remarks>
public static class LinkRoleRules
{
    /// <param name="verse">The verse with all links of one mapping table.</param>
    /// <param name="knownLemmas">
    /// Every lemma of Elb1871Morphology (case-insensitive), built once per run. Rule 3b only combines a particle with a verb lemma when the
    /// combination is in this set; null switches rule 3b off.
    /// </param>
    public static LinkRoleVerseResult Decide(LinkRoleVerse verse, IReadOnlySet<string>? knownLemmas = null)
    {
        var hebrew = verse.Language == SourceLanguage.Hebrew;
        var words = verse.Words.GroupBy(w => w.Id).ToDictionary(g => g.Key, g => g.First());
        var ordered = verse.Words.OrderBy(w => w.PositionInVerse).ToList();
        var sources = verse.SourceWords.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First());
        var morphemes = hebrew
            ? sources.ToDictionary(s => s.Key, s => SourceMorphemes.ParseHebrew(s.Value.ExpandedStrongTags, s.Value.Morphology))
            : new Dictionary<int, IReadOnlyList<SourceMorpheme>>();

        var states = verse.Links
            .Where(l => !l.IsAddedWord && l.StepWordId is not null && words.ContainsKey(l.GermanWordId))
            .Select(l => CreateState(l, words[l.GermanWordId], ordered, hebrew, sources, morphemes))
            .ToList();

        var decisions = new List<LinkRoleDecision>();
        var undecided = new List<LinkRoleUndecided>();
        var verbLemmas = new List<SeparableVerbLemma>();
        foreach (var state in states)
        {
            var evaluation = FittingRules(state, states, hebrew, sources, morphemes, knownLemmas);
            if (Resolve(state, evaluation, hebrew, decisions, undecided) is { Rule: LinkRoleRule.R3bSeparableVerb } && evaluation.VerbLemma is { } verbLemma)
            {
                verbLemmas.Add(verbLemma);
            }
        }

        return new LinkRoleVerseResult(verse.HebRefId, decisions, undecided, ParticleLemmaResets(ordered), DistinctVerbLemmas(verbLemmas, decisions, undecided));
    }

    /// <summary>
    /// One lemma change per verb. Two particles that want different lemmas on the same verb (<c>auf</c> and <c>hin</c>) undo their rule-3b
    /// decisions: both links become undecided.
    /// </summary>
    private static List<SeparableVerbLemma> DistinctVerbLemmas(List<SeparableVerbLemma> changes, List<LinkRoleDecision> decisions, List<LinkRoleUndecided> undecided)
    {
        var result = new List<SeparableVerbLemma>();
        foreach (var group in changes.GroupBy(c => c.GermanWordId))
        {
            if (group.Select(c => c.NewLemma).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1)
            {
                result.Add(group.First());
                continue;
            }

            var particles = group.Select(c => c.ParticleGermanWordId).ToHashSet();
            foreach (var decision in decisions.Where(d => d.Rule == LinkRoleRule.R3bSeparableVerb && particles.Contains(d.GermanWordId)).ToList())
            {
                decisions.Remove(decision);
                undecided.Add(new LinkRoleUndecided(decision.LinkId, decision.GermanWordId, [LinkRoleRule.R3bSeparableVerb],
                    $"separable verb, several particles on one verb: {string.Join(", ", group.Select(c => c.NewLemma).Distinct())}"));
            }
        }

        return result;
    }

    /// <summary>Rule 6: every <c>PTKVZ</c> whose lemma is not its own text (case-insensitive) gets its text in lower case.</summary>
    public static IReadOnlyList<ParticleLemmaReset> ParticleLemmaResets(IEnumerable<LinkRoleGermanWord> words)
    {
        return words
            .Where(w => w.Stts == "PTKVZ" && !string.Equals(w.Lemma?.Trim(), w.Text.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(w => new ParticleLemmaReset(w.Id, w.PositionInVerse, w.Lemma, w.Text.Trim().ToLowerInvariant()))
            .ToList();
    }

    /// <summary><c>PTKZU</c>, or <c>zu</c> tagged <c>PTKVZ</c> right before an infinitive (Elb1871 tags infinitive <c>zu</c> as <c>PTKVZ</c>).</summary>
    public static bool IsZuInfinitive(LinkRoleGermanWord word, LinkRoleGermanWord? next) =>
        word.Stts == "PTKZU"
        || (word.Stts == "PTKVZ" && word.Text.Trim().Equals("zu", StringComparison.OrdinalIgnoreCase) && next?.Stts is { } s && s.EndsWith("INF", StringComparison.Ordinal));

    private sealed record LinkState(
        LinkRoleLink Link,
        LinkRoleGermanWord Word,
        bool ZuInfinitive,
        MorphemeHint Hint,
        string? BaseMorpheme,
        string? BaseProblem)
    {
        /// <summary>The morpheme this link ends up on when it is a partner of another link's rule.</summary>
        public string? EffectiveMorpheme => Hint.Kind == MorphemeHintKind.Match ? Hint.Strongs : BaseMorpheme;
    }

    private sealed record Outcome(LinkRole Role, string? Strongs, int? ParentId);

    /// <summary>The fitting rules of a link; <see cref="Blocker"/> set = undecided for the model whatever fits; <see cref="VerbLemma"/> is rule 3b's verb change.</summary>
    private sealed record Evaluation(List<(LinkRoleRule Rule, Outcome Outcome)> Fits, string? Blocker = null, SeparableVerbLemma? VerbLemma = null);

    private static LinkState CreateState(
        LinkRoleLink link,
        LinkRoleGermanWord word,
        List<LinkRoleGermanWord> ordered,
        bool hebrew,
        Dictionary<int, LinkRoleSourceWord> sources,
        Dictionary<int, IReadOnlyList<SourceMorpheme>> morphemes)
    {
        var index = ordered.FindIndex(w => w.Id == word.Id);
        var next = index >= 0 && index + 1 < ordered.Count ? ordered[index + 1] : null;
        var zuInfinitive = IsZuInfinitive(word, next);
        var fixedMorpheme = string.IsNullOrWhiteSpace(link.StrongsNumber) ? null : link.StrongsNumber.Trim();
        if (!hebrew)
        {
            return new LinkState(link, word, zuInfinitive, MorphemeHint.None, fixedMorpheme, fixedMorpheme is null ? "link has no Strong's" : null);
        }

        if (fixedMorpheme is not null)
        {
            return new LinkState(link, word, zuInfinitive, MorphemeHint.None, fixedMorpheme, null);
        }

        if (!sources.ContainsKey(link.StepWordId!.Value) || morphemes[link.StepWordId.Value].Count == 0)
        {
            return new LinkState(link, word, zuInfinitive, MorphemeHint.None, null, $"source word {link.StepWordId} not found or without morphemes");
        }

        var parsed = morphemes[link.StepWordId.Value];
        var hint = HebrewMorphemeHints.Find(word, parsed, zuInfinitive);
        var mains = parsed.Where(m => m.IsMain).Select(m => m.Strongs).ToList();
        return mains.Count == 1
            ? new LinkState(link, word, zuInfinitive, hint, mains[0], null)
            : new LinkState(link, word, zuInfinitive, hint, null, mains.Count == 0
                ? $"source word {link.StepWordId} has no main morpheme"
                : $"source word {link.StepWordId} has several main morphemes: {string.Join(", ", mains)}");
    }

    private static Evaluation FittingRules(
        LinkState state,
        List<LinkState> all,
        bool hebrew,
        Dictionary<int, LinkRoleSourceWord> sources,
        Dictionary<int, IReadOnlyList<SourceMorpheme>> morphemes,
        IReadOnlySet<string>? knownLemmas)
    {
        var fits = new List<(LinkRoleRule, Outcome)>();
        if (state.Hint.Kind == MorphemeHintKind.Match)
        {
            fits.Add((LinkRoleRule.R1MorphemeHint, new Outcome(LinkRole.Head, state.Hint.Strongs, null)));
        }

        if (state.Link.GermanWordPart is not null || state.BaseMorpheme is null)
        {
            return new Evaluation(fits);
        }

        var partners = all
            .Where(p => !ReferenceEquals(p, state)
                && p.Word.Id != state.Word.Id
                && p.Link.GermanWordPart is null
                && p.Link.StepWordId == state.Link.StepWordId
                && string.Equals(p.EffectiveMorpheme, state.BaseMorpheme, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (partners.Count == 0)
        {
            return new Evaluation(fits);
        }

        var stts = state.Word.Stts ?? string.Empty;
        var unit = new Outcome(LinkRole.Unit, state.BaseMorpheme, null);

        // Rule 2: auxiliary or modal next to a full verb or participle.
        if ((stts.StartsWith("VA", StringComparison.Ordinal) || stts.StartsWith("VM", StringComparison.Ordinal))
            && partners.Any(p => p.Word.Stts is { } ps && (ps.StartsWith("VV", StringComparison.Ordinal) || ps.EndsWith("PP", StringComparison.Ordinal))))
        {
            fits.Add((LinkRoleRule.R2AuxiliaryUnit, unit));
        }

        // Rule 3: separable particle of a finite verb whose lemma starts with it. Rule 3b: the verb lemma lacks the particle yet.
        string? blocker = null;
        SeparableVerbLemma? verbLemma = null;
        if (stts == "PTKVZ" && !state.ZuInfinitive)
        {
            var particle = state.Word.Text.Trim().ToLowerInvariant();
            var finite = partners.Where(p => IsFiniteGerman(p.Word.Stts) && !string.IsNullOrWhiteSpace(p.Word.Lemma)).Select(p => p.Word).DistinctBy(w => w.Id).ToList();
            if (particle.Length > 0 && finite.Any(w => StartsWithParticle(w.Lemma!, particle)))
            {
                fits.Add((LinkRoleRule.R3ParticleUnit, unit));
            }
            else if (particle.Length > 0 && knownLemmas is not null && finite.Count > 0)
            {
                (blocker, verbLemma) = SeparableVerb(state.Word, particle, finite, knownLemmas);
                if (verbLemma is not null)
                {
                    fits.Add((LinkRoleRule.R3bSeparableVerb, unit));
                }
            }
        }

        // Rule 4: reflexive pronoun or infinitive zu next to a verb.
        if ((stts == "PRF" || state.ZuInfinitive) && partners.Any(p => p.Word.Stts?.StartsWith('V') == true))
        {
            fits.Add((LinkRoleRule.R4ReflexiveOrZuUnit, unit));
        }

        // Rule 5: subject pronoun supplied by the verb ending.
        if (stts == "PPER" && SubjectPronounParent(state, partners, hebrew, sources, morphemes) is { } parent)
        {
            fits.Add((LinkRoleRule.R5SubjectPronounAdded, new Outcome(LinkRole.Head, null, parent)));
        }

        return new Evaluation(fits, blocker, verbLemma);
    }

    private static bool StartsWithParticle(string lemma, string particle)
    {
        var text = lemma.Trim().ToLowerInvariant();
        return text.Length > particle.Length && text.StartsWith(particle, StringComparison.Ordinal);
    }

    /// <summary>Rule 3b: exactly one finite verb on the morpheme and particle + its lemma is a known lemma; else the reason it is undecided.</summary>
    private static (string? Blocker, SeparableVerbLemma? Change) SeparableVerb(
        LinkRoleGermanWord particleWord,
        string particle,
        List<LinkRoleGermanWord> finite,
        IReadOnlySet<string> knownLemmas)
    {
        if (finite.Count > 1)
        {
            return ($"separable verb, several finite verbs: {string.Join(", ", finite.Select(w => w.Text))}", null);
        }

        var verb = finite[0];
        var combined = particle + verb.Lemma!.Trim().ToLowerInvariant();
        return knownLemmas.Contains(combined)
            ? (null, new SeparableVerbLemma(verb.Id, verb.PositionInVerse, verb.Lemma, combined, particleWord.Id))
            : ($"separable verb, unknown combination: {combined}", null);
    }

    private static int? SubjectPronounParent(
        LinkState state,
        List<LinkState> partners,
        bool hebrew,
        Dictionary<int, LinkRoleSourceWord> sources,
        Dictionary<int, IReadOnlyList<SourceMorpheme>> morphemes)
    {
        if (!GermanPronouns.IsNominative(state.Word) || GermanPronouns.Candidates(state.Word) is not { Count: > 0 } candidates)
        {
            return null;
        }

        var stepWordId = state.Link.StepWordId!.Value;
        SourceVerb? verb;
        if (hebrew)
        {
            if (!morphemes.TryGetValue(stepWordId, out var parsed) || parsed.Any(m => SourceMorphemes.IsPronounSuffix(m.Strongs)))
            {
                return null;
            }

            verb = SourceMorphemes.HebrewVerb(parsed.FirstOrDefault(m => string.Equals(m.Strongs, state.BaseMorpheme, StringComparison.OrdinalIgnoreCase))?.Grammar);
        }
        else
        {
            verb = sources.TryGetValue(stepWordId, out var source) ? SourceMorphemes.GreekVerb(source.Morphology) : null;
        }

        var personFits = verb switch
        {
            { IsFinite: true, Person: { } person } => candidates.Any(c => c.Person == person && (verb.Number is null || c.Number == verb.Number)),
            { IsParticiple: true } => candidates.Any(c => c.Person == 3 && (verb.Number is null || c.Number == verb.Number)),
            _ => false,
        };
        if (!personFits)
        {
            return null;
        }

        return partners
            .Where(p => IsFiniteGerman(p.Word.Stts))
            .OrderBy(p => Math.Abs(p.Word.PositionInVerse - state.Word.PositionInVerse))
            .ThenBy(p => p.Word.PositionInVerse)
            .Select(p => (int?)p.Word.Id)
            .FirstOrDefault();
    }

    /// <summary>Adds the decision or the undecided entry of a link; returns the decision.</summary>
    private static LinkRoleDecision? Resolve(
        LinkState state,
        Evaluation evaluation,
        bool hebrew,
        List<LinkRoleDecision> decisions,
        List<LinkRoleUndecided> undecided)
    {
        var fits = evaluation.Fits;
        var rules = fits.Select(f => f.Rule).ToList();
        if (state.Hint.Kind == MorphemeHintKind.Ambiguous)
        {
            undecided.Add(new LinkRoleUndecided(state.Link.Id, state.Word.Id, rules,
                $"rule 1: several morphemes fit: {string.Join(", ", state.Hint.Candidates)}"));
            return null;
        }

        if (evaluation.Blocker is not null)
        {
            undecided.Add(new LinkRoleUndecided(state.Link.Id, state.Word.Id, rules, evaluation.Blocker));
            return null;
        }

        if (fits.Count == 0)
        {
            if (hebrew && string.IsNullOrWhiteSpace(state.Link.StrongsNumber))
            {
                if (state.BaseMorpheme is not null)
                {
                    decisions.Add(new LinkRoleDecision(state.Link.Id, state.Word.Id, LinkRoleRule.R1MainMorpheme, LinkRole.Head, state.BaseMorpheme));
                }
                else
                {
                    undecided.Add(new LinkRoleUndecided(state.Link.Id, state.Word.Id, rules, state.BaseProblem ?? "no main morpheme"));
                }

                return decisions.Count > 0 && decisions[^1].LinkId == state.Link.Id ? decisions[^1] : null;
            }

            return null;
        }

        if (fits.Select(f => f.Outcome).Distinct().Count() > 1)
        {
            undecided.Add(new LinkRoleUndecided(state.Link.Id, state.Word.Id, rules,
                $"rules {string.Join(" and ", rules.Select(r => r.Code()))} fit with different outcomes: {string.Join("; ", fits.Select(Describe))}"));
            return null;
        }

        var (rule, outcome) = fits[0];
        var strongs = outcome.ParentId is not null ? null : hebrew ? outcome.Strongs : state.Link.StrongsNumber;
        var decision = new LinkRoleDecision(state.Link.Id, state.Word.Id, rule, outcome.Role, strongs, outcome.ParentId);
        decisions.Add(decision);
        return decision;
    }

    private static string Describe((LinkRoleRule Rule, Outcome Outcome) fit) => fit.Outcome.ParentId is { } parent
        ? $"{fit.Rule.Code()} → added word of {parent}"
        : $"{fit.Rule.Code()} → {fit.Outcome.Role.ToDbValue()} {fit.Outcome.Strongs}";

    private static bool IsFiniteGerman(string? stts) => stts is "VVFIN" or "VAFIN" or "VMFIN" or "VVIMP" or "VAIMP";
}
