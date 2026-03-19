namespace UnshackledWord.Domain.Models;

public interface IBibleWordOrderColumns : IEntityId
{
    public int BibleBookId { get; set; }
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public int RefId { get; set; }
    public int PositionInVerse { get; set; }
}

public interface IEntityId
{
    public int Id { get; set; }
}
