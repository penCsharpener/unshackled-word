namespace UnshackledWord.Tooling.AiWorker.Models.Hebrew;

public class StepHebrewVerseData
{
    public int Id { get; set; }
    public string Hebrew { get; set; } = default!;
}

public sealed class StepHebrewVerseDataWithOrder : StepHebrewVerseData
{
    public int Order { get; set; }
}
