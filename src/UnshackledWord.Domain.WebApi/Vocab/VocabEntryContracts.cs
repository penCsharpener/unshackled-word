using System.Text.Json.Serialization;

namespace UnshackledWord.Domain.WebApi.Vocab;

/// <summary>Route of <c>GET api/vocab/entries/{id}</c>.</summary>
public sealed class GetVocabEntryRequest
{
    public int Id { get; set; }
}

/// <summary>The vocab entry itself (<c>GET api/vocab/entries/{id}</c>, roadmap 0022.2).</summary>
public sealed class VocabEntryDetailResponse
{
    public int Id { get; set; }

    /// <summary><c>G</c> (Greek) or <c>H</c> (Hebrew and Aramaic).</summary>
    public string Track { get; set; } = string.Empty;

    /// <summary>Frequency rank within the track; null for morphemes (not ranked).</summary>
    public int? Rank { get; set; }

    public string Lemma { get; set; } = string.Empty;
    public string? Transliteration { get; set; }
    public string? DictionaryForm { get; set; }
    public string? Pos { get; set; }
    public int Occurrences { get; set; }
    public bool IsProperName { get; set; }

    /// <summary>The maintainer checked the meanings.</summary>
    public bool Reviewed { get; set; }

    public bool HasAudio { get; set; }

    /// <summary>Presigned read URL of the pronunciation; null without audio or without configured object storage.</summary>
    public string? AudioUrl { get; set; }

    /// <summary>Internal: the object key behind <see cref="AudioUrl"/>.</summary>
    [JsonIgnore]
    public string? AudioObjectKey { get; set; }

    /// <summary>The entry's root (roadmap 0030.3, family block of the detail page); null while none is known.</summary>
    public VocabRootResponse? Root { get; set; }
}

/// <summary>One Strong's key of an entry with its occurrences in the original text.</summary>
public sealed class VocabEntryKeyResponse
{
    public string StrongsKey { get; set; } = string.Empty;
    public int Occurrences { get; set; }
}

/// <summary>One German rendering of an entry, summed over all its Strong's keys and grouped by <see cref="GermanKey"/>.</summary>
public sealed class VocabRenderingResponse
{
    /// <summary>Lower-cased lemma of the German word (or its lower-cased form when <see cref="KeyIsForm"/>).</summary>
    public string GermanKey { get; set; } = string.Empty;

    /// <summary>The most frequent spelling of the key.</summary>
    public string GermanDisplay { get; set; } = string.Empty;

    /// <summary>Head links of the live text (Elberfelder 2027 variant, else Elb1871).</summary>
    public int LinkCount { get; set; }

    /// <summary>Sum of the verse counts of the underlying rows: a verse counts once per (key, PoS) row it appears in, so this can exceed the number of distinct verses.</summary>
    public int VerseCount { get; set; }

    /// <summary>Part of <see cref="LinkCount"/> that comes from Elberfelder 2027 variants; the rest comes from Elb1871 verses.</summary>
    public int FromRevisionCount { get; set; }

    /// <summary><see cref="LinkCount"/> in percent of all head links of the entry (one decimal).</summary>
    public decimal Share { get; set; }

    /// <summary>Distinct STTS tags of the German words (unknown for new/changed 2027 words).</summary>
    public List<string> PosTags { get; set; } = [];

    /// <summary>True when the German word has no lemma and the key is its plain form.</summary>
    public bool KeyIsForm { get; set; }
}

/// <summary>A word of a sample verse; <see cref="IsHit"/> marks the words that carry the entry.</summary>
public sealed class VocabSampleWordResponse
{
    public int Position { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? Transliteration { get; set; }
    public bool IsHit { get; set; }

    /// <summary>Verse-context cards only: the word is blanked out (<c>text</c> is empty; <c>isHit</c> is true). The answer returns the verse without gaps.</summary>
    public bool IsGap { get; set; }
}

/// <summary>A verse with a head link of one of the entry's keys. Reader route: <c>/bible/{bibleBookId}/{chapter}#v{verse}</c>.</summary>
public sealed class VocabSampleVerseResponse
{
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }

    /// <summary>The German text is the Elberfelder 2027 variant of the verse (else Elb1871).</summary>
    public bool FromRevision { get; set; }

    /// <summary>The link was made by a human (not by the AI user or the seed).</summary>
    public bool HumanMade { get; set; }

    public List<VocabSampleWordResponse> Original { get; set; } = [];
    public List<VocabSampleWordResponse> German { get; set; } = [];
}

public sealed class GetVocabEntryResponse
{
    public VocabEntryDetailResponse Entry { get; set; } = new();
    public List<VocabEntryKeyResponse> StrongsKeys { get; set; } = [];

    /// <summary>Accepted meanings in rank order; empty until the meanings are derived (roadmap 0022.3).</summary>
    public List<VocabMeaningResponse> Meanings { get; set; } = [];

    /// <summary>Renderings by link count, at most <see cref="MaxRenderings"/>; <see cref="RenderingsTotal"/> counts all.</summary>
    public List<VocabRenderingResponse> Renderings { get; set; } = [];

    public const int MaxRenderings = 100;

    public int RenderingsTotal { get; set; }

    /// <summary>All head links of the entry (the 100 percent of <see cref="VocabRenderingResponse.Share"/>).</summary>
    public int TotalLinks { get; set; }

    /// <summary>Last refresh of the renderings; null = never refreshed, <see cref="Renderings"/> is then empty.</summary>
    public DateTime? RefreshedAt { get; set; }

    public List<VocabSampleVerseResponse> SampleVerses { get; set; } = [];
}
