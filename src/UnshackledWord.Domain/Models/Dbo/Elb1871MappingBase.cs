namespace UnshackledWord.Domain.Models.Dbo;

public class Elb1871MappingBase
{
    public int Id { get; set; }
    public int ElbWordId { get; set; }
    public int? StepWordId { get; set; }
    public int HebRefId { get; set; }
    public int PositionInVerse { get; set; }
    public bool IsAddedWord { get; set; }
    public int? ParentGermanWordId { get; set; }
    public string? GermanWordPart { get; set; }
}
