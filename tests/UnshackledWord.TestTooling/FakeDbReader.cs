using System.Collections;
using UnshackledWord.Application.Abstractions;

namespace UnshackledWord.TestTooling;

/// <summary>
/// In-memory <see cref="IDbReader"/> for repository tests. Records every SQL text with its parameters and returns
/// rows queued with <see cref="Returns{T}"/> (FIFO, one queue entry per call; an empty queue returns no rows).
/// </summary>
public sealed class FakeDbReader : IDbReader
{
    private readonly Queue<object?> _results = new();

    public List<(string Sql, object? Param)> Calls { get; } = [];

    public string LastSql => Calls[^1].Sql;

    public FakeDbReader Returns<T>(params T[] rows)
    {
        _results.Enqueue(rows);
        return this;
    }

    /// <summary>Reads a property of the last call's (anonymous) parameter object.</summary>
    public object? LastParam(string name)
    {
        var param = Calls[^1].Param ?? throw new InvalidOperationException("The last call had no parameters.");
        var property = param.GetType().GetProperty(name)
                       ?? throw new InvalidOperationException($"Parameter '{name}' not found on {param.GetType().Name}.");
        return property.GetValue(param);
    }

    public Task<T?> ReadFirstOrDefaultAsync<T>(string sql, object? param = null)
    {
        Calls.Add((sql, param));
        return Task.FromResult(Dequeue<T>().FirstOrDefault());
    }

    public Task<IEnumerable<T>> ReadAsListAsync<T>(string sql, object? param = null)
    {
        Calls.Add((sql, param));
        return Task.FromResult<IEnumerable<T>>(Dequeue<T>());
    }

    public Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null)
    {
        Calls.Add((sql, param));
        return Task.FromResult(Dequeue<T>().FirstOrDefault());
    }

    public Task<List<T>> ReadMultipleAsListAsync<T>(string sql, object? param,
        Func<IMultiDbReader, Task<List<T>>> mappingFunc, CancellationToken token = default)
    {
        throw new NotSupportedException("FakeDbReader does not support multi-result queries.");
    }

    private List<T> Dequeue<T>()
    {
        if (_results.Count == 0)
        {
            return [];
        }

        var next = _results.Dequeue();
        return next is IEnumerable enumerable ? enumerable.Cast<T>().ToList() : [];
    }
}
