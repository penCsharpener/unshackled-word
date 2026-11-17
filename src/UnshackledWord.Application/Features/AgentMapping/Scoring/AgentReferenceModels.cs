using System.Text.Json;
using System.Text.Json.Serialization;

namespace UnshackledWord.Application.Features.AgentMapping.Scoring;

/// <summary>
/// The frozen reference set (<c>assets/agent-reference/reference-vN.json</c>, roadmap 0018.10): verses the maintainer reviewed by hand, each with
/// the expected links (role, Hebrew morpheme, part, part lemma), the lemma and STTS tag of every German word and the added words. Words are keyed by
/// reading position (<see cref="ReferenceWord.Pos"/>), source words by original verse + position, so the file survives a re-seed. The same
/// <see cref="ReferenceVerse"/> shape describes the state a run leaves behind (the scorer compares two of them).
/// </summary>
public sealed class ReferenceSet
{
    public const int CurrentFormat = 1;

    public int Format { get; set; } = CurrentFormat;

    /// <summary>File version name (<c>v1</c>); the file is <c>reference-v1.json</c>.</summary>
    public string Version { get; set; } = "v1";

    public DateTimeOffset FrozenAt { get; set; }
    public List<ReferenceVerse> Verses { get; set; } = [];
}

public sealed class ReferenceVerse
{
    public int Book { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }

    /// <summary><c>greek</c> or <c>hebrew</c>.</summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>The verse as read (review aid, not compared).</summary>
    public string? Text { get; set; }

    /// <summary>Every German word with letters (punctuation tokens are left out).</summary>
    public List<ReferenceWord> Words { get; set; } = [];

    public List<ReferenceLink> Links { get; set; } = [];
    public List<ReferenceAdded> Added { get; set; } = [];

    [JsonIgnore]
    public string Ref => $"{Book} {Chapter}:{Verse}";

    [JsonIgnore]
    public (int Book, int Chapter, int Verse) Key => (Book, Chapter, Verse);
}

public sealed class ReferenceWord
{
    /// <summary><c>PositionInVerse</c> of the German word.</summary>
    public int Pos { get; set; }

    public string Text { get; set; } = string.Empty;
    public string? Lemma { get; set; }
    public string? Stts { get; set; }
}

public sealed class ReferenceLink
{
    /// <summary>German word position.</summary>
    public int G { get; set; }

    /// <summary>Original verse (BBCCCVVV) of the source word.</summary>
    public int SRef { get; set; }

    /// <summary>Source word position in that verse.</summary>
    public int S { get; set; }

    /// <summary>Source word text (review aid, not compared).</summary>
    public string? SText { get; set; }

    /// <summary><c>head</c> or <c>unit</c>.</summary>
    public string Role { get; set; } = "head";

    /// <summary>Hebrew: the morpheme the link renders (the main one spelled out); null for Greek.</summary>
    public string? Morpheme { get; set; }

    /// <summary>1-based part order of a compound word link, else null.</summary>
    public int? Part { get; set; }

    public string? PartText { get; set; }
    public string? PartLemma { get; set; }

    /// <summary>Stored multi-head flag (informational, not scored).</summary>
    public bool? MultiHead { get; set; }

    [JsonIgnore]
    public string Key => $"{G}>{SRef}.{S}#{Part}";
}

public sealed class ReferenceAdded
{
    public int G { get; set; }
    public int Parent { get; set; }

    [JsonIgnore]
    public string Key => $"{G}^{Parent}";
}

public static class ReferenceSetFile
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    public const string Directory = "agent-reference";

    public static string FileName(string version) => $"reference-{version}.json";

    public static string Serialize(ReferenceSet set) => JsonSerializer.Serialize(set, Json) + "\n";

    public static ReferenceSet Parse(string json) => JsonSerializer.Deserialize<ReferenceSet>(json, Json) ?? throw new InvalidDataException("The reference file is empty.");

    /// <summary>
    /// The 10 verses that are worked examples in the prompts (0016 02-decisions.md "Example verses"). They may not be in a reference set: a model
    /// that sees the answer in its prompt would inflate the scores. Book ids: Gen 1, Rt 8, Mk 41.
    /// </summary>
    public static readonly IReadOnlySet<(int Book, int Chapter, int Verse)> PromptExampleVerses = new HashSet<(int, int, int)>
    {
        (41, 1, 1), (41, 1, 6), (41, 1, 8), (41, 1, 29), (41, 1, 35),
        (1, 1, 1), (1, 1, 3), (8, 1, 4), (8, 1, 5), (8, 1, 22),
    };

    public static bool IsPromptExample(int book, int chapter, int verse) => PromptExampleVerses.Contains((book, chapter, verse));

    /// <summary>Problems that make a file unusable as a reference set: empty, duplicates, prompt examples, bad roles, broken keys.</summary>
    public static List<string> Validate(ReferenceSet set)
    {
        var errors = new List<string>();
        if (set.Format != ReferenceSet.CurrentFormat)
        {
            errors.Add($"Unknown reference format {set.Format} (expected {ReferenceSet.CurrentFormat}).");
        }

        if (set.Verses.Count == 0)
        {
            errors.Add("The reference set has no verses.");
        }

        var seen = new HashSet<(int, int, int)>();
        foreach (var verse in set.Verses)
        {
            var name = $"{verse.Book} {verse.Chapter}:{verse.Verse}";
            if (!seen.Add(verse.Key))
            {
                errors.Add($"{name}: listed twice.");
            }

            if (IsPromptExample(verse.Book, verse.Chapter, verse.Verse))
            {
                errors.Add($"{name}: is a worked example of the prompts and must not be in the reference set.");
            }

            if (verse.Language is not ("greek" or "hebrew"))
            {
                errors.Add($"{name}: language must be greek or hebrew.");
            }

            var positions = verse.Words.Select(w => w.Pos).ToList();
            if (positions.Distinct().Count() != positions.Count)
            {
                errors.Add($"{name}: a word position occurs twice.");
            }

            var known = positions.ToHashSet();
            foreach (var link in verse.Links)
            {
                if (!known.Contains(link.G))
                {
                    errors.Add($"{name}: link names word {link.G}, which is not a word of the verse.");
                }

                if (link.Role is not ("head" or "unit"))
                {
                    errors.Add($"{name}: link {link.G}: role must be head or unit.");
                }
            }

            if (verse.Links.Select(l => l.Key).Distinct().Count() != verse.Links.Count)
            {
                errors.Add($"{name}: a link occurs twice.");
            }

            foreach (var added in verse.Added)
            {
                if (!known.Contains(added.G) || !known.Contains(added.Parent))
                {
                    errors.Add($"{name}: added word {added.G} or its parent {added.Parent} is not a word of the verse.");
                }
            }
        }

        return errors;
    }
}
