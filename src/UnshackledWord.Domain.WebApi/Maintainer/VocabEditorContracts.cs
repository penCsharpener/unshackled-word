using UnshackledWord.Domain.WebApi.Vocab;

namespace UnshackledWord.Domain.WebApi.Maintainer;

// Vocab editor (roadmap 0022.4): api/maintainer/vocab/entries. Admin/Maintainer only.

/// <summary>Query of <c>GET api/maintainer/vocab/entries</c>. All filters combine with AND; without a course both tracks are listed.</summary>
public sealed class ListVocabEditorEntriesRequest
{
    public const int MaxTake = 200;
    public const int MaxQueryLength = 60;

    /// <summary>Course code (<c>nt-greek</c>, <c>ot-hebrew</c>); null = both tracks.</summary>
    public string? Course { get; set; }

    /// <summary>Lemma prefix (normalized), transliteration prefix or a part of an accepted German meaning.</summary>
    public string? Q { get; set; }

    public bool Unreviewed { get; set; }
    public bool NoAudio { get; set; }

    /// <summary>No dictionary form yet.</summary>
    public bool FormMissing { get; set; }

    /// <summary>The dictionary form was proposed by an agent and not yet confirmed.</summary>
    public bool FormFromAgent { get; set; }

    /// <summary>Entries with more than one Strong's key.</summary>
    public bool MultiKey { get; set; }

    /// <summary>List retired entries instead of the active ones.</summary>
    public bool Retired { get; set; }

    public int? RankFrom { get; set; }
    public int? RankTo { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; } = 50;
}

/// <summary>One row of the editor list.</summary>
public sealed class VocabEditorListItemResponse
{
    public int Id { get; set; }
    public string Track { get; set; } = string.Empty;

    /// <summary>Frequency rank within the track; null for morphemes and retired entries.</summary>
    public int? Rank { get; set; }

    public string Lemma { get; set; } = string.Empty;
    public string? Transliteration { get; set; }
    public string? DictionaryForm { get; set; }

    /// <summary><c>generated</c>, <c>agent</c> or <c>manual</c>; null without a form.</summary>
    public string? DictionaryFormOrigin { get; set; }

    public int Occurrences { get; set; }
    public bool HasAudio { get; set; }
    public bool Reviewed { get; set; }
    public bool Retired { get; set; }
    public int ContentVersion { get; set; }

    /// <summary>Users who hold a card on the entry.</summary>
    public int Learners { get; set; }

    /// <summary>Accepted meanings in rank order.</summary>
    public List<VocabMeaningResponse> Meanings { get; set; } = [];

    public List<string> StrongsKeys { get; set; } = [];
}

public sealed class ListVocabEditorEntriesResponse
{
    public int Total { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
    public List<VocabEditorListItemResponse> Items { get; set; } = [];
}

/// <summary>Route of <c>GET api/maintainer/vocab/entries/{id}</c>.</summary>
public sealed class GetVocabEditorEntryRequest
{
    public int Id { get; set; }
}

public sealed class VocabEditorEntryResponse
{
    public int Id { get; set; }
    public string Track { get; set; } = string.Empty;
    public int? Rank { get; set; }
    public string Lemma { get; set; } = string.Empty;
    public string? Transliteration { get; set; }
    public string? DictionaryForm { get; set; }

    /// <summary><c>generated</c>, <c>agent</c> or <c>manual</c>; null without a form.</summary>
    public string? DictionaryFormOrigin { get; set; }

