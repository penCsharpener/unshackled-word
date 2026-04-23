namespace UnshackledWord.Tooling.AiWorker.Mapping;

public sealed class ElbStepAiMapping
{
    public int ElbWordId { get; set; }
    public int? StepWordId { get; set; }
    public bool IsAddedWord { get; set; }
    public int? ParentElbWordId { get; set; }
    public int? PartOrder { get; set; }
    public string? GermanWordPart { get; set; }
    public string? InternalElbWord { get; set; }
    public string? InternalStepWord { get; set; }
    public string? InternalParentWord { get; set; }
}
