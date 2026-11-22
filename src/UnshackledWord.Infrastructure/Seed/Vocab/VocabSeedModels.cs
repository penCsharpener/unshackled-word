using CsvHelper.Configuration;

namespace UnshackledWord.Infrastructure.Seed.Vocab;

/// <summary><c>vocab/entries.csv</c>: one line per live (not retired) entry, identity <c>(Track, Lemma, Homograph)</c>.</summary>
public sealed class VocabSeedEntryLine
{
    public string Track { get; set; } = string.Empty;
    public string Lemma { get; set; } = string.Empty;

    /// <summary>
    /// 1 for the only (or first) live entry with this lemma in the track; a split leaves two entries with the same lemma (H1254A/H1254B),
    /// numbered by their lowest Strong's key (ordinal), then id. Part of the identity in all three files. Optional when reading (default 1).
    /// </summary>
    public int Homograph { get; set; } = 1;
    public int LanguageId { get; set; }
    public string LemmaNormalized { get; set; } = string.Empty;
    public string? DictionaryForm { get; set; }
    public string? DictionaryFormOrigin { get; set; }
    public string? Transliteration { get; set; }
    public string? Pos { get; set; }
    public bool IsProperName { get; set; }
    public bool IsMorpheme { get; set; }
    public int Occurrences { get; set; }
    public int? FrequencyRank { get; set; }

    /// <summary>The key only; the audio object itself lives in Garage and is not part of the seed.</summary>
    public string? AudioObjectKey { get; set; }
}

/// <summary><c>vocab/keys.csv</c>: the Strong's keys of an entry.</summary>
public sealed class VocabSeedKeyLine
{
    public string Track { get; set; } = string.Empty;
    public string Lemma { get; set; } = string.Empty;

    /// <summary>
    /// 1 for the only (or first) live entry with this lemma in the track; a split leaves two entries with the same lemma (H1254A/H1254B),
    /// numbered by their lowest Strong's key (ordinal), then id. Part of the identity in all three files. Optional when reading (default 1).
    /// </summary>
    public int Homograph { get; set; } = 1;
    public string StrongsKey { get; set; } = string.Empty;
    public int Occurrences { get; set; }
}

/// <summary><c>vocab/meanings.csv</c>: the German meanings of an entry (derived and manual, accepted or not, pinned or not).</summary>
public sealed class VocabSeedMeaningLine
{
    /// <summary>Text shown and accepted instead of <see cref="German"/> (maintainer override); empty when none. German, count and share stay.</summary>
    public string? GermanOverride { get; set; }

    public string Track { get; set; } = string.Empty;
    public string Lemma { get; set; } = string.Empty;

    /// <summary>
    /// 1 for the only (or first) live entry with this lemma in the track; a split leaves two entries with the same lemma (H1254A/H1254B),
    /// numbered by their lowest Strong's key (ordinal), then id. Part of the identity in all three files. Optional when reading (default 1).
    /// </summary>
    public int Homograph { get; set; } = 1;
    public string German { get; set; } = string.Empty;
    public string? GermanKey { get; set; }
    public int Rank { get; set; }
    public int? Count { get; set; }
    public decimal? Share { get; set; }
    public bool IsAccepted { get; set; }
    public string Origin { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
}

public sealed class VocabSeedEntryLineMap : ClassMap<VocabSeedEntryLine>
{
    public VocabSeedEntryLineMap()
    {
        Map(x => x.Track).Index(0).Name("Track");
        Map(x => x.Lemma).Index(1).Name("Lemma");
        Map(x => x.LanguageId).Index(2).Name("LanguageId");
        Map(x => x.LemmaNormalized).Index(3).Name("LemmaNormalized");
        Map(x => x.DictionaryForm).Index(4).Name("DictionaryForm");
        Map(x => x.DictionaryFormOrigin).Index(5).Name("DictionaryFormOrigin");
        Map(x => x.Transliteration).Index(6).Name("Transliteration");
        Map(x => x.Pos).Index(7).Name("Pos");
        Map(x => x.IsProperName).Index(8).Name("IsProperName");
        Map(x => x.IsMorpheme).Index(9).Name("IsMorpheme");
        Map(x => x.Occurrences).Index(10).Name("Occurrences");
        Map(x => x.FrequencyRank).Index(11).Name("FrequencyRank");
        Map(x => x.AudioObjectKey).Index(12).Name("AudioObjectKey");
        Map(x => x.Homograph).Index(13).Name("Homograph").Optional().Default(1);
    }
}

public sealed class VocabSeedKeyLineMap : ClassMap<VocabSeedKeyLine>
{
    public VocabSeedKeyLineMap()
    {
        Map(x => x.Track).Index(0).Name("Track");
        Map(x => x.Lemma).Index(1).Name("Lemma");
        Map(x => x.StrongsKey).Index(2).Name("StrongsKey");
        Map(x => x.Occurrences).Index(3).Name("Occurrences");
        Map(x => x.Homograph).Index(4).Name("Homograph").Optional().Default(1);
    }
}

public sealed class VocabSeedMeaningLineMap : ClassMap<VocabSeedMeaningLine>
{
    public VocabSeedMeaningLineMap()
    {
        Map(x => x.Track).Index(0).Name("Track");
        Map(x => x.Lemma).Index(1).Name("Lemma");
        Map(x => x.German).Index(2).Name("German");
        Map(x => x.GermanKey).Index(3).Name("GermanKey");
        Map(x => x.Rank).Index(4).Name("Rank");
        Map(x => x.Count).Index(5).Name("Count");
        Map(x => x.Share).Index(6).Name("Share");
        Map(x => x.IsAccepted).Index(7).Name("IsAccepted");
        Map(x => x.Origin).Index(8).Name("Origin");
        Map(x => x.IsPinned).Index(9).Name("IsPinned");
        Map(x => x.Homograph).Index(10).Name("Homograph").Optional().Default(1);
        Map(x => x.GermanOverride).Index(11).Name("GermanOverride").Optional();
    }
}

/// <summary>The whole vocab seed in memory; the files are serialised from it and the importer applies it.</summary>
public sealed class VocabSeedData
{
    public List<VocabSeedEntryLine> Entries { get; set; } = [];
    public List<VocabSeedKeyLine> Keys { get; set; } = [];
    public List<VocabSeedMeaningLine> Meanings { get; set; } = [];
}
