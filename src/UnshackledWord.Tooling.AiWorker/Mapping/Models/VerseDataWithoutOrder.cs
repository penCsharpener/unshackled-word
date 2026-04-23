namespace UnshackledWord.Tooling.AiWorker.Mapping.Models;

public class VerseDataWithoutOrder : IVerseDataWithoutOrder
{
    public int Id { get; set; }
    public string Word { get; set; } = default!;
}
