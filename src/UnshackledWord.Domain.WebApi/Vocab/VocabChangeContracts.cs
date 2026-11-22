using System.Text.Json;

namespace UnshackledWord.Domain.WebApi.Vocab;

/// <summary>
/// One lexicon change a learner has not accepted yet (roadmap 0022.8b). <c>Kind</c> is meanings, form, lemma, keys, merge, split, retire or delete.
/// <c>Before</c>/<c>After</c> are the stored JSON of the change (for example <c>{"meanings":["…"]}</c>; merge: the target's keys and meanings, and in
/// <c>After</c> <c>mergedFrom</c>, <c>mergedFromLemma</c>, <c>mergedFromForm</c>).
/// </summary>
public sealed class VocabChangeResponse
{
    public string Kind { get; set; } = string.Empty;
    public JsonElement? Before { get; set; }
    public JsonElement? After { get; set; }
    public DateTimeOffset ChangedAt { get; set; }

    /// <summary>merge: the other entry of the merge; split: the entry the keys moved to. Null for other kinds.</summary>
    public int? TargetEntryId { get; set; }

    /// <summary>Lemma and form of the target entry; for a merged-away entry taken from the change row.</summary>
    public string? TargetLemma { get; set; }

    public string? TargetDictionaryForm { get; set; }
}

/// <summary>A card of the user that is flagged. <c>Status</c> is <c>changed</c> (keep or reset) or <c>removed</c> (retired or deleted entry: only reset, i.e. remove).</summary>
public sealed class VocabChangedEntryResponse
{
    public int EntryId { get; set; }
    public string Lemma { get; set; } = string.Empty;
    public string? DictionaryForm { get; set; }
    public string? Transliteration { get; set; }

    /// <summary><c>grc</c> or <c>he</c>.</summary>
    public string Lang { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    /// <summary>The change rows newer than the card's seen version, oldest first.</summary>
    public List<VocabChangeResponse> Changes { get; set; } = [];
}

public sealed class VocabChangesResponse
{
    public int Count { get; set; }
    public List<VocabChangedEntryResponse> Items { get; set; } = [];
}

/// <summary>Route and body of <c>POST api/vocab/changes/{entryId}</c>. <c>Action</c> is <c>keep</c> or <c>reset</c>.</summary>
public sealed class VocabChangeActionRequest
{
    public const string Keep = "keep";
    public const string Reset = "reset";

    public int EntryId { get; set; }
    public string Action { get; set; } = string.Empty;
}

/// <summary><c>Remaining</c> = flagged cards left after the action.</summary>
public sealed class VocabChangeActionResponse
{
    public string Action { get; set; } = string.Empty;
    public int EntryId { get; set; }
    public int Remaining { get; set; }
}

/// <summary><c>Kept</c> = cards accepted by "keep all"; removed cards stay in the list.</summary>
public sealed class VocabKeepAllResponse
{
    public int Kept { get; set; }
    public int Remaining { get; set; }
}

/// <summary><c>GET api/maintainer/vocab/entries/{id}/learners</c>: users with a card on the entry, and how many of them have not accepted its latest version.</summary>
public sealed class VocabLearnersResponse
{
    public int Learners { get; set; }
    public int Flagged { get; set; }
}
