namespace UnshackledWord.Domain.Models.Dbo.Revision;

public sealed class RevisionLevelDbo
{
    public const string DboName = "\"unshackled-word\".\"Revisions\"";

    public int Id { get; set; }

    public int Level { get; set; }
    public string Code { get; set; } = default!;
    public string NameDe { get; set; } = default!;
    public string NameEn { get; set; } = default!;
    public string? DescriptionDe { get; set; }
    public string? DescriptionEn { get; set; }
    public bool IsBase { get; set; }
    public bool IsDefault { get; set; }
}
