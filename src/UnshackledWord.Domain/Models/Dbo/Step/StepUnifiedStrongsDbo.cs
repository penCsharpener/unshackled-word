namespace UnshackledWord.Domain.Models.Dbo.Step;

public sealed class StepUnifiedStrongsDbo : IEntityId
{
    public const string DboName = "\"unshackled-word\".\"StepUnifiedStrongs\"";

    public int Id { get; set; }
    public int StepStrongsLexiconId { get; set; }
    public StrongsLanguage LanguageId { get; set; }
    public int Number { get; set; }
    public string? Extra { get; set; }
}
