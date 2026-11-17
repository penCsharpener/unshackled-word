using UnshackledWord.Application.Features.Lemmas;
using UnshackledWord.Domain.Models.Grammar;
using UnshackledWord.Domain.WebApi.Lemmas;

namespace UnshackledWord.Application.Features.AgentMapping;

public sealed partial class AgentProposalValidator
{
    /// <summary>
    /// Lemma rules of 06-validation.md. Entries that repeat the stored lemma are confirmations and are not checked again.
    /// <paramref name="effective"/> returns lemma, STTS and part of speech per local German id after the answer, for the separable-verb check and the soft rules.
    /// </summary>
    /// <remarks>
    /// <paramref name="lemmaMode"/> (run mode <c>lemma</c>, 06-ai-runs.md): a change of a human-edited lemma is a soft flag
    /// (<c>lemma-human-edited</c>) instead of a rejection, and a re-decided homograph is flagged (<c>homograph</c>).
    /// </remarks>
    private static List<AgentLemmaChange> ValidateLemmas(string v, AgentVerseMap verse, IReadOnlyList<AgentAnswerLemma> entries, AgentValidationContext context, AgentVerseValidation result, out Dictionary<string, (string? Lemma, string? Stts, string? Pos)> effective, bool lemmaMode = false)
    {
        var changes = new List<AgentLemmaChange>();
        var messages = result.Messages;
        var current = verse.German.ToDictionary(g => g.Key, g => (g.Value.Lemma, g.Value.Stts, g.Value.Pos));
        effective = current;
        var particles = new List<(string G, AgentAnswerLemma Entry)>();
        var checkedIds = new HashSet<string>();
        var soft = new List<(string G, AgentGermanMap Word, string Lemma, string? Stts, string? Pos)>();

        foreach (var entry in entries)
        {
            if (!verse.German.TryGetValue(entry.G, out var word))
            {
                messages.Add($"{v} {entry.G}: german word {entry.G} is not in this verse");
                continue;
            }

            if (!checkedIds.Add(entry.G))
            {
                messages.Add($"{v} {entry.G}: the lemma of this word is given twice");
                continue;
            }

            var lemma = entry.Lemma.Trim();
            var sttsChanged = entry.Stts is not null && entry.Stts != word.Stts;
            var uposChanged = entry.Upos is not null && entry.Upos != word.Pos;

            if (word.LemmaState == AgentLemmaState.Fixed && !lemmaMode)
            {
                if (lemma != word.Lemma || sttsChanged || uposChanged)
                {
                    messages.Add($"{v} {entry.G}: the lemma of this word was set by a person ('{word.Lemma}') and must not change");
                }

                continue;
            }

            if (lemma == word.Lemma && !sttsChanged && !uposChanged)
            {
                continue; // confirmation of the stored lemma
            }

            var before = messages.Count;
            var stts = entry.Stts ?? word.Stts;
            var pos = entry.Upos ?? word.Pos;

            if (AgentLemmaRules.CheckWellFormed(lemma) is { } malformed)
            {
                messages.Add($"{v} {entry.G}: {malformed.TrimEnd('.')}");
                continue;
            }

            if (entry.Upos is not null && !ElbLemmaValueMaps.IsValid(ElbLemmaValueMaps.PartOfSpeech, entry.Upos))
            {
                messages.Add($"{v} {entry.G}: unknown part of speech '{entry.Upos}'");
            }

            if (entry.Stts is not null && context.Stts is { } catalog)
            {
                var error = string.IsNullOrEmpty(pos)
                    ? catalog.Exists(entry.Stts) ? null : $"Unknown STTS tag '{entry.Stts}'."
                    : SttsRules.Check(
                        new LemmaEditFields { Lemma = lemma, PartOfSpeech = pos, Stts = entry.Stts },
                        word.Pos is null ? null : new LemmaWordRow { Lemma = word.Lemma, PartOfSpeech = word.Pos, Stts = word.Stts, HasMorphology = true },
                        catalog);
                if (error is not null)
                {
                    messages.Add($"{v} {entry.G}: {error.TrimEnd('.')}");
                }
            }

            if (AgentLemmaRules.CheckCapitalisation(lemma, stts, pos) is { } capitalisation)
            {
                messages.Add($"{v} {entry.G}: {capitalisation.TrimEnd('.')}");
            }

            if (AgentLemmaRules.CheckLegacySpelling(lemma) is { } legacy)
            {
                messages.Add($"{v} {entry.G}: {legacy.TrimEnd('.')}");
            }

            if (AgentLemmaRules.CheckContraction(word.Text, lemma, stts) is { } contraction)
            {
                messages.Add($"{v} {entry.G}: {contraction.TrimEnd('.')}");
            }

            current[entry.G] = (lemma, stts, pos);

            if (stts == "PTKVZ")
            {
                particles.Add((entry.G, entry));
            }

            if (messages.Count == before)
            {
                changes.Add(new AgentLemmaChange
                {
                    ElbWordId = word.Id,
                    Lemma = lemma,
                    Stts = entry.Stts,
                    PartOfSpeech = entry.Upos,
                    BeforeLemma = word.Lemma,
                    BeforeStts = word.Stts,
                });
                soft.Add((entry.G, word, lemma, stts, pos));

                if (lemmaMode && word.LemmaState == AgentLemmaState.Fixed)
                {
                    result.Flags.Add(new AgentFlag("lemma-human-edited", $"{v} {entry.G}: a person set the lemma '{word.Lemma}' of this word; the answer changes it to '{lemma}'", word.Id));
                }

                if (lemmaMode && AgentLemmaRules.IsHomograph(word.Text, word.Stts))
                {
                    result.Flags.Add(new AgentFlag("homograph", $"{v} {entry.G}: homograph '{word.Text}' re-decided: '{word.Lemma}' ({word.Stts}) → '{lemma}' ({stts})", word.Id));
                }
            }
        }

        foreach (var (g, entry) in particles)
        {
            SeparableParticle(v, verse, g, entry.Lemma.Trim(), current, result);
        }

        NeighbourParticles(v, verse, entries, current, result);

        var particleForms = verse.German.Where(g => current[g.Key].Stts == "PTKVZ").Select(g => g.Value.Text).ToList();
        foreach (var (g, word, lemma, stts, pos) in soft)
        {
            LemmaSoftRules(v, g, word, lemma, stts, pos, particleForms, context, result);
        }

        return changes;
    }

