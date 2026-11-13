namespace UnshackledWord.Domain.WebApi.Lemmas;

// Request/response bodies of /api/lemmas/*. All morphology fields carry the abbreviated codes
// (see GET /api/lemmas/value-maps for the labels).

public sealed class ValueMapEntry
{
    public string Code { get; set; } = string.Empty;
    public string LabelDe { get; set; } = string.Empty;
    public string LabelEn { get; set; } = string.Empty;
}

public sealed class ValueMapField
{
    public string Field { get; set; } = string.Empty;
    public IReadOnlyList<ValueMapEntry> Values { get; set; } = [];
}

public sealed class VerbFormRuleEntry
{
    public string VerbForm { get; set; } = string.Empty;
    public IReadOnlyList<string> Fields { get; set; } = [];
}

public sealed class ApplicabilityRuleEntry
{
    public string PartOfSpeech { get; set; } = string.Empty;
    public IReadOnlyList<string> Fields { get; set; } = [];

    /// <summary>Only for verbs: extra applicable fields per verbForm code.</summary>
    public IReadOnlyList<VerbFormRuleEntry>? ByVerbForm { get; set; }
}

/// <summary>An STTS tag with its default UD tag and every UD tag accepted with it (from the SttsTags table).</summary>
public sealed class SttsTagEntry
{
    public string Tag { get; set; } = string.Empty;
    public string Upos { get; set; } = string.Empty;
    public IReadOnlyList<string> AllowedUpos { get; set; } = [];
}

/// <summary>The STTS tag proposed for a UD part of speech; verbForm / mood refine it (null = any).</summary>
public sealed class SttsProposalEntry
{
    public string PartOfSpeech { get; set; } = string.Empty;
    public string? VerbForm { get; set; }
    public string? Mood { get; set; }
    public string Stts { get; set; } = string.Empty;
}

public sealed class ValueMapsResponse
{
    public IReadOnlyList<ValueMapField> Fields { get; set; } = [];
    public IReadOnlyList<ApplicabilityRuleEntry> Rules { get; set; } = [];

    /// <summary>STTS tags of the table (labels are in Fields, field "stts"); the editor filters the dropdown by AllowedUpos.</summary>
    public IReadOnlyList<SttsTagEntry> SttsTags { get; set; } = [];

    /// <summary>Most specific rule first; the first rule that matches wins.</summary>
    public IReadOnlyList<SttsProposalEntry> SttsProposals { get; set; } = [];
}

public sealed class GetLemmaChapterRequest
{
    public int BookId { get; set; }
    public int Chapter { get; set; }
}

public class LemmaWordRow
{
    public int WordId { get; set; }
    public int Verse { get; set; }
    public int PositionInVerse { get; set; }
    public string WordInContext { get; set; } = string.Empty;
    public string? PlainWord { get; set; }
    public string? Lemma { get; set; }
    public string? PartOfSpeech { get; set; }
    public string? Stts { get; set; }
    public string? Degree { get; set; }
    public string? VerbForm { get; set; }
    public string? Tense { get; set; }
    public string? Person { get; set; }
    public string? Number { get; set; }
    public string? Mood { get; set; }
    public string? Case { get; set; }
    public string? Gender { get; set; }
    public bool HasMorphology { get; set; }

    /// <summary>Set when Stts does not fit PartOfSpeech: the tag that would (never applied automatically).</summary>
    public string? ProposedStts { get; set; }
}

public sealed class LemmaChapterResponse
{
    public int BookId { get; set; }
    public int Chapter { get; set; }
    public IReadOnlyList<LemmaWordRow> Words { get; set; } = [];
}

/// <summary>
/// Full replacement of the editable fields: a null morphology field clears it. Stts is the exception: null leaves it untouched,
/// a tag sets it (it must exist and, when it or the part of speech changes, be allowed together with the part of speech).
/// </summary>
public class LemmaEditFields
{
    public string Lemma { get; set; } = string.Empty;
    public string PartOfSpeech { get; set; } = string.Empty;
    public string? Stts { get; set; }
    public string? Degree { get; set; }
    public string? VerbForm { get; set; }
    public string? Tense { get; set; }
    public string? Person { get; set; }
    public string? Number { get; set; }
    public string? Mood { get; set; }
    public string? Case { get; set; }
    public string? Gender { get; set; }
}

/// <summary>PUT /api/lemmas/words/{wordId}; WordId comes from the route.</summary>
public sealed class UpdateLemmaWordRequest : LemmaEditFields
{
    public int WordId { get; set; }
}

/// <summary>One entry of the batch body; WordId is in the body.</summary>
public sealed class LemmaWordChange : LemmaEditFields
{
    public int WordId { get; set; }
}

public sealed class UpdateLemmaChapterRequest
{
    public IReadOnlyList<LemmaWordChange> Changes { get; set; } = [];
}

public sealed class UpdateLemmaChapterResponse
{
    public int Updated { get; set; }
    public IReadOnlyList<LemmaWordRow> Words { get; set; } = [];
}
