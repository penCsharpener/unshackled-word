namespace UnshackledWord.Domain.Models.Dbo.Step;

public class StrongsNumberDbo : IEntityId
{
    public const string DbName = "\"unshackled-word\".\"StrongsNumbers\"";

    public int Id { get; set; }
    public StrongsLanguage LanguageId { get; set; }
    public int Number { get; set; }
    /// <summary>
    /// when the strongs number is followed by a letter or underscore letter
    /// </summary>
    public string? Extra { get; set; }
    public StrongsType StrongsType { get; set; }
    /// <summary>
    /// when the strongs number is surrounded by braces
    /// </summary>
    public bool IsRoot { get; set; }
    /// <summary>
    /// when the strongs number is followed by a +
    /// </summary>
    public bool CoversNextWord { get; set; }
    public int? StepHebrewWordId { get; set; }
    public int? StepGreekWordId { get; set; }
    public int Order { get; set; }
}

public enum StrongsLanguage
{
    Hebrew = 0,
    Aramaic = 1,
    Greek = 2
}

public enum StrongsType
{
    Extended = 0,
    Disambiguated = 1,
    Unified = 2
}
