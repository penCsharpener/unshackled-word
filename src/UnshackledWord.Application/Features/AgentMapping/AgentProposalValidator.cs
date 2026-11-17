using System.Text.RegularExpressions;
using FluentValidation.Results;
using UnshackledWord.Application.Features.Lemmas;
using UnshackledWord.Application.Features.VerseMapping;
using UnshackledWord.Domain.Models.Grammar;
using UnshackledWord.Domain.WebApi.Elberfelder.GetVerseMapping;
using UnshackledWord.Domain.WebApi.Lemmas;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Application.Features.AgentMapping;

/// <summary>What the validator needs besides the package: lexicon facts that only the database knows, loaded by the caller.</summary>
public sealed class AgentValidationContext
{
    public SttsCatalog? Stts { get; init; }
    public IReadOnlySet<string> KnownLemmas { get; init; } = new HashSet<string>();
    public Func<string, IReadOnlySet<string>> HumanLemmas { get; init; } = _ => new HashSet<string>();
    public ConfirmedStats Stats { get; init; } = ConfirmedStats.Empty;
    public AgentMappingOptions Options { get; init; } = new();
}

public sealed record AgentFlag(string Code, string Message, int? ElbWordId = null, int? StepWordId = null);

public sealed class AgentLemmaChange
{
    public int ElbWordId { get; init; }
    public string Lemma { get; init; } = string.Empty;
    public string? Stts { get; init; }
    public string? PartOfSpeech { get; init; }
    public string? BeforeLemma { get; init; }
    public string? BeforeStts { get; init; }
}

public sealed class AgentPartRecord
{
    public int ElbWordId { get; init; }
    public int StepWordId { get; init; }
    public int Order { get; init; }
    public string Text { get; init; } = string.Empty;
}

/// <summary>The proposal of one accepted verse in real database ids: the full link set (fixed + kept hints + the model's links), lemma changes, parts, note.</summary>
public sealed class AgentVerseProposal
{
    public int Chapter { get; init; }
    public int Verse { get; init; }
    public List<VerseMappingLink> Links { get; } = [];
    public List<AgentLemmaChange> Lemmas { get; } = [];
    public List<AgentPartRecord> Parts { get; } = [];

    /// <summary>Run mode <c>lemma</c>: role/morpheme/part-lemma changes (<c>AgentVerseProposals.Roles</c>); the links carry the roles.</summary>
    public List<AgentRoleChange> Roles { get; } = [];

    public string? Note { get; set; }
}

public sealed class AgentVerseValidation
{
    public string V { get; init; } = string.Empty;
    public string? Ref { get; init; }
    public List<string> Messages { get; } = [];
    public List<AgentFlag> Flags { get; } = [];
    public AgentVerseProposal? Proposal { get; set; }

    public string Status => Messages.Count > 0 ? AgentVerseStatus.Rejected : Flags.Count > 0 ? AgentVerseStatus.Flagged : AgentVerseStatus.Accepted;
}

/// <summary>
/// Hard and soft rules of 06-validation.md, per verse, on local ids. Hard rules reject the verse (messages like <c>v1 g3: source s9 belongs to v2</c>),
/// soft rules store the proposal with a flag. Mapping rules that live saves already have are not repeated: after the ids are mapped back
/// through the package, <see cref="SaveVerseMappingValidator"/> checks the link set. Pure: all database facts come in through the context.
/// </summary>
public sealed partial class AgentProposalValidator
{
    public const int MaxNoteLength = 200;
    private const int MaxGermanPerSource = 3;
    private const double CrossingThreshold = 0.6;
    private const int CrossingMinWords = 8;

    public IReadOnlyList<AgentVerseValidation> Validate(AgentPackageMap package, AgentAnswer answer, AgentValidationContext context)
    {
        var results = new List<AgentVerseValidation>();
        var seen = new HashSet<string>();

        foreach (var (v, verse) in package.Verses.OrderBy(k => k.Key.Length).ThenBy(k => k.Key, StringComparer.Ordinal))
        {
            var entries = answer.Verses.Where(a => a.V == v).ToList();
            var result = new AgentVerseValidation { V = v, Ref = verse.Ref };
            seen.Add(v);

            // A verse the answer does not mention is not judged: an agent may send only the verses it is resubmitting.
            if (entries.Count == 0)
            {
                continue;
            }

            if (entries.Count > 1)
            {
                result.Messages.Add($"{v}: the verse is answered {entries.Count} times; send it once.");
            }
            else if (package.Mode == AgentModes.Lemma)
            {
                ValidateLemmaVerse(package, v, verse, entries[0], context, result);
            }
            else
            {
                ValidateVerse(package, v, verse, entries[0], context, result);
            }

            results.Add(result);
        }

        foreach (var extra in answer.Verses.Where(a => !seen.Contains(a.V)))
        {
            var result = new AgentVerseValidation { V = extra.V.Length == 0 ? "?" : extra.V };
            result.Messages.Add(extra.V.Length == 0 ? "a verse entry without a v field." : $"{extra.V}: not a verse of this package.");
            results.Add(result);
        }

        return results;
    }

