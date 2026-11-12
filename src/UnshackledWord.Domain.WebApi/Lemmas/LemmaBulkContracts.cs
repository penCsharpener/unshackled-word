namespace UnshackledWord.Domain.WebApi.Lemmas;

// Request/response bodies of /api/lemmas/lemma-counts, /bulk/*, and /verse (bulk lemma replace).

public sealed class LemmaCountsRequest
{
    public string Q { get; set; } = string.Empty;
    public int Limit { get; set; } = 20;
}

public sealed class LemmaCountItem
{
    public string Lemma { get; set; } = string.Empty;
    public long Count { get; set; }

    /// <summary>Other lemmas that differ only in case (any count), e.g. "HERR" for "Herr".</summary>
    public IReadOnlyList<LemmaCaseVariant> CaseVariants { get; set; } = [];
}

public sealed class LemmaCaseVariant
{
    public string Lemma { get; set; } = string.Empty;
    public long Count { get; set; }
}

public sealed class LemmaCountsResponse
{
    public IReadOnlyList<LemmaCountItem> Items { get; set; } = [];
}

/// <summary>Filters shared by the occurrence list and the id list. From is sent as repeated query keys (?from=Jesu&amp;from=Jesum).</summary>
public class BulkFilterRequest
{
    public string[] From { get; set; } = [];

    /// <summary>"ot" or "nt"; not together with BookId.</summary>
    public string? Testament { get; set; }

    public int? BookId { get; set; }

    /// <summary>Needs BookId.</summary>
    public int? Chapter { get; set; }

    public string? PartOfSpeech { get; set; }

    /// <summary>Exact PlainWord; with PartOfSpeech this selects one group.</summary>
    public string? PlainWord { get; set; }

    /// <summary>Original-language group keys ("G4151", "H7307", "G2316+G3588", "added", "none"), repeated query keys, OR-combined. Does not narrow ByOriginal.</summary>
    public string[] Original { get; set; } = [];
}

public sealed class BulkOccurrencesRequest : BulkFilterRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 100;
}

public sealed class BulkBookCount
{
    public int BookId { get; set; }
    public long Count { get; set; }
}

public sealed class BulkLemmaCount
{
    public string Lemma { get; set; } = string.Empty;
    public long Count { get; set; }
}

public sealed class BulkGroupCount
{
    /// <summary>Only set on Groups (the triple grouping); null on FormGroups.</summary>
    public string? OrigKey { get; set; }

    public string? PlainWord { get; set; }
    public string? PartOfSpeech { get; set; }
    public long Count { get; set; }
}

public sealed class BulkOccurrence
{
    public int WordId { get; set; }
    public int BookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public int PositionInVerse { get; set; }
    public string Lemma { get; set; } = string.Empty;
    public string WordInContext { get; set; } = string.Empty;
    public string? PlainWord { get; set; }
    public string? PartOfSpeech { get; set; }
    public string? Case { get; set; }
    public string? Number { get; set; }
    public string? Gender { get; set; }

    /// <summary>aligned | multi | added | none.</summary>
    public string? OrigKind { get; set; }

    /// <summary>Group key: G4680 / H2450 / "G2316+G3588" / added / none.</summary>
    public string? OrigKey { get; set; }

    /// <summary>Disambiguated Strong's shown to the user (G4680, H2450G); Hebrew is the braced main part.</summary>
    public string? OrigStrongs { get; set; }

    /// <summary>Surface form in the original verse.</summary>
    public string? OrigWord { get; set; }

    /// <summary>Original word class: N V A T R P ... (first letter of the STEP morphology).</summary>
    public string? OrigPos { get; set; }

    public bool PosConflict { get; set; }

    /// <summary>Up to 5 words before the match, same verse.</summary>
    public string Left { get; set; } = string.Empty;

    /// <summary>Up to 5 words after the match, same verse.</summary>
    public string Right { get; set; } = string.Empty;
}