    /// <summary>
    /// 0018: a separable particle keeps itself as lemma (<c>aus</c>), and a finite verb of the same verse carries particle + verb (<c>ausgehen</c>).
    /// Clause boundaries are not known, the verse is the scope.
    /// </summary>
    private static void SeparableParticle(string v, AgentVerseMap verse, string g, string lemma, Dictionary<string, (string? Lemma, string? Stts, string? Pos)> effective, AgentVerseValidation result)
    {
        var others = effective.Where(e => e.Key != g).Select(e => (e.Value.Lemma, e.Value.Stts));
        if (AgentLemmaRules.CheckParticle(verse.German[g].Text, lemma, others) is { } message)
        {
            result.Messages.Add($"{v} {g}: {message}");
        }
    }

    /// <summary>0018: no non-verb lemma changed by the answer ends with the particle of a PTKVZ token next to it.</summary>
    private static void NeighbourParticles(string v, AgentVerseMap verse, IReadOnlyList<AgentAnswerLemma> entries, Dictionary<string, (string? Lemma, string? Stts, string? Pos)> effective, AgentVerseValidation result)
    {
        var order = verse.German.Keys.ToList();
        foreach (var entry in entries)
        {
            var index = order.IndexOf(entry.G);
            if (index < 0 || !effective.TryGetValue(entry.G, out var e) || e.Lemma is null || e.Lemma == verse.German[entry.G].Lemma)
            {
                continue;
            }

            var particles = Enumerable.Range(Math.Max(0, index - AgentLemmaRules.ParticleNeighbourhood), Math.Min(order.Count, index + AgentLemmaRules.ParticleNeighbourhood + 1) - Math.Max(0, index - AgentLemmaRules.ParticleNeighbourhood))
                .Where(i => i != index && effective[order[i]].Stts == "PTKVZ")
                .Select(i => verse.German[order[i]].Text.ToLowerInvariant());
            if (AgentLemmaRules.CheckAbsorbedParticle(e.Lemma, e.Stts, e.Pos, particles) is { } message)
            {
                result.Messages.Add($"{v} {entry.G}: {message}");
            }
        }
    }

