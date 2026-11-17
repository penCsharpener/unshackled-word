namespace UnshackledWord.Application.Features.Revision;

/// <summary>The live variant of one (verse, level); only what the write path needs.</summary>
public sealed class RevisionActiveRow
{
    public int Id { get; set; }
    public int Version { get; set; }
    public int RevisionId { get; set; }
}

public sealed class RevisionBaseWordRow
{
    public int PositionInVerse { get; set; }
    public string? PlainWord { get; set; }
}

/// <summary>A live variant as the verse read shows it. <see cref="CreatedAt"/> and <see cref="CreatedBy"/> are time and author's display name of the last text save (NULL author for seed imports).</summary>
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

/// <summary>
/// Everything stored for a text save of a variant; words come from <see cref="RevisionTokenizer"/>. The version is not part of it: the
/// repository assigns it under the row lock (live version + 1, or the highest audited version + 1 for a verse without a live row).
/// </summary>
public sealed class NewRevisionVersion
{
    public int HebRefId { get; init; }
    public int Level { get; init; }

    /// <summary>The live version the editor was loaded with; null = the editor expects no live row. Anything else makes the save fail (409).</summary>
    public int? ExpectedVersion { get; init; }
    public required string VerseText { get; init; }
    public required string BaseTextHash { get; init; }
    public string? Note { get; init; }
    public int? CreatedByUserId { get; init; }

    /// <summary>The revision this text was started from (revision of a revision); null = started from Elb1871.</summary>
    public int? BasedOnRevisionId { get; init; }

    /// <summary>Version of the parent's verse when this text was written; set exactly when <see cref="BasedOnRevisionId"/> is.</summary>
    public int? BasedOnVersion { get; init; }
    public required IReadOnlyList<RevisionToken> Tokens { get; init; }

    /// <summary>Base position and change per token (matched by position); a position without an entry is stored as <c>new</c>.</summary>
    public IReadOnlyList<RevisionCarryWord> Words { get; init; } = [];

    /// <summary>Mapping rows carried over from the parent text (<c>Origin = 'carried'</c>), keyed by the new word position.</summary>
    public IReadOnlyList<RevisionCarryRow> CarriedRows { get; init; } = [];

    /// <summary><c>G</c> or <c>H</c>, set from the book, never from the client.</summary>
    public string Lang { get; init; } = "H";
}

/// <summary>Result of a text save: the stable id of the live row and the version it now has.</summary>
public sealed record SavedRevisionVersion(int RevisionVerseId, int Version);

/// <summary>Header of a live variant.</summary>
public sealed class RevisionVerseHeaderRow
{
    public int Id { get; set; }
    public int HebRefId { get; set; }
    public int Level { get; set; }
    public int Version { get; set; }
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

    /// <summary>head or unit (roadmap 0018).</summary>
    public string Role { get; set; } = "head";

    /// <summary>Hebrew morpheme (Lang H only); null = main morpheme.</summary>
    public string? StrongsNumber { get; set; }

    /// <summary>Lemma of the compound part; only with <see cref="GermanWordPart"/>.</summary>
    public string? PartLemma { get; set; }

    /// <summary>The maintainer accepted several heads on this source morpheme.</summary>
    public bool MultiHead { get; set; }
}

/// <summary>One <c>RevisionVerseAudit</c> row of a (verse, level): a text save, a mapping save or a withdraw. <see cref="By"/> is the display name, NULL for seed imports.</summary>
public sealed class RevisionHistoryRow
{
    public long AuditId { get; set; }
    public int Version { get; set; }
    public string Action { get; set; } = string.Empty;
    public string VerseText { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime At { get; set; }
    public string? By { get; set; }
}

public interface IRevisionRepository
{
    Task<string?> GetBaseVerseTextAsync(int hebRefId, CancellationToken token = default);
    Task<IReadOnlyList<RevisionBaseWordRow>> GetBaseWordsAsync(int hebRefId, CancellationToken token = default);

    /// <summary>True when the level is a preset and not the base level 100.</summary>
    Task<bool> IsWritableLevelAsync(int level, CancellationToken token = default);

    /// <summary>The live row of (verse, level); null when the verse has no variant at that level (never written or withdrawn).</summary>
    Task<RevisionActiveRow?> GetActiveAsync(int hebRefId, int level, CancellationToken token = default);

    /// <summary>
    /// Text save in one transaction: locks the live row of (verse, level). Without one it inserts (version = highest audited version + 1, so a
    /// withdrawn verse continues); with one it updates it in place (version + 1, mapping review reset) when <see cref="NewRevisionVersion.ExpectedVersion"/>
    /// equals the live version. Words are rewritten (the old mapping rows go with them by cascade), the carried mapping inserted and a
    /// <c>text</c> audit row written. Null on a stale expected version or when a concurrent first save won (unique violation).
    /// </summary>
    Task<SavedRevisionVersion?> SaveVersionAsync(NewRevisionVersion version, CancellationToken token = default);

    /// <summary>All audit rows of (verse, level), newest first (by audit id).</summary>
    Task<IReadOnlyList<RevisionHistoryRow>> GetHistoryAsync(int hebRefId, int level, CancellationToken token = default);

    /// <summary>Withdraws a variant: writes the <c>withdraw</c> audit row, then deletes the live row (words and mapping by cascade). False when the row no longer exists.</summary>
    Task<bool> EndVariantAsync(int revisionVerseId, int userId, CancellationToken token = default);

    Task<IReadOnlyList<RevisionVariantRow>> GetActiveVariantsAsync(int hebRefId, CancellationToken token = default);

    Task<RevisionVerseHeaderRow?> GetHeaderAsync(int revisionVerseId, CancellationToken token = default);
    Task<IReadOnlyList<RevisionWordRow>> GetWordsAsync(int revisionVerseId, CancellationToken token = default);
    Task<IReadOnlyList<RevisionMappingRow>> GetMappingAsync(int revisionVerseId, CancellationToken token = default);

    /// <summary>
    /// Replaces the mapping rows of one variant and stamps it reviewed, in one transaction. False when the variant no longer exists or a
    /// concurrent text save replaced its words (a row refers to a word id that is not part of the verse anymore).
    /// </summary>
    Task<bool> ReplaceMappingAsync(int revisionVerseId, string lang, IReadOnlyList<RevisionMappingRow> rows, int userId, CancellationToken token = default);
}
