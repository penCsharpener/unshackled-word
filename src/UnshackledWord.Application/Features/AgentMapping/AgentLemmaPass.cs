using UnshackledWord.Application.Features.LinkRoles;
using UnshackledWord.Application.Features.VerseMapping;

namespace UnshackledWord.Application.Features.AgentMapping;

/// <summary>Reason codes of a lemma-mode verse (06-ai-runs.md "Only verses with work are sent"); a verse without any is not sent.</summary>
public static class AgentLemmaWork
{
    /// <summary>A link the role rules leave undecided, or the sent roles break invariant 1–4 (checks R1–R4).</summary>
    public const string Roles = "roles";

    /// <summary>A <c>PTKVZ</c> token (check R5 and the separable-verb rule).</summary>
    public const string Particle = "ptkvz";

    /// <summary>A form of the homograph list (<see cref="AgentLemmaRules.IsHomograph"/>).</summary>
    public const string Homograph = "homograph";

    /// <summary>A lemma that only LemmaSharp gave (no human edit of the word, no trusted lexicon lemma of the form).</summary>
    public const string LemmaSharp = "lemmasharp";

    /// <summary>A lemma that is empty, an 1871 spelling (check R6) or not the one the lexicon trusts for the form.</summary>
    public const string Lexicon = "lexicon";
}