    private static void LemmaSoftRules(string v, string g, AgentGermanMap word, string lemma, string? stts, string? pos, IReadOnlyList<string> particleForms, AgentValidationContext context, AgentVerseValidation result)
    {
        void Flag(string code, string text) => result.Flags.Add(new AgentFlag(code, $"{v} {g}: {text}", word.Id));

        if (!context.KnownLemmas.Contains(lemma))
        {
            Flag("lemma-unknown", $"lemma '{lemma}' is not in the lexicon (new word, please check)");
        }

        if (AgentLemmaRules.IsVerb(stts, pos))
        {
            // A separable verb lemma starts with its particle (ging ... aus = ausgehen): the finite form is compared with the lemma without it.
            var bare = particleForms.Where(p => lemma.Length > p.Length && lemma.StartsWith(p, StringComparison.OrdinalIgnoreCase)).Select(p => lemma[p.Length..]).ToList();
            if (!AgentLemmaRules.FormFitsLemma(word.Text, lemma) && !bare.Any(b => AgentLemmaRules.FormFitsLemma(word.Text, b)))
            {
                Flag("lemma-form-mismatch", $"form '{word.Text}' and lemma '{lemma}' share no prefix of two letters and are not a known irregular pair");
            }

            if (!AgentLemmaRules.LooksLikeInfinitive(lemma))
            {
                Flag("verb-lemma-ending", $"verb lemma '{lemma}' does not end in -en, -eln, -ern or -n");
            }
        }

        var human = context.HumanLemmas(word.Text.ToLowerInvariant());
        if (human.Count > 0 && !human.Contains(lemma))
        {
            Flag("lemma-differs-from-human", $"people chose {string.Join(" / ", human.Select(h => $"'{h}'"))} for the form '{word.Text}' elsewhere");
        }
    }

    /// <summary>Soft mapping rules of 06-validation.md on the link set; <paramref name="modelLinks"/> are the links the model gave itself.</summary>
    private static void AddMappingFlags(string v, AgentVerseMap verse, List<AgentLinkMap> final, List<(string G, string S)> modelLinks, Dictionary<string, (string? Lemma, string? Stts, string? Pos)> effective, AgentValidationContext context, AgentVerseValidation result)
    {
        var germanOrder = verse.German.Keys.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        var sourceOrder = verse.Source.Keys.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        var linkedSources = final.Where(l => l.S is not null).GroupBy(l => l.S!).ToDictionary(g => g.Key, g => g.Select(l => l.G).Distinct().ToList());

        var unlinked = verse.Source.Where(s => !s.Value.Exempt && !linkedSources.ContainsKey(s.Key)).Select(s => s.Key).ToList();
        if (unlinked.Count > 0)
        {
            result.Flags.Add(new AgentFlag("source-unlinked", $"{v}: no German word links to {string.Join(", ", unlinked)}"));
        }

        foreach (var (s, germans) in linkedSources.Where(kv => kv.Value.Count > MaxGermanPerSource))
        {
            result.Flags.Add(new AgentFlag("many-to-one", $"{v} {s}: {germans.Count} German words ({string.Join(", ", germans)}) share this source word", null, verse.Source[s].Id));
        }

        if (verse.German.Count >= CrossingMinWords && verse.Source.Count >= CrossingMinWords)
        {
            foreach (var (g, s) in modelLinks)
            {
                var gPos = germanOrder[g] / (double)(verse.German.Count - 1);
                var sPos = sourceOrder[s] / (double)(verse.Source.Count - 1);
                if (Math.Abs(gPos - sPos) > CrossingThreshold)
                {
                    result.Flags.Add(new AgentFlag("crossing-link", $"{v} {g}: link to {s} jumps across the verse (word {germanOrder[g] + 1} of {verse.German.Count} to word {sourceOrder[s] + 1} of {verse.Source.Count})", verse.German[g].Id, verse.Source[s].Id));
                }
            }
        }

        foreach (var (g, s) in modelLinks)
        {
            var lemma = effective[g].Lemma;
            var strongs = verse.Source[s].Strongs;
            var total = context.Stats.Total(lemma);
            if (total < context.Options.MinConfirmations || context.Stats.Count(lemma, strongs) > 0)
            {
                continue;
            }

            var competitor = verse.Source.FirstOrDefault(x => x.Key != s && context.Stats.Count(lemma, x.Value.Strongs) >= context.Options.MinConfirmations);
            if (competitor.Key is not null)
            {
                result.Flags.Add(new AgentFlag("strongs-statistics", $"{v} {g}: '{lemma}' was confirmed with {competitor.Value.Strongs} ({competitor.Key}), never with {strongs} ({s})", verse.German[g].Id, verse.Source[s].Id));
            }
        }
    }
}
