using UnshackledWord.Domain.WebApi.Elberfelder.GetStrongsSummary;

namespace UnshackledWord.Domain.WebApi.Elberfelder.GetWordStats;

public sealed class GetWordStatsResponse
{
    /// <summary>Most frequent spelling found, else the request as given.</summary>
    public string Word { get; set; } = string.Empty;

    /// <summary>"all", "ot" or "nt".</summary>
    public string Testament { get; set; } = "all";

    public int OccurrenceCount { get; set; }
    public int VerseCount { get; set; }

    /// <summary>Words without a non-added mapping (italic/added words, untagged).</summary>
    public int UnmappedCount { get; set; }

    public List<StrongsBookCountResponse> ByBook { get; set; } = [];

    /// <summary>The Strong's numbers behind the word, most used first.</summary>
    public List<WordOriginalResponse> Originals { get; set; } = [];
}

public sealed class WordOriginalResponse
{
    /// <summary>"H6951", "G1577".</summary>
    public string Strongs { get; set; } = string.Empty;

    /// <summary>"greek", "hebrew" or "aramaic".</summary>
    public string Language { get; set; } = string.Empty;

    public string? Lemma { get; set; }
    public string? Transliteration { get; set; }
    public string? Gloss { get; set; }

    /// <summary>Occurrences of this Strong's number rendered with the word (within the testament).</summary>
    public int Count { get; set; }

    /// <summary>All occurrences of the Strong's number.</summary>
    public int TotalCount { get; set; }
}
