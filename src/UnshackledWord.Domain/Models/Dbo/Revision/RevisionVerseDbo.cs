namespace UnshackledWord.Domain.Models.Dbo.Revision;

public sealed class RevisionVerseDbo
{
    public const string DboName = "\"unshackled-word\".\"RevisionVerses\"";

    public int Id { get; set; }
    public int HebRefId { get; set; }
    public int RevisionId { get; set; }
    public int Version { get; set; }
    public string VerseText { get; set; } = default!;
    public int? BasedOnRevisionId { get; set; }
    public int? BasedOnVersion { get; set; }
    public string BaseTextHash { get; set; } = default!;
    public string? Note { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? MappingReviewedByUserId { get; set; }
    public DateTime? MappingReviewedAt { get; set; }
}
