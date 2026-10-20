namespace UnshackledWord.Domain.Models.Study;

public sealed class StepBibleVerse
{
    public int HebRefId { get; init; }
    public string VerseText { get; init; } = default!;
    public string LemmaText { get; init; } = default!;
}
