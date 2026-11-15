namespace UnshackledWord.Application.Features.Revision;

/// <summary>Active variant of one (verse, level); only what the write path needs.</summary>
public sealed class RevisionActiveRow
{
    public int Id { get; set; }
    public int Version { get; set; }
}

public sealed class RevisionBaseWordRow
{
    public int PositionInVerse { get; set; }
    public string? PlainWord { get; set; }
}

/// <summary>An active variant as the verse read shows it. <see cref="CreatedBy"/> is the author's display name, NULL for seed imports.</summary>
public sealed class RevisionVariantRow
{
    public int RevisionVerseId { get; set; }
    public int Level { get; set; }
    public int Version { get; set; }
    public string VerseText { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? MappingReviewedAt { get; set; }
}

/// <summary>Everything stored for a new version of a variant; words come from <see cref="RevisionTokenizer"/>.</summary>
public sealed class NewRevisionVersion
{
    public int HebRefId { get; init; }
    public int Level { get; init; }
    public int Version { get; init; }
    public required string VerseText { get; init; }
    public required string BaseTextHash { get; init; }
    public string? Note { get; init; }
    public int? CreatedByUserId { get; init; }

    /// <summary>The variant this version was started from (revision of a revision); null = started from Elb1871.</summary>
    public int? BasedOnRevisionVerseId { get; init; }

    /// <summary>The previously active row of the same (verse, level), ended in the same transaction.</summary>
    public int? EndActiveRevisionVerseId { get; init; }
    public required IReadOnlyList<RevisionToken> Tokens { get; init; }

    /// <summary>Base position and change per token (matched by position); a position without an entry is stored as <c>new</c>.</summary>
    public IReadOnlyList<RevisionCarryWord> Words { get; init; } = [];

    /// <summary>Mapping rows carried over from the parent text (<c>Origin = 'carried'</c>), keyed by the new word position.</summary>
    public IReadOnlyList<RevisionCarryRow> CarriedRows { get; init; } = [];

    /// <summary><c>G</c> or <c>H</c>, set from the book, never from the client.</summary>
    public string Lang { get; init; } = "H";
}

/// <summary>Header of a stored variant version, active or ended.</summary>
public sealed class RevisionVerseHeaderRow
{
    public int Id { get; set; }
    public int HebRefId { get; set; }
    public int Level { get; set; }
    public int Version { get; set; }
    public bool IsActive { get; set; }
    public DateTime? MappingReviewedAt { get; set; }
}

public sealed class RevisionWordRow
{
    public int Id { get; set; }
    public int PositionInVerse { get; set; }
    public string WordInContext { get; set; } = string.Empty;
    public string? PlainWord { get; set; }
    public int? BasePositionInVerse { get; set; }
    public string Change { get; set; } = string.Empty;
}

/// <summary>One <c>RevisionMapping</c> row; <see cref="Origin"/> is carried, manual or seed.</summary>
public sealed class RevisionMappingRow
{
    public int RevisionWordId { get; set; }
    public int? StepWordId { get; set; }
    public bool IsAddedWord { get; set; }
    public int? ParentRevisionWordId { get; set; }
    public string? GermanWordPart { get; set; }
    public string Origin { get; set; } = string.Empty;
}

/// <summary>One stored version of a (verse, level), active or ended; <see cref="CreatedBy"/> is the author's display name.</summary>
public sealed class RevisionHistoryRow
{
    public int RevisionVerseId { get; set; }
    public int Version { get; set; }
    public string VerseText { get; set; } = string.Empty;
    public string? Note { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? EndedAt { get; set; }
}

public interface IRevisionRepository
{
    Task<string?> GetBaseVerseTextAsync(int hebRefId, CancellationToken token = default);
    Task<IReadOnlyList<RevisionBaseWordRow>> GetBaseWordsAsync(int hebRefId, CancellationToken token = default);

    /// <summary>True when the level is a preset and not the base level 100.</summary>
    Task<bool> IsWritableLevelAsync(int level, CancellationToken token = default);

    Task<RevisionActiveRow?> GetActiveAsync(int hebRefId, int level, CancellationToken token = default);

    /// <summary>Highest version ever stored for (verse, level), ended ones included; 0 when there is none.</summary>
    Task<int> GetLastVersionAsync(int hebRefId, int level, CancellationToken token = default);

    /// <summary>Ends the previous active row and inserts the new version with its words in one transaction. Null when a concurrent save won (unique violation).</summary>
    Task<int?> InsertVersionAsync(NewRevisionVersion version, int endedByUserId, CancellationToken token = default);

    /// <summary>All versions of (verse, level), newest first.</summary>
    Task<IReadOnlyList<RevisionHistoryRow>> GetHistoryAsync(int hebRefId, int level, CancellationToken token = default);

    /// <summary>Ends the active variant without a successor. False when it is not (or no longer) the active version.</summary>
    Task<bool> EndVariantAsync(int revisionVerseId, int userId, CancellationToken token = default);

    Task<IReadOnlyList<RevisionVariantRow>> GetActiveVariantsAsync(int hebRefId, CancellationToken token = default);

    Task<RevisionVerseHeaderRow?> GetHeaderAsync(int revisionVerseId, CancellationToken token = default);
    Task<IReadOnlyList<RevisionWordRow>> GetWordsAsync(int revisionVerseId, CancellationToken token = default);
    Task<IReadOnlyList<RevisionMappingRow>> GetMappingAsync(int revisionVerseId, CancellationToken token = default);

    /// <summary>
    /// Replaces the mapping rows of one variant and stamps it reviewed, in one transaction. False when the variant is not
    /// (or no longer) the active version.
    /// </summary>
    Task<bool> ReplaceMappingAsync(int revisionVerseId, string lang, IReadOnlyList<RevisionMappingRow> rows, int userId, CancellationToken token = default);
}
