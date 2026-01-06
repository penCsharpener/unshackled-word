namespace UnshackledWord.Domain.Models.Dbo;

public sealed class BibleBookDbo
{
    public const string DboName = "\"unshackled-word\".\"BibleBooks\"";

    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string Abbreviations { get; set; } = default!;
    public int LanguageId { get; set; }
}
