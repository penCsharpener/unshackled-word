namespace UnshackledWord.Tooling.AiWorker.Models;

public sealed class StepGreekVerseData
{
    public int Id { get; set; }
    public string Greek { get; set; } = default!;
    public int Order { get; set; }
}
