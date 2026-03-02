namespace UnshackledWord.Tooling.AiWorker;

public sealed class ElbStepAiMapping
{
    public int ElbWordId { get; set; }
    public int? StepWordId { get; set; }
    public bool IsAddedWord { get; set; }
    public int? ParentElbWordId { get; set; }
}