    private void ValidateVerse(AgentPackageMap package, string v, AgentVerseMap verse, AgentAnswerVerse answer, AgentValidationContext context, AgentVerseValidation result)
    {
        var messages = result.Messages;
        foreach (var error in answer.ParseErrors)
        {
            messages.Add($"{v}: {error}");
        }

        string Where(string id) => $"{v} {id}";

        string OtherVerseOf(string id, bool german) =>
            package.Verses.Where(kv => kv.Key != v && (german ? kv.Value.German.ContainsKey(id) : kv.Value.Source.ContainsKey(id))).Select(kv => kv.Key).FirstOrDefault() ?? string.Empty;

        // Ids are mapped back only through the package; an id the package never handed out is an invented one.
        bool CheckGerman(string id, string owner, string what)
        {
            if (verse.German.ContainsKey(id))
            {
                return true;
            }

            var other = OtherVerseOf(id, german: true);
            messages.Add(other.Length > 0
                ? $"{Where(owner)}: {what} {id} belongs to {other}"
                : $"{Where(owner)}: {what} {id} is not in this package");
            return false;
        }

        bool CheckSource(string id, string owner)
        {
            if (verse.Source.ContainsKey(id))
            {
                return true;
            }

            var other = OtherVerseOf(id, german: false);
            messages.Add(other.Length > 0
                ? $"{Where(owner)}: source {id} belongs to {other}"
                : $"{Where(owner)}: source {id} is not in this package");
            return false;
        }

        var fixedPairs = verse.Fixed.Select(f => (f.G, Other: f.S ?? f.Parent)).ToHashSet();
        var fixedTokens = verse.German.Where(g => g.Value.Status == AgentTokenStatus.Fixed).Select(g => g.Key).ToHashSet();

        // Entries that only repeat a fixed item are harmless; any other entry on a fixed token is a change.
        bool TouchesFixed(string g, string? other, string kind)
        {
            if (!fixedTokens.Contains(g))
            {
                return false;
            }

            if (kind != "unmapped" && fixedPairs.Contains((g, other)))
            {
                return true; // echo of the fixed item: skipped, no error
            }

            var current = string.Join(", ", verse.Fixed.Where(f => f.G == g).Select(f => $"{f.G}→{f.S ?? f.Parent}"));
            messages.Add($"{Where(g)}: this word is fixed by the algorithm ({current}) and must not change");
            return true;
        }

        // A word named in an entry that failed is not reported a second time as "not covered".
        var attempted = new HashSet<string>();
        var links = new List<(string G, string S)>();
        var roles = package.Roles;
        var hebrew = package.Language == "hebrew";
        var modelLinks = new List<AgentLinkMap>();
        foreach (var link in answer.Links)
        {
            var okG = CheckGerman(link.G, link.G, "german word");
            if (okG)
            {
                attempted.Add(link.G);
            }

            if (!roles && (link.Role is not null || link.Strongs is not null))
            {
                messages.Add($"{Where(link.G)}: links entry must be [german, source]; this prompt version has no roles");
                continue;
            }

            var okS = CheckSource(link.S, link.G);
            if (okG && okS && !TouchesFixed(link.G, link.S, "link"))
            {
                // Prompt v2 (0018.9): the link carries its role and morpheme; without one it is the head of the main morpheme.
                var linkMap = new AgentLinkMap { G = link.G, S = link.S, Role = MappingRoles.Head };
                if (roles && link.Role is not null && !ApplyRole(v, link.G, link.S, verse.Source[link.S], link.Role, link.Strongs, hebrew, linkMap, messages))
                {
                    continue;
                }

                links.Add((link.G, link.S));
                modelLinks.Add(linkMap);
            }
        }

        var added = new List<(string G, string Parent)>();
        foreach (var item in answer.Added)
        {
            var okG = CheckGerman(item.G, item.G, "german word");
            if (okG)
            {
                attempted.Add(item.G);
            }

            var okP = CheckGerman(item.Parent, item.G, "parent");
            if (okG && okP && !TouchesFixed(item.G, item.Parent, "added"))
            {
                added.Add((item.G, item.Parent));
            }
        }

        var parts = new List<AgentAnswerPart>();
        foreach (var part in answer.Parts)
        {
            var okG = CheckGerman(part.G, part.G, "german word");
            if (okG)
            {
                attempted.Add(part.G);
            }

            if (part.PartLemma is { } partLemma)
            {
                var problem = roles ? PartLemmaProblem(partLemma.Trim()) : "parts entry must be [german, source, order, text]; this prompt version has no part lemmas.";
                if (problem is not null)
                {
                    messages.Add($"{Where(part.G)}: part {part.Order}: {problem.TrimEnd('.')}");
                    continue;
                }
            }

            var okS = CheckSource(part.S, part.G);
            if (okG && okS && !TouchesFixed(part.G, part.S, "part"))
            {
                parts.Add(part);
            }
        }

        var unmapped = new List<AgentAnswerUnmapped>();
        foreach (var item in answer.Unmapped)
        {
            if (CheckGerman(item.G, item.G, "german word"))
            {
                attempted.Add(item.G);
                if (!TouchesFixed(item.G, null, "unmapped"))
                {
                    unmapped.Add(item);
                }
            }
        }

        var touched = links.Select(l => l.G).Concat(added.Select(a => a.G)).Concat(parts.Select(p => p.G)).Concat(unmapped.Select(u => u.G)).ToHashSet();

        foreach (var (g, word) in verse.German)
        {
            if (word.Status == AgentTokenStatus.Open && !touched.Contains(g) && !attempted.Contains(g))
            {
                messages.Add($"{Where(g)}: open word '{word.Text}' has no link, added word or unmapped reason");
            }
        }

        foreach (var g in links.Select(l => l.G).Concat(parts.Select(p => p.G)).Intersect(added.Select(a => a.G)).Distinct())
        {
            messages.Add($"{Where(g)}: a word cannot be both linked and added");
        }

        // The full set the proposal stands for: fixed items, hints the model did not touch, then the model's own entries.
        var final = new List<AgentLinkMap>();
        final.AddRange(verse.Fixed);
        final.AddRange(verse.Hints.Where(h => !touched.Contains(h.G)));
        final.AddRange(modelLinks);
        final.AddRange(added.Select(a => new AgentLinkMap { G = a.G, Parent = a.Parent }));
        final.AddRange(parts.Select(p => new AgentLinkMap
        {
            G = p.G, S = p.S, PartOrder = p.Order, Part = p.Text, Role = roles ? MappingRoles.Head : null, PartLemma = string.IsNullOrWhiteSpace(p.PartLemma) ? null : p.PartLemma.Trim(),
        }));

        var addedWords = final.Where(l => l.Parent is not null).Select(l => l.G).ToHashSet();
        foreach (var link in final.Where(l => l.Parent is not null && addedWords.Contains(l.Parent!)))
        {
            messages.Add($"{Where(link.G)}: parent {link.Parent} is itself an added word; chains are not allowed");
        }

        ValidateWithSaveRules(v, verse, final, result, roleAware: roles, hebrew);

        var lemmaChanges = ValidateLemmas(v, verse, answer.Lemmas, context, result, out var effective);

        if (messages.Count == 0)
        {
            AddMappingFlags(v, verse, final, links, effective, context, result);
            if (roles)
            {
                AddMultiHeadFlags(v, verse, final, hebrew, result);
            }

            foreach (var item in unmapped)
            {
                result.Flags.Add(new AgentFlag("unmapped", $"{Where(item.G)}: left unmapped: {item.Reason}", verse.German[item.G].Id));
            }

            var note = answer.Note?.Trim();
            if (note is { Length: > MaxNoteLength })
            {
                note = note[..MaxNoteLength];
                result.Flags.Add(new AgentFlag("note-truncated", $"{v}: note was longer than {MaxNoteLength} characters and was cut"));
            }

            var proposal = new AgentVerseProposal { Chapter = verse.Chapter, Verse = verse.Verse, Note = string.IsNullOrEmpty(note) ? null : note };
            proposal.Links.AddRange(final.Select(l => roles ? ToRoleLink(verse, l, hebrew) : ToRealLink(verse, l)));
            proposal.Lemmas.AddRange(lemmaChanges);
            proposal.Parts.AddRange(parts.Select(p => new AgentPartRecord { ElbWordId = verse.German[p.G].Id, StepWordId = verse.Source[p.S].Id, Order = p.Order, Text = p.Text }));
            result.Proposal = proposal;
        }
    }

