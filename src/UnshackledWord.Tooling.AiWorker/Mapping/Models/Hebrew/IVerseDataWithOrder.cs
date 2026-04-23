namespace UnshackledWord.Tooling.AiWorker.Mapping.Models.Hebrew;

public interface IVerseDataWithOrder : IVerseDataWithoutOrder
{
    int Order { get; set; }
    int PositionInWord { get; set; }
}
