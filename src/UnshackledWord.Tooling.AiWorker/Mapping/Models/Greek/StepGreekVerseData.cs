using UnshackledWord.Tooling.AiWorker.Mapping.Models.Hebrew;

namespace UnshackledWord.Tooling.AiWorker.Mapping.Models.Greek;

public class StepGreekVerseData : VerseDataWithoutOrder
{
    public string Greek
    {
        get => Word;
        set => Word = value;
    }
}

public sealed class StepGreekVerseDataWithOrder : StepGreekVerseData, IVerseDataWithOrder
{
    public int Order { get; set; }
    public int PositionInWord { get; set; }
}
