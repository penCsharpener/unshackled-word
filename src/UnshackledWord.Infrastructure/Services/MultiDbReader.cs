using Dapper;
using UnshackledWord.Application.Abstractions;

namespace UnshackledWord.Infrastructure.Services;

public sealed class MultiDbReader : IMultiDbReader
{
    private readonly SqlMapper.GridReader _reader;

    public MultiDbReader(SqlMapper.GridReader reader)
    {
        _reader = reader;
    }

    public async Task<T> ReadFirstAsync<T>()
    {
        return await _reader.ReadFirstAsync<T>();
    }

    public async Task<List<T>> ReadAsync<T>()
    {
        return (await _reader.ReadAsync<T>()).ToList();
    }
}
