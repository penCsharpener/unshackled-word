namespace UnshackledWord.Application.Abstractions;

public interface IMultiDbReader
{
    Task<T> ReadFirstAsync<T>();
    Task<List<T>> ReadAsync<T>();
}
