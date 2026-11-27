namespace UnshackledWord.Domain.WebApi.Vocab;

/// <summary>
/// The root of a vocab entry (roadmap 0030.1), from the <c>VocabEntryRoots</c> view: the root row of the entry's main Strong's key.
/// Absent (null, omitted in JSON) while no root is known for the entry.
/// </summary>
public sealed class VocabRootResponse
{
    /// <summary><c>VocabRoots."Id"</c>: stable, the family the entry belongs to.</summary>
    public int Id { get; set; }

    /// <summary>The bare root alone (<c>μαρτυρέω</c>, Hebrew root letters <c>כתב</c>).</summary>
    public string Root { get; set; } = string.Empty;

    /// <summary>Strong's key of the root word (<c>G3140</c>); null for a Hebrew root without a verb in the corpus.</summary>
    public string? StrongsKey { get; set; }

    /// <summary>The stripped prepositions in word order (<c>σύν + ἐπί</c>); null for a simple word and for Hebrew.</summary>
    public string? Prefixes { get; set; }

    /// <summary><c>prefill</c>, <c>agent</c> (shown with the KI badge) or <c>manual</c>.</summary>
    public string Origin { get; set; } = string.Empty;

    /// <summary>No prefixes and the root is the entry's own main key: the list shows the root dimmed.</summary>
    public bool IsSimple { get; set; }
}

/// <summary>A root by id and text (parent or sub-root link).</summary>
public sealed class VocabRootRefResponse
{
    public int Id { get; set; }
    public string Root { get; set; } = string.Empty;
}

/// <summary>One member of a root's family.</summary>
public sealed class VocabRootMemberResponse
{
    public int EntryId { get; set; }
    public string Lemma { get; set; } = string.Empty;

    /// <summary>The first accepted meaning; null while none is derived.</summary>
    public string? Meaning { get; set; }

    public int Rank { get; set; }
}

/// <summary><c>GET api/vocab/roots/{id}</c> (roadmap 0030.3): the root with its parent, direct sub-roots and a sample of its family.</summary>
public sealed class VocabRootDetailResponse
{
    public const int MaxMembers = 12;

    public int Id { get; set; }
    public string Root { get; set; } = string.Empty;
    public string? StrongsKey { get; set; }

    /// <summary>The wider family this root belongs to; omitted for a top-level root.</summary>
    public VocabRootRefResponse? Parent { get; set; }

    public List<VocabRootRefResponse> SubRoots { get; set; } = [];

    /// <summary>At most <see cref="MaxMembers"/> entries of the whole family (sub-roots included): the root's own entry first, then by rank.</summary>
    public List<VocabRootMemberResponse> Members { get; set; } = [];

    /// <summary>All ranked, active entries of the family.</summary>
    public int MemberTotal { get; set; }
}

/// <summary>Route of <c>GET api/vocab/roots/{id}</c>.</summary>
public sealed class GetVocabRootRequest
{
    public int Id { get; set; }
}
