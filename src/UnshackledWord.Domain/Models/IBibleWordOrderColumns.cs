namespace UnshackledWord.Domain.Models;

public interface IBibleWordOrderColumns : IEntityId
{
    public int LxxRefId { get; set; }
    public int PositionInVerse { get; set; }
}

public interface IEntityId
{
    public int Id { get; set; }
}
