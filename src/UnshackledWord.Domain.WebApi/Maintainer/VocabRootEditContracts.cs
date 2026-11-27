using UnshackledWord.Domain.WebApi.Vocab;

namespace UnshackledWord.Domain.WebApi.Maintainer;

/// <summary>
/// Body of <c>PUT api/maintainer/vocab/entries/{id}/root</c> (roadmap 0030.5): the manual root of the entry's main key. One of three shapes:
/// <see cref="Simple"/> (the word is its own root); <see cref="PrefixKeys"/> with a Greek root (<see cref="RootId"/> or <see cref="RootKey"/>);
/// Hebrew/Aramaic: <see cref="RootId"/> or <see cref="RootLetters"/> with an optional verb <see cref="RootKey"/> and <see cref="Homograph"/>
/// (the root is created when missing). The rules are those of the agent run (<c>VocabRootAnswerValidator</c>).
/// </summary>
public sealed class SetVocabEntryRootRequest
{
    public int Id { get; set; }

    /// <summary>The word is its own root, no prefixes.</summary>
    public bool Simple { get; set; }

    /// <summary>Strong's keys of the stripped prepositions in word order (closed list, see <c>GET api/maintainer/vocab/roots/prepositions</c>); Greek only.</summary>
    public List<string> PrefixKeys { get; set; } = [];

    /// <summary>An existing root (<c>VocabRoots."Id"</c>) of the entry's track.</summary>
    public int? RootId { get; set; }

    /// <summary>Strong's key of the root word: Greek (creates the root when none has this key) or the optional Hebrew verb key.</summary>
    public string? RootKey { get; set; }

    /// <summary>Hebrew/Aramaic root consonants (2 to 4); the root is created when missing.</summary>
    public string? RootLetters { get; set; }

    /// <summary>Homograph number of a Hebrew root (ענה I/II = 1/2); omitted: the only root with these letters, or 1 when none exists.</summary>
    public short? Homograph { get; set; }
}

/// <summary>Body of <c>PUT api/maintainer/vocab/roots/{id}/parent</c>: the parent root by id, or (Greek) by the root word's key; none of both clears the parent.</summary>
public sealed class SetVocabRootParentRequest
{
    public int Id { get; set; }
    public int? ParentRootId { get; set; }

    /// <summary>Alternative to <see cref="ParentRootId"/>: the Greek word's key; its root is created when it has none yet.</summary>
    public string? ParentStrongsKey { get; set; }
}

/// <summary>The preposition table of the root prefixes (closed list, ordered by dictionary form).</summary>
public sealed class VocabPrepositionsResponse
{
    public List<VocabPrepositionResponse> Items { get; set; } = [];
}

public sealed class VocabPrepositionResponse
{
    /// <summary>Strong's key (<c>G4862</c>).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Dictionary form (<c>σύν</c>).</summary>
    public string Lemma { get; set; } = string.Empty;
}

/// <summary>Query of <c>GET api/maintainer/vocab/roots/search</c>.</summary>
public sealed class SearchVocabRootsRequest
{
    /// <summary><c>G</c> (Greek) or <c>H</c> (Hebrew/Aramaic).</summary>
    public string Track { get; set; } = "G";

    public string? Q { get; set; }
    public int Take { get; set; } = 10;
}

public sealed class SearchVocabRootsResponse
{
    public List<VocabRootCandidateResponse> Items { get; set; } = [];
}

/// <summary>
/// A root the picker offers. Greek: an active vocab entry by lemma prefix; <see cref="RootId"/> is the root row of its key and null while there is
/// none (the editor then sends <see cref="StrongsKey"/>). Hebrew: an existing root row by consonants.
/// </summary>
public sealed class VocabRootCandidateResponse
{
    public int? RootId { get; set; }
    public string Root { get; set; } = string.Empty;
    public string? StrongsKey { get; set; }
    public short Homograph { get; set; } = 1;

    /// <summary>First accepted meaning of the word with the key, for orientation.</summary>
    public string? Meaning { get; set; }
}

/// <summary>The editable root of an entry (<see cref="GetVocabEditorEntryResponse.Root"/>): the row of its main key.</summary>
public sealed class VocabEditorRootResponse
{
    public int RootId { get; set; }
    public string Root { get; set; } = string.Empty;
    public string? StrongsKey { get; set; }
    public short Homograph { get; set; } = 1;

    /// <summary>Strong's keys of the prepositions in word order; empty for a simple word and for Hebrew.</summary>
    public string[] PrefixKeys { get; set; } = [];

    public string? Prefixes { get; set; }

    /// <summary><c>prefill</c>, <c>agent</c> or <c>manual</c>.</summary>
    public string Origin { get; set; } = string.Empty;

    public bool IsSimple { get; set; }

    /// <summary>The root's parents, nearest first (<c>μαρτυρία → μαρτυρέω</c>: the root is μαρτυρία, the list starts with μαρτυρέω); at most 4.</summary>
    public List<VocabRootRefResponse> Ancestors { get; set; } = [];
}
