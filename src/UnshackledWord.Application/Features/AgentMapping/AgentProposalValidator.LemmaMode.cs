using UnshackledWord.Application.Features.VerseMapping;
using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;

namespace UnshackledWord.Application.Features.AgentMapping;

/// <summary>
/// One role, morpheme, multi-head, part-lemma or added-word change of a lemma-mode proposal (<c>AgentVerseProposals.Roles</c>), in real ids.
/// <see cref="Source"/> says who decided it: <c>model</c> or <c>rules</c> (the deterministic role rules filled in a default).
/// </summary>
public sealed class AgentRoleChange
{
    public int ElbWordId { get; init; }
    public int? StepWordId { get; init; }
    public string? Role { get; init; }
    public string? StrongsNumber { get; init; }
    public bool MultiHead { get; init; }
    public int? PartOrder { get; init; }
    public string? PartLemma { get; init; }
    public string? BeforeRole { get; init; }
    public string? BeforeStrongsNumber { get; init; }
    public bool BeforeMultiHead { get; init; }
    public string? BeforePartLemma { get; init; }

    /// <summary>The linked word became an added word with this parent (subject pronoun, 04-roles-and-counting.md).</summary>
    public int? AddedParentElbWordId { get; init; }

    public string Source { get; init; } = AgentRoleChangeSources.Model;
}

public static class AgentRoleChangeSources
{
    public const string Model = "model";
    public const string Rules = "rules";
}

public sealed partial class AgentProposalValidator
{
    /// <summary>Role value of a lemma answer that accepts several heads on one morpheme (invariant 1 exception, flagged <c>multi-head</c>).</summary>
    public const string MultiHeadRole = "multi-head";

