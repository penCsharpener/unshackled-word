namespace UnshackledWord.Domain.Models.Dbo.Revision;

public sealed class RevisionWordDbo
{
    public const string DboName = "\"unshackled-word\".\"RevisionWords\"";

    public int Id { get; set; }
    public int RevisionVerseId { get; set; }
    public int PositionInVerse { get; set; }
    public string WordInContext { get; set; } = default!;
    public string? PlainWord { get; set; }
    public int? BasePositionInVerse { get; set; }

    /// <summary><c>same</c>, <c>changed</c> or <c>new</c>.</summary>
    public string Change { get; set; } = default!;
}