    private static VerseMappingLink ToRealLink(AgentVerseMap verse, AgentLinkMap link) => new()
    {
        ElbWordId = verse.German[link.G].Id,
        StepWordId = link.S is null ? null : verse.Source[link.S].Id,
        IsAddedWord = link.Parent is not null,
        ParentElbWordId = link.Parent is null ? null : verse.German[link.Parent].Id,
        PartOrder = link.PartOrder,
        GermanWordPart = link.Part,
    };

    /// <summary>
    /// Maps the link set to database ids and runs <see cref="SaveVerseMappingValidator"/> on it; its messages come back in local ids.
    /// <paramref name="roleAware"/> (lemma mode): the links carry roles and morphemes and the one-head rule (invariant 1) is enforced, like a
    /// role-aware save (05-data-model.md "Save contract").
    /// </summary>
    private static void ValidateWithSaveRules(string v, AgentVerseMap verse, List<AgentLinkMap> final, AgentVerseValidation result, bool roleAware = false, bool hebrew = false)
    {
        var real = final.Select(l => roleAware ? ToRoleLink(verse, l, hebrew) : ToRealLink(verse, l)).ToList();
        var validation = new SaveVerseMappingValidator().Validate(new VerseMappingSaveInput
        {
            Links = real,
            GermanWordIds = verse.German.Values.Select(g => g.Id).ToHashSet(),
            OriginalWordIds = verse.Source.Values.Select(s => s.Id).ToHashSet(),
            GermanWordTexts = verse.German.Values.ToDictionary(g => g.Id, g => g.Text),
            HebrewMorphemes = roleAware && hebrew
                ? verse.Source.Values.ToDictionary(s => s.Id, s => (IReadOnlyList<VerseMappingMorpheme>)(s.Morphemes ?? []).Select(m => new VerseMappingMorpheme { Strongs = m.Strongs, IsMain = m.IsMain }).ToList())
                : null,
            OriginalWordStrongs = roleAware && !hebrew ? verse.Source.Values.ToDictionary(s => s.Id, s => s.Strongs) : null,
            GermanWordStts = roleAware ? verse.German.Values.ToDictionary(g => g.Id, g => g.Stts) : null,
            EnforceSingleHead = roleAware,
        });

        foreach (var failure in Distinct(validation))
        {
            result.Messages.Add(ToLocal(v, verse, failure));
        }
    }

