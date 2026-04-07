namespace UnshackledWord.Tooling.SeedDb.Services.AiMappingImport;

public sealed class StepWordDto
{
    public int StepWordId { get; set; }
    public string StepWord { get; set; } = default!;
    public int HebRefId { get; set; }
    public int PositionInVerse { get; set; }
}