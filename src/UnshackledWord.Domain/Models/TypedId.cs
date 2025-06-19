namespace UnshackledWord.Domain.Models;

public class TypedId<T>(int id) where T : class
{
    public int Id { get; set; } = id;

    public static explicit operator int(TypedId<T> id)
    {
        return id.Id;
    }

    public static implicit operator TypedId<T>(int id)
    {
        return new TypedId<T>(id);
    }
}
