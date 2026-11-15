namespace UnshackledWord.Domain.Models.Dbo.Revision;

public sealed class RevisionMappingDbo
{
    public const string DboName = "\"unshackled-word\".\"RevisionMapping\"";

    public int Id { get; set; }
    public int RevisionWordId { get; set; }

    /// <summary><c>G</c> (books 40-66) or <c>H</c> (books 1-39).</summary>
    public string Lang { get; set; } = default!;
    public int? StepWordId { get; set; }
    public bool IsAddedWord { get; set; }
    public int? ParentRevisionWordId { get; set; }
    public string? GermanWordPart { get; set; }

    /// <summary><c>carried</c>, <c>manual</c> or <c>seed</c>.</summary>
    public string Origin { get; set; } = default!;
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}
