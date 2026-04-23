namespace UnshackledWord.Tooling.AiWorker.Mapping.Models;

public class ElbVerseDataWithoutOrder : VerseDataWithoutOrder
{
    public string German
    {
        get => Word;
        set => Word = value;
    }
}