    /// <summary>
    /// Run mode <c>lemma</c> (06-ai-runs.md): links are fixed. The answer may set role and morpheme of a fixed link, turn a linked subject
    /// pronoun into an added word, set part lemmas and change lemmas. Hard: a new or removed link, a role on an added word, a morpheme that is
    /// not in the source word, invariants 1–3 (role-aware <see cref="SaveVerseMappingValidator"/>, unless <c>multi-head</c>), the lemma rules.
    /// Soft: human-edited lemma changed, lemma not in the lexicon, homograph re-decided, pronoun turned into an added word, multi-head.
    /// </summary>
    private void ValidateLemmaVerse(AgentPackageMap package, string v, AgentVerseMap verse, AgentAnswerVerse answer, AgentValidationContext context, AgentVerseValidation result)
    {
        var messages = result.Messages;
        foreach (var error in answer.ParseErrors)
        {
            messages.Add($"{v}: {error}");
        }

        if (answer.Links.Count > 0 || answer.Parts.Count > 0 || answer.Unmapped.Count > 0)
        {
            messages.Add($"{v}: links are fixed in lemma mode and must not be added or removed; answer only lemmas, roles, added and partLemmas");
        }

        var hebrew = package.Language == "hebrew";
        var current = verse.Fixed.Select(Clone).ToList();
        var touchedByModel = new HashSet<(string G, string? S)>();
        var roleSeen = new HashSet<(string, string)>();

        bool KnownGerman(string id, string owner)
        {
            if (verse.German.ContainsKey(id))
            {
                return true;
            }

            messages.Add($"{v} {owner}: german word {id} is not in this verse");
            return false;
        }

        foreach (var entry in answer.Roles)
        {
            if (!KnownGerman(entry.G, entry.G))
            {
                continue;
            }

            if (!verse.Source.TryGetValue(entry.S, out var source))
            {
                messages.Add($"{v} {entry.G}: source {entry.S} is not in this verse");
                continue;
            }

            var link = current.FirstOrDefault(l => l.G == entry.G && l.S == entry.S);
            if (link is null)
            {
                messages.Add(current.Any(l => l.G == entry.G && l.Parent is not null) || answer.Added.Any(a => a.G == entry.G)
                    ? $"{v} {entry.G}: an added word has no role"
                    : $"{v} {entry.G}: {entry.G}→{entry.S} is not a link; links are fixed, a role can only be set on an existing link");
                continue;
            }

            if (!roleSeen.Add((entry.G, entry.S)))
            {
                messages.Add($"{v} {entry.G}: the role of {entry.G}→{entry.S} is given twice");
                continue;
            }

            if (!ApplyRole(v, entry.G, entry.S, source, entry.Role, entry.Strongs, hebrew, link, messages))
            {
                continue;
            }

            touchedByModel.Add((link.G, link.S));
        }

        foreach (var item in answer.Added)
        {
            if (!KnownGerman(item.G, item.G) || !KnownGerman(item.Parent, item.G))
            {
                continue;
            }

            var linked = current.Where(l => l.G == item.G && l.S is not null).ToList();
            if (linked.Count == 0)
            {
                if (!current.Any(l => l.G == item.G && l.Parent == item.Parent))
                {
                    messages.Add($"{v} {item.G}: only a linked subject pronoun can become an added word; links are fixed otherwise");
                }

                continue; // echo of an existing added word
            }

            var word = verse.German[item.G];
            if (word.Stts != "PPER")
            {
                messages.Add($"{v} {item.G}: only a subject pronoun (PPER) can become an added word, '{word.Text}' is {word.Stts ?? "untagged"}");
                continue;
            }

            if (item.Parent == item.G)
            {
                messages.Add($"{v} {item.G}: a word cannot be its own parent");
                continue;
            }

            if (roleSeen.Any(r => r.Item1 == item.G))
            {
                messages.Add($"{v} {item.G}: an added word has no role; drop the roles entry or the added entry");
                continue;
            }

            current.RemoveAll(l => l.G == item.G);
            current.Add(new AgentLinkMap { G = item.G, Parent = item.Parent });
            touchedByModel.Add((item.G, null));
            result.Flags.Add(new AgentFlag("pronoun-added", $"{v} {item.G}: '{word.Text}' is no longer linked, it becomes an added word of {item.Parent} ('{verse.German[item.Parent].Text}')", word.Id));
        }

        var addedWords = current.Where(l => l.Parent is not null).Select(l => l.G).ToHashSet();
        foreach (var link in current.Where(l => l.Parent is not null && addedWords.Contains(l.Parent!)))
        {
            messages.Add($"{v} {link.G}: parent {link.Parent} is itself an added word; chains are not allowed");
        }

        foreach (var entry in answer.PartLemmas)
        {
            if (!KnownGerman(entry.G, entry.G))
            {
                continue;
            }

            var parts = current.Where(l => l.G == entry.G && l.S is not null && l.PartOrder == entry.Order && l.Part is not null).ToList();
            if (parts.Count == 0)
            {
                messages.Add($"{v} {entry.G}: the word has no part {entry.Order}; parts are fixed in lemma mode");
                continue;
            }

            var lemma = entry.Lemma.Trim();
            var problem = PartLemmaProblem(lemma);
            if (problem is not null)
            {
                messages.Add($"{v} {entry.G}: part {entry.Order}: {problem.TrimEnd('.')}");
                continue;
            }

            foreach (var part in parts)
            {
                part.PartLemma = lemma;
                touchedByModel.Add((part.G, part.S));
            }
        }

        var lemmaChanges = ValidateLemmas(v, verse, answer.Lemmas, context, result, out _, lemmaMode: true);

        ValidateWithSaveRules(v, verse, current, result, roleAware: true, hebrew);

        if (messages.Count > 0)
        {
            return;
        }

        AddMultiHeadFlags(v, verse, current, hebrew, result);

        var note = answer.Note?.Trim();
        if (note is { Length: > MaxNoteLength })
        {
            note = note[..MaxNoteLength];
            result.Flags.Add(new AgentFlag("note-truncated", $"{v}: note was longer than {MaxNoteLength} characters and was cut"));
        }

        var proposal = new AgentVerseProposal { Chapter = verse.Chapter, Verse = verse.Verse, Note = string.IsNullOrEmpty(note) ? null : note };
        proposal.Links.AddRange(current.Select(l => ToRoleLink(verse, l, hebrew)));
        proposal.Lemmas.AddRange(lemmaChanges);
        proposal.Roles.AddRange(RoleChanges(verse, current, touchedByModel));
        result.Proposal = proposal;
    }

