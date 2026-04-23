namespace UnshackledWord.Tooling.AiWorker.Mapping.Models.Hebrew;

public class StepHebrewVerseData : VerseDataWithoutOrder
{
    public string Hebrew
    {
        get => Word;
        set => Word = value;
    }
}

public sealed class StepHebrewVerseDataWithOrder : StepHebrewVerseData, IVerseDataWithOrder
{
    public int Order { get; set; }
    public int PositionInWord { get; set; }
}
