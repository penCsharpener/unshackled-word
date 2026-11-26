using System.Text.Json;
using System.Text.Json.Serialization;

namespace UnshackledWord.Application.Features.AgentMapping;

/// <summary>Link status of a German token (04-work-package.md).</summary>
public static class AgentTokenStatus
{
    public const string Fixed = "fixed";
    public const string Hint = "hint";
    public const string Open = "open";
}

/// <summary>Lemma status of a German token: <c>fixed</c> (human edit of this exact word), <c>hint</c> (trusted lexicon form), <c>null</c> = only a LemmaSharp candidate.</summary>
public static class AgentLemmaState
{
    public const string Fixed = "fixed";
    public const string Hint = "hint";
}

/// <summary>
/// Everything about a package that the validator needs and the model must not see, stored in <c>AgentPackages.LocalIdMap</c>: local id to database id,
/// the snapshot of what was decided deterministically, and per verse a hash of the word ids. Ids are mapped back only through this table.
/// </summary>
public sealed class AgentPackageMap
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    public int Version { get; set; } = 1;
    public string Language { get; set; } = string.Empty;
    public string Mode { get; set; } = AgentModes.Redo;
    public int BookId { get; set; }

    /// <summary>
    /// Mapping modes from prompt version 2 on (roadmap 0018.9): fixed links and hints carry role and morpheme, Hebrew source words their
    /// morphemes, and the answer's links/parts may carry role, morpheme and part lemma. Lemma mode is always role-aware.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Roles { get; set; }

    /// <summary>Keyed by the local verse id (<c>v1</c>). Empty in mode <c>lxx-tag</c>.</summary>
    public Dictionary<string, AgentVerseMap> Verses { get; set; } = [];

    /// <summary>Mode <c>lxx-tag</c> (roadmap 0028.7): the LXX units by local verse id; null in the other modes.</summary>
    public Dictionary<string, LxxVerseMap>? LxxVerses { get; set; }

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    public static AgentPackageMap FromJson(string json) => JsonSerializer.Deserialize<AgentPackageMap>(json, Json) ?? throw new InvalidOperationException("Empty LocalIdMap.");
}

public sealed class AgentVerseMap
{
    public string Ref { get; set; } = string.Empty;
    public int Chapter { get; set; }
    public int Verse { get; set; }

    /// <summary>Hash of the German and original word ids of the verse when the package was built.</summary>
    public string Hash { get; set; } = string.Empty;

    /// <summary>Local id (<c>g1</c>) to German word, in reading order.</summary>
    public Dictionary<string, AgentGermanMap> German { get; set; } = [];

    /// <summary>Local id (<c>s1</c>) to original word, in reading order.</summary>
    public Dictionary<string, AgentSourceMap> Source { get; set; } = [];

    /// <summary>Decided by the algorithm; the model must not change them. Merged into the proposal.</summary>
    public List<AgentLinkMap> Fixed { get; set; } = [];

    /// <summary>Algorithm guesses; they stand unless the model gives its own link for the German word.</summary>
    public List<AgentLinkMap> Hints { get; set; } = [];
}

public sealed class AgentGermanMap
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? Lemma { get; set; }
    public string? Pos { get; set; }
    public string? Stts { get; set; }

    /// <see cref="AgentTokenStatus"/>
    public string Status { get; set; } = AgentTokenStatus.Open;

    /// <see cref="AgentLemmaState"/>
    public string? LemmaState { get; set; }
}

public sealed class AgentSourceMap
{
    public int Id { get; set; }
    public string? Strongs { get; set; }

    /// <summary>An article or particle: not flagged when no German word links to it.</summary>
    public bool Exempt { get; set; }

    /// <summary>Lemma mode, Hebrew: the morphemes of the word (punctuation left out); null otherwise.</summary>
    public List<AgentMorphemeMap>? Morphemes { get; set; }
}

public sealed class AgentMorphemeMap
{
    public string Strongs { get; set; } = string.Empty;
    public bool IsMain { get; set; }
}

/// <summary>A link or added word in local ids. <see cref="S"/> null + <see cref="Parent"/> set = added word.</summary>
public sealed class AgentLinkMap
{
    public string G { get; set; } = string.Empty;
    public string? S { get; set; }
    public string? Parent { get; set; }
    public int? PartOrder { get; set; }
    public string? Part { get; set; }

    /// <summary>Lemma mode: the role of the link as sent (stored role, or the deterministic decision); null = head.</summary>
    public string? Role { get; set; }

    /// <summary>Lemma mode, Hebrew: the morpheme as sent (null = main morpheme).</summary>
    public string? Strongs { get; set; }

    /// <summary>Lemma mode: the stored part lemma of a part link.</summary>
    public string? PartLemma { get; set; }

    /// <summary>Lemma mode: the stored multi-head flag.</summary>
    public bool MultiHead { get; set; }

    /// <summary>Lemma mode: the role rules changed role or morpheme of this link; <see cref="StoredRole"/>/<see cref="StoredStrongs"/> hold the stored values.</summary>
    public bool Decided { get; set; }

    /// <summary>Lemma mode: the stored role (<c>head</c>/<c>unit</c>); for the stale check and the role diff.</summary>
    public string? StoredRole { get; set; }

    /// <summary>Lemma mode, Hebrew: the stored morpheme column (null = main morpheme).</summary>
    public string? StoredStrongs { get; set; }

    /// <summary>Lemma mode: the deterministic role rules leave this link undecided; the model must decide it.</summary>
    public bool Open { get; set; }
}

/// <summary>One LXX unit of an <c>lxx-tag</c> package: database ids behind the positions the model answers with, and the prefills it was shown.</summary>
public sealed class LxxVerseMap
{
    public string Ref { get; set; } = string.Empty;
    public int LxxVerseId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public int Subverse { get; set; }
    public string TextForm { get; set; } = string.Empty;
    public int? HebRefId { get; set; }

    /// <summary>Hash of the LXX and Hebrew word ids when the package was built (<see cref="Lxx.LxxAgentPackageBuilder.VerseHash"/>).</summary>
    public string Hash { get; set; } = string.Empty;

    /// <summary>Swete position to <c>LxxWords.Id</c>.</summary>
    public Dictionary<int, int> Words { get; set; } = [];

    /// <summary>Hebrew number (1.. in reading order, as in the package) to <c>StepHebrewWords.Id</c>; empty for additions.</summary>
    public Dictionary<int, int> Hebrew { get; set; } = [];

    /// <summary>The prefill shown per position (Strong's and morphology), to mark answers that keep it.</summary>
    public Dictionary<int, LxxPrefillMap> Prefill { get; set; } = [];
}

public sealed class LxxPrefillMap
{
    public string Strongs { get; set; } = string.Empty;
    public string Morph { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
