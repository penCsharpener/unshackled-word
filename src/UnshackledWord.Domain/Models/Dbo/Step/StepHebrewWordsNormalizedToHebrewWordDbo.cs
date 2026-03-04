namespace UnshackledWord.Domain.Models.Dbo.Step;

public sealed class StepHebrewWordsNormalizedToHebrewWordDbo
{
    public const string DbName = "\"unshackled-word\".\"StepHebrewWordsNormalizedToHebrewWords\"";
    public int StepHebrewWordsId { get; set; }
    public int StepHebrewWordsNormalizedId { get; set; }
    public int PositionInWord { get; set; }
}