/// <summary>
/// The deterministic part of run mode <c>lemma</c>, one verse at a time and without database access: every stored link is fixed and sent
/// as context with its role and morpheme. The role rules of 0018 (<see cref="LinkRoleRules.Decide"/>) fill in role and morpheme of links that
/// still have the defaults (head, main morpheme); links they leave undecided are marked open. <see cref="VerseDraft.Work"/> lists why the
/// verse needs the model; an empty list means the verse is skipped.
/// </summary>
public static class AgentLemmaPass
{
    public static VerseDraft Run(
        int chapter,
        int verse,
        string verseRef,
        string hash,
        IReadOnlyList<AgentGermanWordRow> germanWords,
        IReadOnlyList<AgentSourceWordRow> sourceWords,
        IReadOnlyList<AgentMappingRow> mappings,
        LemmaLexicon lexicon,
        bool hebrew)
    {
        var tokens = germanWords
            .Where(w => !AgentDeterministicPass.IsPunctuationOnly(w))
            .OrderBy(w => w.PositionInVerse).ThenBy(w => w.Id)
            .Select(w => new DraftGerman { Row = w, Status = AgentTokenStatus.Fixed })
            .ToList();
        foreach (var token in tokens)
        {
            var lemma = lexicon.Resolve(token.Row.Plain, token.Row.Lemma, token.Row.LemmaHumanEdited);
            token.Lemma = lemma.Lemma;
            token.LemmaState = lemma.State;
            token.Candidates = lemma.Candidates;
        }

        var source = sourceWords
            .OrderBy(w => w.Position).ThenBy(w => w.Id)
            .Select(w => new DraftSource
            {
                Row = w,
                Exempt = AgentDeterministicPass.IsArticleOrParticle(w.Morphology, hebrew),
                IsName = AgentDeterministicPass.IsName(w.Morphology, hebrew),
                Morphemes = hebrew ? HebrewMorphemes.Parse(w.ExpandedStrongTags).Select(m => new AgentMorphemeMap { Strongs = m.Strongs, IsMain = m.IsMain }).ToList() : null,
            })
            .ToList();

        var tokenIds = tokens.Select(t => t.Row.Id).ToHashSet();
        var sourceById = source.ToDictionary(s => s.Row.Id);
        var rows = StoredLinks(mappings, tokenIds, sourceById.Keys.ToHashSet());

        var roleVerse = new LinkRoleVerse(
            germanWords.FirstOrDefault()?.HebRefId ?? 0,
            hebrew ? SourceLanguage.Hebrew : SourceLanguage.Greek,
            tokens.Select(t => new LinkRoleGermanWord(t.Row.Id, t.Row.PositionInVerse, t.Row.Plain, t.Lemma ?? t.Row.Lemma, t.Row.Stts, t.Row.Person, t.Row.Number, t.Row.Gender, t.Row.Case)).ToList(),
            rows.Select((r, i) => new LinkRoleLink(i, r.ElbWordId, r.IsAddedWord ? null : r.StepWordId, LinkStrongs(r, sourceById, hebrew), r.IsAddedWord, r.GermanWordPart)).ToList(),
            source.Select(s => new LinkRoleSourceWord(s.Row.Id, s.Row.Morphology, s.Row.ExpandedStrongTags)).ToList());
        var decided = LinkRoleRules.Decide(roleVerse);
        var decisions = decided.Decisions.Where(d => !d.BecomesAddedWord).GroupBy(d => d.LinkId).ToDictionary(g => g.Key, g => g.First());
        var undecided = decided.Undecided.Select(u => u.LinkId).ToHashSet();

        var links = new List<DraftLink>();
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var hasPart = row.PartOrder is >= 1 && !string.IsNullOrWhiteSpace(row.GermanWordPart);
            if (row.IsAddedWord)
            {
                links.Add(new DraftLink { GermanId = row.ElbWordId, ParentId = row.ParentElbWordId, Fixed = true });
                continue;
            }

            var storedRole = row.Role == MappingRoles.Unit ? MappingRoles.Unit : MappingRoles.Head;
            var storedStrongs = hebrew && !string.IsNullOrWhiteSpace(row.StrongsNumber) ? row.StrongsNumber.Trim() : null;

            // A stored unit or morpheme was set by the backfill or a person; only the defaults are filled in by the rules.
            var isDefault = storedRole == MappingRoles.Head && storedStrongs is null;
            var role = storedRole;
            var strongs = storedStrongs;
            if (isDefault && decisions.TryGetValue(i, out var decision))
            {
                role = decision.Role.ToDbValue();
                strongs = hebrew ? decision.StrongsNumber : null;
            }

            links.Add(new DraftLink
            {
                GermanId = row.ElbWordId,
                SourceId = row.StepWordId,
                PartOrder = hasPart ? row.PartOrder : null,
                Part = hasPart ? row.GermanWordPart!.Trim() : null,
                Fixed = true,
                Role = role,
                Strongs = strongs,
                PartLemma = hasPart && !string.IsNullOrWhiteSpace(row.PartLemma) ? row.PartLemma.Trim() : null,
                MultiHead = row.MultiHead,
                Decided = role != storedRole || strongs != storedStrongs,
                StoredRole = storedRole,
                StoredStrongs = storedStrongs,
                Open = isDefault && undecided.Contains(i),
            });
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
            Work = Reasons(tokens, source, links, hebrew),
        };
    }

    /// <summary>
    /// Mapping modes from prompt v2 on (roadmap 0018.9): the draft of <see cref="AgentDeterministicPass"/> with roles. A link that matches a stored
    /// row takes its role, morpheme, part lemma and multi-head flag; links that still have the defaults (stored or guessed) get role and morpheme
    /// from <see cref="LinkRoleRules.Decide"/>; undecided ones stay <c>head</c> on the main morpheme. Hebrew source words get their morphemes.
    /// </summary>
    public static VerseDraft WithRoles(VerseDraft draft, IReadOnlyList<AgentMappingRow> mappings, bool hebrew)
    {
        var source = draft.Source
            .Select(s => new DraftSource
            {
                Row = s.Row,
                Exempt = s.Exempt,
                IsName = s.IsName,
                Morphemes = hebrew ? HebrewMorphemes.Parse(s.Row.ExpandedStrongTags).Select(m => new AgentMorphemeMap { Strongs = m.Strongs, IsMain = m.IsMain }).ToList() : null,
            })
            .ToList();
        var sourceById = source.ToDictionary(s => s.Row.Id);
        var stored = mappings.Where(m => !m.IsAddedWord && m.StepWordId is not null)
            .GroupBy(m => (m.ElbWordId, m.StepWordId!.Value))
            .ToDictionary(g => g.Key, g => g.First());

        AgentMappingRow? Stored(DraftLink link) =>
            link.SourceId is { } sid && link.ParentId is null && stored.TryGetValue((link.GermanId, sid), out var row) ? row : null;

        string? Strongs(DraftLink link)
        {
            if (Stored(link) is { } row)
            {
                return LinkStrongs(row, sourceById, hebrew);
            }

            return !hebrew && link.SourceId is { } id && sourceById.TryGetValue(id, out var s) ? s.Row.Strongs : null;
        }

        var roleVerse = new LinkRoleVerse(
            draft.German.FirstOrDefault()?.Row.HebRefId ?? 0,
            hebrew ? SourceLanguage.Hebrew : SourceLanguage.Greek,
            draft.German.Select(t => new LinkRoleGermanWord(t.Row.Id, t.Row.PositionInVerse, t.Row.Plain, t.Lemma ?? t.Row.Lemma, t.Row.Stts, t.Row.Person, t.Row.Number, t.Row.Gender, t.Row.Case)).ToList(),
            draft.Links.Select((l, i) => new LinkRoleLink(i, l.GermanId, l.ParentId is null ? l.SourceId : null, Strongs(l), l.ParentId is not null, l.Part)).ToList(),
            source.Select(s => new LinkRoleSourceWord(s.Row.Id, s.Row.Morphology, s.Row.ExpandedStrongTags)).ToList());
        var decisions = LinkRoleRules.Decide(roleVerse).Decisions.Where(d => !d.BecomesAddedWord).GroupBy(d => d.LinkId).ToDictionary(g => g.Key, g => g.First());

        var links = new List<DraftLink>();
        for (var i = 0; i < draft.Links.Count; i++)
        {
            var link = draft.Links[i];
            if (link.ParentId is not null || link.SourceId is null)
            {
                links.Add(link);
                continue;
            }

            var row = Stored(link);
            var storedRole = row?.Role == MappingRoles.Unit ? MappingRoles.Unit : MappingRoles.Head;
            var storedStrongs = hebrew && !string.IsNullOrWhiteSpace(row?.StrongsNumber) ? row.StrongsNumber.Trim() : null;
            var role = storedRole;
            var strongs = storedStrongs;
            if (storedRole == MappingRoles.Head && storedStrongs is null && decisions.TryGetValue(i, out var decision))
            {
                role = decision.Role.ToDbValue();
                strongs = hebrew ? decision.StrongsNumber : null;
            }

            links.Add(new DraftLink
            {
                GermanId = link.GermanId,
                SourceId = link.SourceId,
                PartOrder = link.PartOrder,
                Part = link.Part,
                Fixed = link.Fixed,
                Role = role,
                Strongs = strongs,
                PartLemma = link.Part is not null && !string.IsNullOrWhiteSpace(row?.PartLemma) ? row.PartLemma.Trim() : null,
                MultiHead = row?.MultiHead ?? false,
                Decided = role != storedRole || strongs != storedStrongs,
                StoredRole = storedRole,
                StoredStrongs = storedStrongs,
            });
        }

        return new VerseDraft
        {
            Chapter = draft.Chapter,
            Verse = draft.Verse,
            Ref = draft.Ref,
            Hash = draft.Hash,
            RemovedPunctuation = draft.RemovedPunctuation,
            German = draft.German,
            Source = source,
            Links = links,
            Work = draft.Work,
        };
    }

    /// <summary>The work reasons of a verse in a fixed order (see <see cref="AgentLemmaWork"/>).</summary>
    public static IReadOnlyList<string> Reasons(IReadOnlyList<DraftGerman> tokens, IReadOnlyList<DraftSource> source, IReadOnlyList<DraftLink> links, bool hebrew)
    {
        var reasons = new List<string>();
        if (links.Any(l => l.Open) || BreaksRoleInvariants(tokens, source, links, hebrew))
        {
            reasons.Add(AgentLemmaWork.Roles);
        }

        if (tokens.Any(t => t.Row.Stts == "PTKVZ"))
        {
            reasons.Add(AgentLemmaWork.Particle);
        }

        if (tokens.Any(t => AgentLemmaRules.IsHomograph(t.Row.Plain, t.Row.Stts)))
        {
            reasons.Add(AgentLemmaWork.Homograph);
        }

        if (tokens.Any(t => t.LemmaState is null))
        {
            reasons.Add(AgentLemmaWork.LemmaSharp);
        }

        if (tokens.Any(t => string.IsNullOrWhiteSpace(t.Row.Lemma)
                            || AgentLemmaRules.CheckLegacySpelling(t.Row.Lemma) is not null
                            || (t.LemmaState == AgentLemmaState.Hint && !string.Equals(t.Lemma, t.Row.Lemma, StringComparison.Ordinal))))
        {
            reasons.Add(AgentLemmaWork.Lexicon);
        }

        return reasons;
    }

    /// <summary>
    /// Checks R1–R4 on the sent links: a morpheme with links but no head (a unit alone, except the article part of an <c>APPRART</c>
    /// contraction), several heads without the stored multi-head flag, or one German lemma twice on a morpheme.
    /// </summary>
    private static bool BreaksRoleInvariants(IReadOnlyList<DraftGerman> tokens, IReadOnlyList<DraftSource> source, IReadOnlyList<DraftLink> links, bool hebrew)
    {
        var byId = tokens.ToDictionary(t => t.Row.Id);
        var sources = source.ToDictionary(s => s.Row.Id);

        string Morpheme(DraftLink l)
        {
            if (!hebrew || l.SourceId is not { } id || !sources.TryGetValue(id, out var s))
            {
                return string.Empty;
            }

            return l.Strongs ?? s.Morphemes?.FirstOrDefault(m => m.IsMain)?.Strongs ?? string.Empty;
        }

        foreach (var morpheme in links.Where(l => l.SourceId is not null && l.ParentId is null).GroupBy(l => (l.SourceId, Morpheme(l))))
        {
            var heads = morpheme.Where(l => l.Role != MappingRoles.Unit).ToList();
            if (heads.Count == 0 && morpheme.Any(l => !byId.TryGetValue(l.GermanId, out var t) || t.Row.Stts != MappingRoles.ContractionStts))
            {
                return true;
            }

            if (heads.Count > 1 && !heads.All(h => h.MultiHead))
            {
                return true;
            }

            var lemmas = morpheme.Select(l => byId.TryGetValue(l.GermanId, out var t) ? (l.PartLemma ?? t.Lemma ?? t.Row.Lemma)?.ToLowerInvariant() : null).Where(x => x is not null).ToList();
            if (lemmas.Count != lemmas.Distinct().Count())
            {
                return true;
            }
        }

        return false;
    }

    private static string? LinkStrongs(AgentMappingRow row, Dictionary<int, DraftSource> sources, bool hebrew)
    {
        if (hebrew)
        {
            return string.IsNullOrWhiteSpace(row.StrongsNumber) ? null : row.StrongsNumber.Trim();
        }

        return !string.IsNullOrWhiteSpace(row.StrongsNumber) ? row.StrongsNumber.Trim()
            : row.StepWordId is { } id && sources.TryGetValue(id, out var s) ? s.Row.Strongs : null;
    }

    /// <summary>Stored rows that belong to the verse: added words with a parent in the verse, links to an original word of the verse; one row per (german, source) pair.</summary>
    private static List<AgentMappingRow> StoredLinks(IReadOnlyList<AgentMappingRow> mappings, HashSet<int> tokenIds, HashSet<int> sourceIds)
    {
        var seen = new HashSet<(int, int?, bool)>();
        return mappings
            .Where(m => tokenIds.Contains(m.ElbWordId))
            .Where(m => m.IsAddedWord ? m.ParentElbWordId is { } p && p != m.ElbWordId && tokenIds.Contains(p) : m.StepWordId is { } s && sourceIds.Contains(s))
            .Where(m => seen.Add((m.ElbWordId, m.IsAddedWord ? null : m.StepWordId, m.IsAddedWord)))
            .ToList();
    }
}