    /// <summary>
    /// Checks one role answer (<c>head</c>/<c>unit</c>/<c>multi-head</c>, optional morpheme) of the link <paramref name="g"/>→<paramref name="s"/>
    /// and writes it into <paramref name="link"/>; false (with a message) when the role or the morpheme is not allowed. Hebrew: the morpheme must be
    /// one of the source word's morphemes (braces are ignored); Greek: it may only repeat the word's Strong's.
    /// </summary>
    private static bool ApplyRole(string v, string g, string s, AgentSourceMap source, string role, string? strongsAnswer, bool hebrew, AgentLinkMap link, List<string> messages)
    {
        if (role is not (MappingRoles.Head or MappingRoles.Unit or MultiHeadRole))
        {
            messages.Add($"{v} {g}: role must be '{MappingRoles.Head}', '{MappingRoles.Unit}' or '{MultiHeadRole}', got '{role}'");
            return false;
        }

        var strongs = strongsAnswer?.Trim().Trim('{', '}');
        if (!string.IsNullOrEmpty(strongs))
        {
            if (hebrew)
            {
                var morphemes = source.Morphemes ?? [];
                var match = morphemes.FirstOrDefault(m => string.Equals(m.Strongs, strongs, StringComparison.OrdinalIgnoreCase));
                if (HebrewMorphemes.IsPunctuation(strongs) || match is null)
                {
                    messages.Add($"{v} {g}: morpheme {strongs} is not in source {s} ({string.Join(", ", morphemes.Select(m => m.Strongs))})");
                    return false;
                }

                link.Strongs = match.Strongs;
            }
            else if (!string.Equals(strongs, source.Strongs, StringComparison.OrdinalIgnoreCase))
            {
                messages.Add($"{v} {g}: a Greek link takes the Strong's of its word ({source.Strongs}), not {strongs}");
                return false;
            }
        }

        link.Role = role == MappingRoles.Unit ? MappingRoles.Unit : MappingRoles.Head;
        link.MultiHead = role == MultiHeadRole || (role == MappingRoles.Head && link.MultiHead);
        return true;
    }

    /// <summary>Checks a part lemma (well-formed, length, no 1871 spelling); the problem text or null.</summary>
    private static string? PartLemmaProblem(string lemma) =>
        AgentLemmaRules.CheckWellFormed(lemma)
        ?? (lemma.Length > MappingRoles.MaxPartLemmaLength ? $"part lemma is longer than {MappingRoles.MaxPartLemmaLength} characters." : null)
        ?? AgentLemmaRules.CheckLegacySpelling(lemma);

    /// <summary>Several head links on one morpheme that were not stored as multi-head: flag <c>multi-head</c> (the maintainer confirms).</summary>
    private static void AddMultiHeadFlags(string v, AgentVerseMap verse, List<AgentLinkMap> final, bool hebrew, AgentVerseValidation result)
    {
        foreach (var morpheme in final.Where(l => l.S is not null && l.Role != MappingRoles.Unit).GroupBy(l => (l.S, Morpheme: MorphemeOf(verse, l, hebrew)))
                     .Where(g => g.Count() > 1 && g.Any(l => !verse.Fixed.Concat(verse.Hints).Any(f => f.G == l.G && f.S == l.S && f.MultiHead))))
        {
            result.Flags.Add(new AgentFlag("multi-head", $"{v} {morpheme.Key.S}: {morpheme.Count()} head links ({string.Join(", ", morpheme.Select(l => l.G))}) accepted as multi-head; please confirm", null, verse.Source[morpheme.Key.S!].Id));
        }
    }

