using UnshackledWord.Application.Features.AgentMapping.Lxx;
using UnshackledWord.Application.Features.Lxx;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Application.Features.AgentMapping;

/// <summary>Why an LXX word was dropped or marked (roadmap 0028.7); stored in <c>Flags.flags</c> of the proposal.</summary>
public static class LxxFlagCodes
{
    /// <summary>The model gave no Strong's number (no TBESG entry fits, e.g. a name TBESG lacks): the word stays untagged.</summary>
    public const string NoStrongs = "no-strongs";

    public const string StrongsUnknown = "strongs-unknown";
    public const string LemmaMismatch = "lemma-mismatch";
    public const string MorphInvalid = "morph-invalid";
    public const string HebUnknown = "heb-unknown";
    public const string WordMissing = "word-missing";
    public const string WordDuplicate = "word-duplicate";
    public const string WordUnknown = "word-unknown";
    public const string RowShape = "row-shape";

    /// <summary>Not <c>high</c>/<c>low</c>: the word is kept as <c>low</c>.</summary>
    public const string Confidence = "confidence";

    /// <summary>The word is kept; every other code drops it.</summary>
    public static bool Drops(string code) => code != Confidence;
}

/// <summary>A dropped or marked word; <see cref="Position"/> is the Swete position (null when the row had none).</summary>
public sealed record LxxWordFlag(string Code, string Message, int? Position = null);

/// <summary>A word tag of an LXX proposal (<c>AgentVerseProposals.Lemmas</c>): TBESG lemma, Strong's, Robinson code; <see cref="Prefilled"/> = equal to the prefill shown.</summary>
public sealed class LxxWordProposal
{
    public int LxxWordId { get; init; }
    public int Position { get; init; }
    public string Lemma { get; init; } = string.Empty;
    public string Strongs { get; init; } = string.Empty;
    public string Morph { get; init; } = string.Empty;
    public string Confidence { get; init; } = "low";
    public bool Prefilled { get; init; }
}

/// <summary>An LXX word renders a Hebrew word (<c>AgentVerseProposals.Links</c>).</summary>
public sealed class LxxLinkProposal
{
    public int LxxWordId { get; init; }
    public int Position { get; init; }
    public int StepHebrewWordId { get; init; }

    /// <summary>The Hebrew number of the package (1.. in reading order; the STEP position for ordinary verses).</summary>
    public int HebNumber { get; init; }
}

/// <summary><c>AgentVerseProposals.Flags</c> of an LXX proposal: the unit key (the open-proposal index reads textForm/subverse) and the dropped words.</summary>
public sealed class LxxProposalFlags
{
    public string TextForm { get; init; } = string.Empty;
    public int Subverse { get; init; }
    public int LxxVerseId { get; init; }
    public int? HebRefId { get; init; }
    public List<LxxWordFlag> Flags { get; init; } = [];
}

public sealed class LxxVerseProposal
{
    public required LxxVerseMap Unit { get; init; }
    public List<LxxWordProposal> Words { get; } = [];
    public List<LxxLinkProposal> Links { get; } = [];
    public string? Note { get; set; }
}

public sealed class LxxVerseValidation
{
    public string V { get; init; } = string.Empty;
    public string? Ref { get; init; }

    /// <summary>Messages reject the verse (it is sent back to the model); word problems are <see cref="Flags"/> and only drop the word.</summary>
    public List<string> Messages { get; } = [];

    public List<LxxWordFlag> Flags { get; } = [];
    public LxxVerseProposal? Proposal { get; set; }

    public string Status => Messages.Count > 0 ? AgentVerseStatus.Rejected : Flags.Count > 0 ? AgentVerseStatus.Flagged : AgentVerseStatus.Accepted;
}

/// <summary>
/// The <c>lxx</c> branch (roadmap 0028.7, 05-tagging.md "Pass 2"). Per word: the Strong's is in TBESG and its lemma matches the answer's lemma, the
/// code passes <see cref="GreekMorphologyCode"/>, every Hebrew number exists in the unit's Hebrew verse, and the word is answered exactly once. A word that
/// fails is dropped and recorded in the flags; the rest of the verse is kept. A verse is rejected only as a whole: answered twice, not readable, or no
/// word left.
/// </summary>
public sealed partial class AgentProposalValidator
{
    public IReadOnlyList<LxxVerseValidation> ValidateLxx(AgentPackageMap package, LxxAgentAnswer answer, LxxLexicon lexicon)
    {
        var results = new List<LxxVerseValidation>();
        var verses = package.LxxVerses ?? [];

        foreach (var (v, unit) in verses.OrderBy(k => k.Key.Length).ThenBy(k => k.Key, StringComparer.Ordinal))
        {
            var entries = answer.Verses.Where(a => a.V == v).ToList();
            if (entries.Count == 0)
            {
                continue;
            }

            var result = new LxxVerseValidation { V = v, Ref = unit.Ref };
            if (entries.Count > 1)
            {
                result.Messages.Add($"{v}: the verse is answered {entries.Count} times; send it once.");
            }
            else if (entries[0].ParseErrors.Count > 0)
            {
                result.Messages.AddRange(entries[0].ParseErrors.Select(e => $"{v}: {e}"));
            }
            else
            {
                ValidateLxxVerse(v, unit, entries[0], lexicon, result);
            }

            results.Add(result);
        }

        foreach (var extra in answer.Verses.Where(a => !verses.ContainsKey(a.V)))
        {
            var result = new LxxVerseValidation { V = extra.V.Length == 0 ? "?" : extra.V };
            result.Messages.Add(extra.V.Length == 0 ? "a verse entry without a v field." : $"{extra.V}: not a verse of this package.");
            results.Add(result);
        }

        return results;
    }

