namespace UnshackledWord.Domain.Models.Dbo;

public sealed class SblApparatusDbo
{
    public const string DboName = "\"unshackled-word\".\"SblApparatus\"";

    public int Id { get; set; }
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public int RefId { get; set; }
    public string Text { get; set; } = default!;
}