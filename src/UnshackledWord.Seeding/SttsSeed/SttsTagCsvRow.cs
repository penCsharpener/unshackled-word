namespace UnshackledWord.Seeding.SttsSeed;

/// <summary>One line of assets/seed/stts-tags.csv (column names = header names).</summary>
public sealed class SttsTagCsvRow
{
    public string Tag { get; set; } = string.Empty;
    public string LabelDe { get; set; } = string.Empty;
    public string LabelEn { get; set; } = string.Empty;
    public string DescriptionDe { get; set; } = string.Empty;
    public string DescriptionEn { get; set; } = string.Empty;
    public string ExampleDe { get; set; } = string.Empty;
    public string Upos { get; set; } = string.Empty;

    /// <summary>"|"-separated UD tags.</summary>
    public string AllowedUpos { get; set; } = string.Empty;

    public string? Alias { get; set; }
    public bool IsPunctuation { get; set; }
    public int SortOrder { get; set; }
}