    private static void ValidateLxxVerse(string v, LxxVerseMap unit, LxxAnswerVerse answer, LxxLexicon lexicon, LxxVerseValidation result)
    {
        var proposal = new LxxVerseProposal { Unit = unit, Note = Truncate(answer.Note) };
        var flags = result.Flags;

        foreach (var row in answer.Words.Where(r => r.Position is null))
        {
            flags.Add(new LxxWordFlag(LxxFlagCodes.RowShape, $"{v}: {row.ShapeError}"));
        }

        var byPosition = answer.Words.Where(r => r.Position is not null).GroupBy(r => r.Position!.Value).OrderBy(g => g.Key);
        foreach (var group in byPosition)
        {
            var position = group.Key;
            var at = $"{v} #{position}";
            if (!unit.Words.TryGetValue(position, out var wordId))
            {
                flags.Add(new LxxWordFlag(LxxFlagCodes.WordUnknown, $"{at}: the unit has no word at this position.", position));
                continue;
            }

            if (group.Count() > 1)
            {
                flags.Add(new LxxWordFlag(LxxFlagCodes.WordDuplicate, $"{at}: answered {group.Count()} times; the word is left untagged.", position));
                continue;
            }

            var row = group.Single();
            if (row.ShapeError is not null)
            {
                flags.Add(new LxxWordFlag(LxxFlagCodes.RowShape, $"{at}: {row.ShapeError}", position));
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.Strongs))
            {
                flags.Add(new LxxWordFlag(LxxFlagCodes.NoStrongs, $"{at}: no Strong's number given; the word is left untagged.", position));
                continue;
            }

            if (LxxLexicon.NormalizeStrongs(row.Strongs) is not { } strongs || !lexicon.Contains(strongs))
            {
                flags.Add(new LxxWordFlag(LxxFlagCodes.StrongsUnknown, $"{at}: Strong's '{row.Strongs}' is not in TBESG.", position));
                continue;
            }

            if (lexicon.MatchLemma(strongs, row.Lemma) is not { } lemma)
            {
                var expected = string.Join(" / ", lexicon.Entries(strongs).Select(e => e.Lemma).Distinct());
                flags.Add(new LxxWordFlag(LxxFlagCodes.LemmaMismatch, $"{at}: lemma '{row.Lemma}' is not the TBESG lemma of {strongs} ({expected}).", position));
                continue;
            }

            var morph = row.Morph!.ToUpperInvariant();
            if (!GreekMorphologyCode.TryParse(morph, out _, out var error))
            {
                flags.Add(new LxxWordFlag(LxxFlagCodes.MorphInvalid, $"{at}: morphology '{row.Morph}' is not a Robinson code: {error}", position));
                continue;
            }

            var unknownHeb = row.Heb.Where(h => !unit.Hebrew.ContainsKey(h)).Distinct().ToList();
            if (unknownHeb.Count > 0)
            {
                var why = unit.Hebrew.Count == 0 ? "the unit has no Hebrew verse" : $"the Hebrew verse has 1..{unit.Hebrew.Count}";
                flags.Add(new LxxWordFlag(LxxFlagCodes.HebUnknown, $"{at}: Hebrew {string.Join(", ", unknownHeb)} does not exist ({why}).", position));
                continue;
            }

            var confidence = row.Confidence;
            if (confidence is not ("high" or "low"))
            {
                flags.Add(new LxxWordFlag(LxxFlagCodes.Confidence, $"{at}: confidence '{row.Confidence}' is not high or low; kept as low.", position));
                confidence = "low";
            }

            var prefilled = unit.Prefill.TryGetValue(position, out var pre) && pre.Strongs == strongs && pre.Morph == morph;
            proposal.Words.Add(new LxxWordProposal { LxxWordId = wordId, Position = position, Lemma = lemma, Strongs = strongs, Morph = morph, Confidence = confidence, Prefilled = prefilled });
            foreach (var h in row.Heb.Distinct().Order())
            {
                proposal.Links.Add(new LxxLinkProposal { LxxWordId = wordId, Position = position, StepHebrewWordId = unit.Hebrew[h], HebNumber = h });
            }
        }

        var answered = answer.Words.Where(r => r.Position is not null).Select(r => r.Position!.Value).ToHashSet();
        foreach (var position in unit.Words.Keys.Where(p => !answered.Contains(p)).Order())
        {
            flags.Add(new LxxWordFlag(LxxFlagCodes.WordMissing, $"{v} #{position}: the word is not answered; it is left untagged.", position));
        }

        if (proposal.Words.Count == 0 && unit.Words.Count > 0)
        {
            // Nothing usable: send the verse back with the reasons instead of storing an empty proposal.
            result.Messages.Add($"{v}: no word passed the checks.");
            result.Messages.AddRange(flags.Select(f => f.Message));
            flags.Clear();
            return;
        }

        result.Proposal = proposal;
    }

    private static string? Truncate(string? note) =>
        string.IsNullOrWhiteSpace(note) ? null : note.Length <= MaxNoteLength ? note : note[..MaxNoteLength];
}
