namespace UnshackledWord.Application.Features.AgentMapping.Lxx;

/// <summary>One LXX unit (<c>LxxVerses</c> row) of a package range, Rahlfs numbering.</summary>
public sealed class LxxAgentUnitRow
{
    public int Id { get; set; }
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public int Subverse { get; set; }

    /// <summary><c>og</c>/<c>th</c> in Daniel, else empty.</summary>
    public string TextForm { get; set; } = string.Empty;

    /// <summary>The Hebrew verse (Elberfelder counting) TVTMS maps the unit to; null for additions.</summary>
    public int? HebRefId { get; set; }

    public string MapKind { get; set; } = string.Empty;

    /// <summary>Swete's reading order.</summary>
    public int SortOrder { get; set; }
}

/// <summary>One Swete word with its current tag row (any status or source; null when untagged).</summary>
public sealed class LxxAgentWordRow
{
    public int Id { get; set; }
    public int LxxVerseId { get; set; }
    public int Position { get; set; }
    public string Greek { get; set; } = string.Empty;
    public string? TagLemma { get; set; }
    public string? TagStrongs { get; set; }
    public string? TagMorphology { get; set; }
    public string? TagStatus { get; set; }
    public string? TagSource { get; set; }
}

/// <summary>The units of a range, their words, and the STEP Hebrew words of their <c>HebRefId</c>s (<see cref="AgentSourceWordRow.RefId"/> = HebRefId).</summary>
public sealed class LxxAgentRangeData
{
    public IReadOnlyList<LxxAgentUnitRow> Units { get; init; } = [];
    public IReadOnlyList<LxxAgentWordRow> Words { get; init; } = [];
    public IReadOnlyList<AgentSourceWordRow> HebrewWords { get; init; } = [];
}

/// <summary>A TBESG (<c>StepStrongsLexicon</c>, Greek) entry: the Strong's key as LXX tags store it (<c>G0746</c>, <c>G1487G</c>), lemma and gloss.</summary>
public sealed record LxxLexiconEntry(string Strongs, string Lemma, string Gloss);

/// <summary>How often an NT running-text form (<c>StepGreekWords.Greek</c>) carries a Strong's number.</summary>
public sealed record LxxNtFormCount(string Greek, string Strongs, int Count);

/// <summary>Reads of the LXX agent mode (roadmap 0028.7), implemented in Infrastructure. Nothing here writes.</summary>
public interface ILxxAgentDataRepository
{
    /// <summary>The units with a Rahlfs reference of one book, (chapter, verse) from..to inclusive (all subverses and text forms), in Swete order.</summary>
    Task<LxxAgentRangeData> GetRangeAsync(int bibleBookId, int fromChapter, int fromVerse, int toChapter, int toVerse, CancellationToken token = default);

    /// <summary>The same data for given <c>LxxVerses</c> ids (the stale check at submit).</summary>
    Task<LxxAgentRangeData> GetUnitsAsync(IReadOnlyCollection<int> lxxVerseIds, CancellationToken token = default);

    /// <summary>Every Greek TBESG entry (about 11k rows).</summary>
    Task<IReadOnlyList<LxxLexiconEntry>> GetLexiconAsync(CancellationToken token = default);

    /// <summary>NT forms with their Strong's numbers and counts (grouped in SQL).</summary>
    Task<IReadOnlyList<LxxNtFormCount>> GetNtFormsAsync(CancellationToken token = default);

    /// <summary><c>LxxVerses</c> ids that already have a proposal of the run (<c>Flags.lxxVerseId</c>): resume skips them.</summary>
    Task<IReadOnlySet<int>> GetProposedUnitIdsAsync(int runId, CancellationToken token = default);
}