    /// <summary>Role, morpheme, multi-head and part-lemma differences between the stored links (package) and the final set, plus links that became added words.</summary>
    private static IEnumerable<AgentRoleChange> RoleChanges(AgentVerseMap verse, List<AgentLinkMap> final, HashSet<(string G, string? S)> touchedByModel)
    {
        foreach (var stored in verse.Fixed.Where(l => l.S is not null))
        {
            var beforeRole = stored.StoredRole ?? stored.Role ?? MappingRoles.Head;
            var beforeStrongs = stored.Decided ? stored.StoredStrongs : stored.Strongs;
            var now = final.FirstOrDefault(l => l.G == stored.G && l.S == stored.S);
            if (now is null)
            {
                var added = final.FirstOrDefault(l => l.G == stored.G && l.Parent is not null);
                yield return new AgentRoleChange
                {
                    ElbWordId = verse.German[stored.G].Id,
                    StepWordId = verse.Source[stored.S!].Id,
                    BeforeRole = beforeRole,
                    BeforeStrongsNumber = beforeStrongs,
                    BeforeMultiHead = stored.MultiHead,
                    AddedParentElbWordId = added?.Parent is { } p ? verse.German[p].Id : null,
                };
                continue;
            }

            var role = now.Role ?? MappingRoles.Head;
            if (role == beforeRole && now.Strongs == beforeStrongs && now.MultiHead == stored.MultiHead && now.PartLemma == stored.PartLemma)
            {
                continue;
            }

            yield return new AgentRoleChange
            {
                ElbWordId = verse.German[stored.G].Id,
                StepWordId = verse.Source[stored.S!].Id,
                Role = role,
                StrongsNumber = now.Strongs,
                MultiHead = now.MultiHead,
                PartOrder = now.PartOrder,
                PartLemma = now.PartLemma,
                BeforeRole = beforeRole,
                BeforeStrongsNumber = beforeStrongs,
                BeforeMultiHead = stored.MultiHead,
                BeforePartLemma = stored.PartLemma,
                Source = touchedByModel.Contains((stored.G, stored.S)) ? AgentRoleChangeSources.Model : AgentRoleChangeSources.Rules,
            };
        }
    }

    /// <summary>A link of the final lemma-mode set as a role-aware save link: role always set, morpheme only for Hebrew.</summary>
    private static VerseMappingLink ToRoleLink(AgentVerseMap verse, AgentLinkMap link, bool hebrew)
    {
        var real = ToRealLink(verse, link);
        if (link.S is null)
        {
            real.Role = MappingRoles.Head;
            return real;
        }

        real.Role = link.Role == MappingRoles.Unit ? MappingRoles.Unit : MappingRoles.Head;
        real.StrongsNumber = hebrew ? link.Strongs : null;
        real.PartLemma = link.Part is null ? null : link.PartLemma;
        real.AllowMultiHead = link.MultiHead && real.Role == MappingRoles.Head ? true : null;
        return real;
    }

    private static string MorphemeOf(AgentVerseMap verse, AgentLinkMap link, bool hebrew)
    {
        if (!hebrew || link.S is null || !verse.Source.TryGetValue(link.S, out var source))
        {
            return string.Empty;
        }

        return link.Strongs ?? source.Morphemes?.FirstOrDefault(m => m.IsMain)?.Strongs ?? string.Empty;
    }

    private static AgentLinkMap Clone(AgentLinkMap l) => new()
    {
        G = l.G,
        S = l.S,
        Parent = l.Parent,
        PartOrder = l.PartOrder,
        Part = l.Part,
        Role = l.Role,
        Strongs = l.Strongs,
        PartLemma = l.PartLemma,
        MultiHead = l.MultiHead,
        Decided = l.Decided,
        StoredRole = l.StoredRole,
        StoredStrongs = l.StoredStrongs,
        Open = l.Open,
    };
}
