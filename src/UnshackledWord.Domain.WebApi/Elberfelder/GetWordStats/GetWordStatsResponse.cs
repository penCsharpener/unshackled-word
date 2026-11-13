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

    /// <summary>Snapshot only: the lemma the typed word was resolved to ("ging" gives "gehen"). Absent for unknown words and in the live fallback.</summary>
    public string? Lemma { get; set; }

    /// <summary>Snapshot only: the part of speech of the resolved lemma when exactly one (or the requested) PoS is selected, else absent (summed over PoS).</summary>
    public string? Pos { get; set; }

    /// <summary>Snapshot only: every lemma + PoS the word resolves to (for a PoS switch), most frequent first.</summary>
    public List<WordLemmaCandidate>? Candidates { get; set; }

    /// <summary>When the stats snapshot was last refreshed; absent while the live fallback answered.</summary>
    public DateTime? StatsRefreshedAt { get; set; }
}

public sealed class WordLemmaCandidate
{
    public string Lemma { get; set; } = string.Empty;
    public string? Pos { get; set; }

    /// <summary>Occurrences of this lemma + PoS over the whole Bible.</summary>
    public int Occurrences { get; set; }
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
