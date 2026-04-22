using UnshackledWord.Tooling.AiWorker.Models.Hebrew;

namespace UnshackledWord.Tooling.AiWorker.Models;

public sealed class ElbVerseData : ElbVerseDataWithoutOrder, IVerseDataWithOrder
{
    public int Order { get; set; }
    public int PositionInWord { get; set; }
}
