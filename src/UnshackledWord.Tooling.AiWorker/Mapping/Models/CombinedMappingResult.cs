namespace UnshackledWord.Tooling.AiWorker.Mapping.Models;

public sealed class CombinedMappingResult<T> where T : VerseDataWithoutOrder
{
    public List<VerseDataList<ElbStepAiMapping>> Response { get; set; } = default!;
    public List<ElbVerseData> ElbWords { get; set; } = default!;
    public List<T> StepWords { get; set; } = default!;
}