    private static IEnumerable<string> Distinct(ValidationResult validation) => validation.Errors.Select(e => e.ErrorMessage).Distinct();

    [GeneratedRegex(@"^Link \d+ \(word (\d+)\): (.*)$")]
    private static partial Regex LinkMessage();

    [GeneratedRegex(@"^Word (\d+) \(.*?\): (.*)$")]
    private static partial Regex WordMessage();

    [GeneratedRegex(@"(stepWordId|parentElbWordId) (\d+)")]
    private static partial Regex IdInText();

    [GeneratedRegex(@"^Original word (\d+)(?: morpheme (\S+))?: (.*)$")]
    private static partial Regex OriginalMessage();

    [GeneratedRegex(@"\b(words?) (\d+(?:, \d+)*)")]
    private static partial Regex WordIdsInText();

    /// <summary>Rewrites a <see cref="SaveVerseMappingValidator"/> message: database ids become local ids, field names become words a model understands.</summary>
    internal static string ToLocal(string v, AgentVerseMap verse, string message)
    {
        string G(string id) => verse.German.FirstOrDefault(g => g.Value.Id.ToString() == id).Key ?? $"word {id}";
        string S(string id) => verse.Source.FirstOrDefault(s => s.Value.Id.ToString() == id).Key ?? $"source {id}";

        var prefix = v;
        var text = message;
        if (LinkMessage().Match(message) is { Success: true } link)
        {
            prefix = $"{v} {G(link.Groups[1].Value)}";
            text = link.Groups[2].Value;
        }
        else if (WordMessage().Match(message) is { Success: true } word)
        {
            prefix = $"{v} {G(word.Groups[1].Value)}";
            text = word.Groups[2].Value;
        }
        else if (OriginalMessage().Match(message) is { Success: true } original)
        {
            prefix = original.Groups[2].Success ? $"{v} {S(original.Groups[1].Value)} morpheme {original.Groups[2].Value}" : $"{v} {S(original.Groups[1].Value)}";
            text = WordIdsInText().Replace(original.Groups[3].Value, m => $"{m.Groups[1].Value} {string.Join(", ", m.Groups[2].Value.Split(", ").Select(G))}");
        }

        text = IdInText().Replace(text, m => m.Groups[1].Value == "stepWordId" ? $"source {S(m.Groups[2].Value)}" : $"parent {G(m.Groups[2].Value)}");
        text = text.Replace("(elbWordId, stepWordId)", "(german, source)").Replace("parentElbWordId", "parent").Replace("stepWordId", "source")
            .Replace("elbWordId", "german word").Replace("germanWordPart", "part text").Replace("partOrder", "part order");
        return $"{prefix}: {text.TrimEnd('.')}";
    }
}
