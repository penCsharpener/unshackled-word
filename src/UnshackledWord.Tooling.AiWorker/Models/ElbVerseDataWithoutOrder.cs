namespace UnshackledWord.Tooling.AiWorker.Models;

public class ElbVerseDataWithoutOrder : VerseDataWithoutOrder
{
    public string German
    {
        get => Word;
        set => Word = value;
    }
}

public class VerseDataWithoutOrder
{
    public int Id { get; set; }
    public string Word { get; set; } = default!;
}
