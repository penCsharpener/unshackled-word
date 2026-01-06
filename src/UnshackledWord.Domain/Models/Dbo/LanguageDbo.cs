namespace UnshackledWord.Domain.Models.Dbo;

public sealed class LanguageDbo
{
    public const string DboName = "\"unshackled-word\".\"Languages\"";
    public int Id { get; set; }
    public string Name { get; set; } = default!;
}
