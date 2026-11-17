namespace UnshackledWord.Application.Features.AgentMapping;

/// <summary>One German word of the range with its stored lemma row (joined by HebRefId + PositionInVerse, not by id).</summary>
public sealed class AgentGermanWordRow
{
    public int Id { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public int HebRefId { get; set; }
    public int PositionInVerse { get; set; }
    public string Text { get; set; } = string.Empty;

    /// <summary>The word without punctuation; empty for a punctuation-only token.</summary>
    public string Plain { get; set; } = string.Empty;

    public string? Lemma { get; set; }
    public string? PartOfSpeech { get; set; }
    public string? Stts { get; set; }

    /// <summary>The current lemma equals the result of an edit by a non-AI user (<c>Elb1871MorphologyEdits</c>).</summary>
    public bool LemmaHumanEdited { get; set; }

    /// <summary>Elb1871Morphology person/number/gender/case (the link-role rules of roadmap 0018 need them).</summary>
    public string? Person { get; set; }

    public string? Number { get; set; }
    public string? Gender { get; set; }
    public string? Case { get; set; }
}

public sealed class AgentSourceWordRow
{
    public int Id { get; set; }

    /// <summary>HebRefId (LxxRefId) of the original verse the word belongs to.</summary>
    public int RefId { get; set; }

    public int Position { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? Lemma { get; set; }
    public string? Strongs { get; set; }
    public string? Gloss { get; set; }
    public string? Morphology { get; set; }

    /// <summary>Hebrew: <c>ExpandedStrongTags</c> (the morphemes of the word, roadmap 0018); null for Greek.</summary>
    public string? ExpandedStrongTags { get; set; }
}

public sealed class AgentMappingRow
{
    public int ElbWordId { get; set; }
    public int? StepWordId { get; set; }
    public bool IsAddedWord { get; set; }
    public int? ParentElbWordId { get; set; }
    public string? GermanWordPart { get; set; }
    public int? PartOrder { get; set; }
    public int? CreatedByUserId { get; set; }

    /// <summary>The author has the role <c>ai</c>.</summary>
    public bool AuthorIsAi { get; set; }

    /// <summary>Roadmap 0018: <c>head</c> / <c>unit</c>.</summary>
    public string Role { get; set; } = "head";

    /// <summary>Greek: the link's Strong's; Hebrew: the morpheme the link renders (null = main morpheme).</summary>
    public string? StrongsNumber { get; set; }

    public string? PartLemma { get; set; }
    public bool MultiHead { get; set; }

    /// <summary>Written by a human user (maintainer save in the editor): the only rows that count as confirmed.</summary>
    public bool IsConfirmed => CreatedByUserId is not null && !AuthorIsAi;
}

public sealed class AgentRangeData
{
    public IReadOnlyList<AgentGermanWordRow> GermanWords { get; init; } = [];
    public IReadOnlyList<AgentSourceWordRow> SourceWords { get; init; } = [];
    public IReadOnlyList<AgentMappingRow> Mappings { get; init; } = [];
}

/// <summary>One lemma of a lower-cased German form: how often it occurs and whether a human decided it.</summary>
public sealed class AgentFormLemmaRow
{
    public string Form { get; set; } = string.Empty;
    public string Lemma { get; set; } = string.Empty;
    public int Count { get; set; }

    /// <summary>True: counts words of this form whose lemma was set by a human edit; false: LemmaSharp rows.</summary>
    public bool Trusted { get; set; }
}

/// <summary>Confirmed links (human authors only) of a lower-cased German lemma to a Strongs number, for the statistics rules.</summary>
public sealed class AgentConfirmedPairRow
{
    public string Lemma { get; set; } = string.Empty;
    public string Strongs { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>Reads for the package builder and the validator (implemented in Infrastructure).</summary>
public interface IAgentDataRepository
{
    /// <summary>German words, original words and mapping rows of the verses <c>from</c>..<c>to</c> (inclusive) of one book.</summary>
    Task<AgentRangeData> GetRangeAsync(int bibleBookId, int fromChapter, int fromVerse, int toChapter, int toVerse, CancellationToken token = default);

    /// <summary>All lemmas (trusted human-edited ones and LemmaSharp ones, with counts) of the given lower-cased forms.</summary>
    Task<IReadOnlyList<AgentFormLemmaRow>> GetFormLemmasAsync(IReadOnlyCollection<string> forms, CancellationToken token = default);

    /// <summary>Counts of confirmed (human) links per German lemma (lower case) and Strongs number.</summary>
    Task<IReadOnlyList<AgentConfirmedPairRow>> GetConfirmedPairsAsync(bool hebrew, IReadOnlyCollection<string> lemmasLower, CancellationToken token = default);

    /// <summary>The subset of <paramref name="lemmas"/> that occurs as a lemma in the German morphology.</summary>
    Task<IReadOnlySet<string>> GetKnownLemmasAsync(IReadOnlyCollection<string> lemmas, CancellationToken token = default);
}
