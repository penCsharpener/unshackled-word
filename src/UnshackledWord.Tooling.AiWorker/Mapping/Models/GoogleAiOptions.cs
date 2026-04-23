namespace UnshackledWord.Tooling.AiWorker.Mapping.Models;

public class GoogleAiOptions
{
    public string ApiKey { get; set; } = default!;
    public int MaxParallelTasks { get; set; } = 5;
    public int VersesPerTask { get; set; } = 10;
}
