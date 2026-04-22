namespace UnshackledWord.Tooling.AiWorker.Models;

public class ElbVerseDataWithoutOrder : VerseDataWithoutOrder
{
    public string German
    {
        get => Word;
        set => Word = value;
    }
}
