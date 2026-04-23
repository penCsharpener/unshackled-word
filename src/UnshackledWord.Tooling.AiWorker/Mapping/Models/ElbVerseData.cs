using UnshackledWord.Tooling.AiWorker.Mapping.Models.Hebrew;

namespace UnshackledWord.Tooling.AiWorker.Mapping.Models;

public sealed class ElbVerseData : ElbVerseDataWithoutOrder, IVerseDataWithOrder
{
    public int Order { get; set; }
    public int PositionInWord { get; set; }
}
