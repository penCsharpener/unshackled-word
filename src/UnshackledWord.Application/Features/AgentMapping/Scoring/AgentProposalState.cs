using System.Text.Json;
using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;

namespace UnshackledWord.Application.Features.AgentMapping.Scoring;

/// <summary>A stored proposal as the scorer needs it (the JSON columns of <c>AgentVerseProposals</c> still serialized).</summary>
public sealed class ScoredProposal
{
    public long Id { get; init; }
    public int BibleBookId { get; init; }
    public int Chapter { get; init; }
    public int Verse { get; init; }
    public string Status { get; init; } = string.Empty;
    public string LinksJson { get; init; } = "[]";
    public string? LemmasJson { get; init; }
    public string? RolesJson { get; init; }
    public string? FlagsJson { get; init; }
}

/// <summary>
/// The state a proposal stands for. Proposals are diffs against the stored state ("silence keeps"): <c>Links</c> is the full link set of the verse
/// (fixed items, untouched hints and the model's own entries), <c>Lemmas</c> lists only the changed words and <c>Roles</c> only the changed
/// roles/morphemes. So the result starts from the stored state of the verse, replaces the link set, then applies the lemma and role changes.
/// A link without role information keeps the role, morpheme and part lemma stored for the same (german, source) pair, like a legacy save.
/// </summary>
public static class AgentProposalState
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static ReferenceVerse Apply(AgentVerseSnapshot baseline, ScoredProposal proposal)
    {
        var state = Clone(baseline.State);
        var language = state.Language;

        var links = Deserialize<VerseMappingLink>(proposal.LinksJson);
        if (links.Count > 0)
        {
            var stored = baseline.State.Links.ToDictionary(l => $"{l.G}>{l.SRef}.{l.S}#{l.Part}", l => l);
            state.Links = [];
            state.Added = [];

            foreach (var link in links)
            {
                if (!baseline.PositionById.TryGetValue(link.ElbWordId, out var g))
                {
                    continue;
                }

                if (link.IsAddedWord)
                {
                    if (link.ParentElbWordId is { } parentId && baseline.PositionById.TryGetValue(parentId, out var parent))
                    {
                        state.Added.Add(new ReferenceAdded { G = g, Parent = parent });
                    }

                    continue;
                }

                if (link.StepWordId is not { } stepId || !baseline.SourceById.TryGetValue(stepId, out var source))
                {
                    continue;
                }

                stored.TryGetValue($"{g}>{source.Ref}.{source.Pos}#{link.PartOrder}", out var before);
                baseline.MainMorphemeById.TryGetValue(stepId, out var main);

                state.Links.Add(new ReferenceLink
                {
                    G = g,
                    SRef = source.Ref,
                    S = source.Pos,
                    SText = before?.SText,
                    Role = NormalizeRole(link.Role) ?? before?.Role ?? "head",
                    Morpheme = AgentVerseSnapshots.ResolveMorpheme(language, link.StrongsNumber ?? before?.Morpheme, main),
                    Part = link.PartOrder,
                    PartText = link.GermanWordPart,
                    PartLemma = link.PartLemma ?? before?.PartLemma,
                });
            }
        }

        foreach (var change in Deserialize<AgentLemmaChange>(proposal.LemmasJson))
        {
            if (baseline.PositionById.TryGetValue(change.ElbWordId, out var pos) && state.Words.FirstOrDefault(w => w.Pos == pos) is { } word)
            {
                word.Lemma = change.Lemma;
                if (!string.IsNullOrEmpty(change.Stts))
                {
                    word.Stts = change.Stts;
                }
            }
        }

        foreach (var change in Deserialize<AgentRoleChange>(proposal.RolesJson))
        {
            ApplyRole(baseline, state, change);
        }

        state.Links = [.. state.Links.OrderBy(l => l.G).ThenBy(l => l.SRef).ThenBy(l => l.S).ThenBy(l => l.Part)];

        return state;
    }

    /// <summary><c>multi-head</c> is a head that is allowed to share its morpheme (flag, not a role).</summary>
    public static string? NormalizeRole(string? role) => role switch
    {
        null or "" => null,
        "multi-head" => "head",
        _ => role,
    };

    public static List<string> FlagCodes(string? flagsJson) => AgentProposalFlagsJson.Codes(flagsJson);

    private static void ApplyRole(AgentVerseSnapshot baseline, ReferenceVerse state, AgentRoleChange change)
    {
        if (!baseline.PositionById.TryGetValue(change.ElbWordId, out var g))
        {
            return;
        }

        if (change.AddedParentElbWordId is { } parentId && baseline.PositionById.TryGetValue(parentId, out var parent))
        {
            state.Links.RemoveAll(l => l.G == g);
            state.Added.RemoveAll(a => a.G == g);
            state.Added.Add(new ReferenceAdded { G = g, Parent = parent });

            return;
        }

        int? sRef = null;
        int? s = null;
        if (change.StepWordId is { } stepId && baseline.SourceById.TryGetValue(stepId, out var source))
        {
            (sRef, s) = (source.Ref, source.Pos);
        }

        foreach (var link in state.Links.Where(l => l.G == g && (s is null || (l.SRef == sRef && l.S == s)) && (change.PartOrder is null || l.Part == change.PartOrder)))
        {
            if (NormalizeRole(change.Role) is { } role)
            {
                link.Role = role;
            }

            if (change.StrongsNumber is not null && state.Language == "hebrew")
            {
                link.Morpheme = AgentVerseSnapshots.ResolveMorpheme(state.Language, change.StrongsNumber, null);
            }

            if (change.PartLemma is not null)
            {
                link.PartLemma = change.PartLemma;
            }
        }
    }

    private static List<T> Deserialize<T>(string? json) =>
        string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<T>>(json, Json) ?? [];

    public static ReferenceVerse Clone(ReferenceVerse verse) => new()
    {
        Book = verse.Book,
        Chapter = verse.Chapter,
        Verse = verse.Verse,
        Language = verse.Language,
        Text = verse.Text,
        Words = [.. verse.Words.Select(w => new ReferenceWord { Pos = w.Pos, Text = w.Text, Lemma = w.Lemma, Stts = w.Stts })],
        Links = [.. verse.Links.Select(l => new ReferenceLink { G = l.G, SRef = l.SRef, S = l.S, SText = l.SText, Role = l.Role, Morpheme = l.Morpheme, Part = l.Part, PartText = l.PartText, PartLemma = l.PartLemma, MultiHead = l.MultiHead })],
        Added = [.. verse.Added.Select(a => new ReferenceAdded { G = a.G, Parent = a.Parent })],
    };
}
