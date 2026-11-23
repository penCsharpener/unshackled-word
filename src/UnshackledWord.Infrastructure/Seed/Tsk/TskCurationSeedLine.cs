using System.Globalization;
using CsvHelper.Configuration;

namespace UnshackledWord.Infrastructure.Seed.Tsk;

/// <summary>
/// One line of <c>assets/seed/tsk/curations.csv</c> (roadmap 0024.7, docs/roadmaps/0024-tsk-curation/05-seed-diff.md): one <c>TskCurations</c> row
/// without any user id, e-mail or name. <see cref="Source"/> is the readable source verse and ignored on import.
/// </summary>
public sealed class TskCurationSeedLine
{
    /// <summary><c>UpdatedAt</c> as UTC ISO-8601 with up to six fraction digits (none when whole seconds), so a round trip keeps the exact value.</summary>
    public const string TimeFormat = "yyyy-MM-dd'T'HH:mm:ss.FFFFFF'Z'";

    public string Source { get; set; } = string.Empty;
    public int SourceRef { get; set; }
    public int TargetStart { get; set; }
    public int? TargetEnd { get; set; }
    public string Origin { get; set; } = string.Empty;
    public string? Rating { get; set; }
    public int? StartPosition { get; set; }
    public int? EndPosition { get; set; }
    public string? Quote { get; set; }
    public string? Comment { get; set; }
    public string UpdatedAt { get; set; } = string.Empty;

    public static string FormatTime(DateTimeOffset value) => value.UtcDateTime.ToString(TimeFormat, CultureInfo.InvariantCulture);

    public static bool TryParseTime(string? value, out DateTimeOffset result) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result);
}

/// <summary>Column order and names of <c>tsk/curations.csv</c>.</summary>
public sealed class TskCurationSeedLineMap : ClassMap<TskCurationSeedLine>
{
    public TskCurationSeedLineMap()
    {
        Map(x => x.Source).Index(0).Name("source");
        Map(x => x.SourceRef).Index(1).Name("sourceRef");
        Map(x => x.TargetStart).Index(2).Name("targetStart");
        Map(x => x.TargetEnd).Index(3).Name("targetEnd");
        Map(x => x.Origin).Index(4).Name("origin");
        Map(x => x.Rating).Index(5).Name("rating");
        Map(x => x.StartPosition).Index(6).Name("startPosition");
        Map(x => x.EndPosition).Index(7).Name("endPosition");
        Map(x => x.Quote).Index(8).Name("quote");
        Map(x => x.Comment).Index(9).Name("comment");
        Map(x => x.UpdatedAt).Index(10).Name("updatedAt");
    }
}
