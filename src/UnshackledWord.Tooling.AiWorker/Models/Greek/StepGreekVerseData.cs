namespace UnshackledWord.Tooling.AiWorker.Models.Greek;

public class StepGreekVerseData : VerseDataWithoutOrder
{
    public string Greek
    {
        get => Word;
        set => Word = value;
    }
}

public sealed class StepGreekVerseDataWithOrder : StepGreekVerseData
{
    public int Order { get; set; }
    public int PositionInWord { get; set; }
}
