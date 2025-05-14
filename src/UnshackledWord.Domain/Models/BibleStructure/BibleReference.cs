namespace UnshackledWord.Domain.Models.BibleStructure;

public record struct BibleReference
{
    public int BookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
}
