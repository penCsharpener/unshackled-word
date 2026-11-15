namespace UnshackledWord.Domain.Models.Dbo.Revision;

public sealed class RevisionVerseDbo
{
    public const string DboName = "\"unshackled-word\".\"RevisionVerses\"";

    public int Id { get; set; }
    public int HebRefId { get; set; }
    public int RevisionId { get; set; }
    public int Version { get; set; }
    public string VerseText { get; set; } = default!;
    public int? BasedOnRevisionVerseId { get; set; }
    public string BaseTextHash { get; set; } = default!;
    public string? Note { get; set; }
    public bool IsActive { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? EndedByUserId { get; set; }
    public DateTime? EndedAt { get; set; }
    public int? MappingReviewedByUserId { get; set; }
    public DateTime? MappingReviewedAt { get; set; }
}