public sealed class BulkOriginalCount
{
    public string Key { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string? Lang { get; set; }

    /// <summary>Lexicon lemma (σοφός / חָכָם); falls back to the surface form.</summary>
    public string? Word { get; set; }

    public string? Gloss { get; set; }
    public string? Pos { get; set; }
    public long Count { get; set; }

    /// <summary>Share of the aligned matches of its language (added/none: of all matches), 0..1.</summary>
    public double Share { get; set; }

    public bool Minority { get; set; }
    public bool PosConflict { get; set; }
    public bool Unmapped { get; set; }
    public bool Added { get; set; }
}

/// <summary>Total, byBook, byLemma and groups are counted over ALL matches, not just the page.</summary>
public sealed class BulkOccurrencesResponse
{
    public IReadOnlyList<string> From { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public long Total { get; set; }
    public int TotalPages { get; set; }
    public IReadOnlyList<BulkBookCount> ByBook { get; set; } = [];
    public IReadOnlyList<BulkLemmaCount> ByLemma { get; set; } = [];

    /// <summary>(origKey, plainWord, partOfSpeech) triples.</summary>
    public IReadOnlyList<BulkGroupCount> Groups { get; set; } = [];

    /// <summary>Classic (plainWord, partOfSpeech) groups, without origKey.</summary>
    public IReadOnlyList<BulkGroupCount> FormGroups { get; set; } = [];

    /// <summary>One entry per original key of the from-lemmas (all other filters applied, but NOT the original filter, so the filter list stays complete).</summary>
    public IReadOnlyList<BulkOriginalCount> ByOriginal { get; set; } = [];

    /// <summary>Server-computed hint: several original words with different meanings, or a part-of-speech conflict.</summary>
    public bool Ambiguous { get; set; }

    public IReadOnlyList<BulkOccurrence> Occurrences { get; set; } = [];
}

/// <summary>All matching word ids (reading order), for "select all pages" / "select group on all pages". Capped at 20,000.</summary>
public sealed class BulkWordIdsResponse
{
    public long Total { get; set; }
    public bool Truncated { get; set; }
    public IReadOnlyList<int> WordIds { get; set; } = [];
}

public sealed class GetLemmaVerseRequest
{
    public int BookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
}

public sealed class LemmaVerseResponse
{
    public int BookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public IReadOnlyList<LemmaWordRow> Words { get; set; } = [];
}

public sealed class BulkApplyRequest
{
    public string[] From { get; set; } = [];
    public string To { get; set; } = string.Empty;
    public int[] WordIds { get; set; } = [];

    /// <summary>Required, same length and order as WordIds: the OrigKey the user saw ("" = added or none).</summary>
    public string[] ExpectedOriginal { get; set; } = [];

    public string? Note { get; set; }
}

public sealed class BulkApplyResponse
{
    public int ChangeSetId { get; set; }
    public int Updated { get; set; }
}

/// <summary>Body of the 409 on apply: rows whose lemma is no longer one of the source lemmas (or that have no morphology row). Nothing was written.</summary>
public sealed class BulkStaleResponse
{
    public IReadOnlyList<int> Stale { get; set; } = [];
    public IReadOnlyList<BulkStaleDetail> Details { get; set; } = [];
}

public sealed class BulkStaleDetail
{
    public int WordId { get; set; }

    /// <summary>"lemma" (lemma changed or morphology row missing) or "original" (the alignment changed).</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Current lemma, for reason "lemma".</summary>
    public string? Lemma { get; set; }

    /// <summary>Current original key, for reason "original".</summary>
    public string? OrigKey { get; set; }
}

public sealed class BulkRevertRequest
{
    public int ChangeSetId { get; set; }
}

/// <summary>Only rows that still carry the change set's new lemma are restored; the rest are counted as skipped.</summary>
public sealed class BulkRevertResponse
{
    /// <summary>The new "revert" change set.</summary>
    public int ChangeSetId { get; set; }

    public int Restored { get; set; }
    public int Skipped { get; set; }
}
