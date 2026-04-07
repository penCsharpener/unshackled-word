namespace UnshackledWord.Tooling.SeedDb.Services.AiMappingImport;

public sealed class ElbWordDto
{
    public int ElbWordId { get; set; }
    public string ElbWord { get; set; } = default!;
    public int HebRefId { get; set; }
    public int PositionInVerse { get; set; }
}