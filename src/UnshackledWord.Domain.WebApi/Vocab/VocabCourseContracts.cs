using System.Text.Json.Serialization;

namespace UnshackledWord.Domain.WebApi.Vocab;

/// <summary>One vocab course of <c>GET api/vocab/courses</c> (roadmap 0022.1). No per-user numbers yet.</summary>
public sealed class VocabCourseResponse
{
    public string Code { get; set; } = string.Empty;

    /// <summary><c>G</c> (Greek) or <c>H</c> (Hebrew and Aramaic).</summary>
    public string Track { get; set; } = string.Empty;

    public string NameDe { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Entries of the course (ranked entries of the track).</summary>
    public int TotalEntries { get; set; }

    /// <summary>Original-text occurrences of all ranked entries (morphemes are not ranked, so they are not counted).</summary>
    public int TotalOccurrences { get; set; }
}

public sealed class GetVocabCoursesResponse
{
    public List<VocabCourseResponse> Courses { get; set; } = [];
}

/// <summary>Query of <c>GET api/vocab/courses/{code}/entries</c>.</summary>
public sealed class GetVocabCourseEntriesRequest
{
    public const int MaxTake = 100;
    public const int DefaultTake = 50;
    public const int MaxQueryLength = 50;

    public string Code { get; set; } = string.Empty;
    public int Skip { get; set; }
    public int Take { get; set; } = DefaultTake;

    /// <summary>Optional search: normalized lemma prefix, transliteration prefix or German meaning text.</summary>
    public string? Q { get; set; }
}

public sealed class VocabMeaningResponse
{
    public string German { get; set; } = string.Empty;
    public int Rank { get; set; }

    /// <summary>Head links with this rendering at derivation time; null for manual meanings.</summary>
    public int? Count { get; set; }

    /// <summary>Percent of the entry's head links at derivation time; null for manual meanings.</summary>
    public decimal? Share { get; set; }
}

public sealed class VocabCourseEntryResponse
{
    public int Id { get; set; }
    public int Rank { get; set; }
    public string Lemma { get; set; } = string.Empty;
    public string? Transliteration { get; set; }
    public string? DictionaryForm { get; set; }

    /// <summary>Accepted meanings in rank order; empty until the meanings are derived (roadmap 0022.2).</summary>
    public List<VocabMeaningResponse> Meanings { get; set; } = [];

    public int Occurrences { get; set; }
    public bool HasAudio { get; set; }

    /// <summary>Presigned read URL of the pronunciation; null without audio or without configured object storage.</summary>
    public string? AudioUrl { get; set; }

    /// <summary>Internal: the object key behind <see cref="AudioUrl"/>.</summary>
    [JsonIgnore]
    public string? AudioObjectKey { get; set; }

    /// <summary>The maintainer checked the meanings.</summary>
    public bool Reviewed { get; set; }

    public List<string> StrongsKeys { get; set; } = [];
}

public sealed class GetVocabCourseEntriesResponse
{
    public string Code { get; set; } = string.Empty;

    /// <summary>Entries of the course that match <c>q</c> (all when there is none).</summary>
    public int Total { get; set; }

    public int Skip { get; set; }
    public int Take { get; set; }
    public List<VocabCourseEntryResponse> Items { get; set; } = [];
}
