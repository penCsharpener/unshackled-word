namespace UnshackledWord.Seeding.RevisionSeed;

/// <summary>One line of assets/seed/revision/levels.csv (column names = header names).</summary>
public sealed class RevisionLevelCsvRow
{
    public int Level { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameDe { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? DescriptionDe { get; set; }
    public string? DescriptionEn { get; set; }
    public bool IsDefault { get; set; }
}