    public string? Pos { get; set; }
    public int Occurrences { get; set; }
    public bool IsProperName { get; set; }
    public bool IsMorpheme { get; set; }
    public bool HasAudio { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int? ReviewedByUserId { get; set; }
    public DateTime? RetiredAt { get; set; }
    public int ContentVersion { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}

/// <summary>A meaning of the editor, accepted or not.</summary>
public sealed class VocabEditorMeaningResponse
{
    public int Id { get; set; }
    public string German { get; set; } = string.Empty;
    public int Rank { get; set; }
    public bool IsAccepted { get; set; }

    /// <summary><c>derived</c> or <c>manual</c>. Only manual meanings can be deleted.</summary>
    public string Origin { get; set; } = string.Empty;

    public bool IsPinned { get; set; }
    public int? Count { get; set; }
    public decimal? Share { get; set; }
}

/// <summary>A Strong's key of the entry with its occurrences and the lexicon lemma it stands for.</summary>
public sealed class VocabEditorKeyResponse
{
    public string StrongsKey { get; set; } = string.Empty;
    public int Occurrences { get; set; }
    public string? Lemma { get; set; }
    public string? Transliteration { get; set; }
}

/// <summary>Everything the editor page needs for one entry; renderings and sample verses are those of the public entry endpoint.</summary>
public sealed class GetVocabEditorEntryResponse
{
    public VocabEditorEntryResponse Entry { get; set; } = new();

    /// <summary>All meanings in rank order, including the ones that are not accepted.</summary>
    public List<VocabEditorMeaningResponse> Meanings { get; set; } = [];

    public List<VocabEditorKeyResponse> StrongsKeys { get; set; } = [];

    /// <summary>Users with a card on the entry: "n Nutzer werden benachrichtigt" before a learning-relevant change.</summary>
    public int Learners { get; set; }

    public List<VocabRenderingResponse> Renderings { get; set; } = [];
    public int RenderingsTotal { get; set; }
    public int TotalLinks { get; set; }
    public DateTime? RefreshedAt { get; set; }
    public List<VocabSampleVerseResponse> SampleVerses { get; set; } = [];
}

/// <summary>Body of <c>PUT api/maintainer/vocab/entries/{id}</c>: the editable entry fields, sent in full.</summary>
public sealed class UpdateVocabEntryRequest
{
    public int Id { get; set; }

    /// <summary>The learning form, e.g. <c>λόγος, -ου, ὁ</c>; blank clears it. A changed text gets the origin <c>manual</c>.</summary>
    public string? DictionaryForm { get; set; }

    public string? Transliteration { get; set; }

    /// <summary>The maintainer checked the entry. Turning it on sets <c>ReviewedAt</c> and the reviewer once; turning it off clears both.</summary>
    public bool Reviewed { get; set; }
}

/// <summary>Body of <c>POST api/maintainer/vocab/entries/{id}/meanings</c>: adds a manual, accepted meaning at the end.</summary>
public sealed class AddVocabMeaningRequest
{
    public int Id { get; set; }
    public string German { get; set; } = string.Empty;
}

/// <summary>Body of <c>PATCH api/maintainer/vocab/entries/{id}/meanings/{meaningId}</c>: only the sent fields change.</summary>
public sealed class UpdateVocabMeaningRequest
{
    public int Id { get; set; }
    public int MeaningId { get; set; }
    public bool? IsAccepted { get; set; }
    public bool? IsPinned { get; set; }
}

/// <summary>Route of <c>DELETE api/maintainer/vocab/entries/{id}/meanings/{meaningId}</c> (manual meanings only).</summary>
public sealed class DeleteVocabMeaningRequest
{
    public int Id { get; set; }
    public int MeaningId { get; set; }
}

/// <summary>Body of <c>PUT api/maintainer/vocab/entries/{id}/meanings/order</c>: every meaning id of the entry in the wanted order.</summary>
public sealed class ReorderVocabMeaningsRequest
{
    public int Id { get; set; }
    public List<int> MeaningIds { get; set; } = [];
}

/// <summary>Route and body of <c>POST api/maintainer/vocab/entries/{id}/keys/{key}/move</c>.</summary>
public sealed class MoveVocabKeyRequest
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;

    /// <summary>The entry that receives the key; null = a new entry for this key alone.</summary>
    public int? TargetEntryId { get; set; }
}

/// <summary>Route of <c>POST api/maintainer/vocab/entries/{id}/merge/{targetId}</c>: entry <c>id</c> is merged into <c>targetId</c> and deleted.</summary>
public sealed class MergeVocabEntryRequest
{
    public int Id { get; set; }
    public int TargetId { get; set; }
}

/// <summary>Route of <c>POST api/maintainer/vocab/entries/{id}/retire</c> and <c>DELETE api/maintainer/vocab/entries/{id}</c>.</summary>
public sealed class VocabEntryIdRequest
{
    public int Id { get; set; }
}

/// <summary>What an edit did. The editor reloads the entry afterwards.</summary>
public sealed class VocabEditResponse
{
    public int EntryId { get; set; }

    /// <summary>Content version of <see cref="EntryId"/> after the edit (before it for a delete or merge source, which are gone).</summary>
    public int ContentVersion { get; set; }

    /// <summary>The edit was learning-relevant: version bumped and a change row written.</summary>
    public bool Bumped { get; set; }

    /// <summary>The other entry of a key move (new or existing), merge or split.</summary>
    public int? TargetEntryId { get; set; }

    public int? TargetContentVersion { get; set; }

    /// <summary>Id of a created meaning.</summary>
    public int? MeaningId { get; set; }

    /// <summary>Users with a card on the changed entry (those who will see "geändert").</summary>
    public int Learners { get; set; }

    /// <summary>Entry occurrences after a key move or merge: this entry and the other one.</summary>
    public int? Occurrences { get; set; }

    public int? TargetOccurrences { get; set; }
}
