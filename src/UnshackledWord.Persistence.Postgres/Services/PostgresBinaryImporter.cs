using Npgsql;
using NpgsqlTypes;
using UnshackledWord.Application.Abstractions;

namespace UnshackledWord.Persistence.Postgres.Services;

public class PostgresBinaryImporter : IBinaryImporter, IDisposable, IAsyncDisposable
{
    private readonly NpgsqlBinaryImporter _writer;

    public PostgresBinaryImporter(NpgsqlBinaryImporter writer)
    {
        _writer = writer;
    }

    public async Task StartRowAsync(CancellationToken token = default)
    {
        await _writer.StartRowAsync(token);
    }

    public async Task CompleteAsync(CancellationToken token = default)
    {
        await _writer.CompleteAsync(token);
    }

    public async Task WriteAsync<T>(T value, CancellationToken token = default)
    {
        if (value is null)
        {
            return;
        }

        var npgType = typeof(T) switch
        {
            var t when t == typeof(int) => NpgsqlDbType.Integer,
            var t when t == typeof(long) => NpgsqlDbType.Bigint,
            var t when t == typeof(string) => NpgsqlDbType.Text,
            var t when t == typeof(bool) => NpgsqlDbType.Boolean,
            var t when t == typeof(DateTime) => NpgsqlDbType.Timestamp,
            var t when t == typeof(double) => NpgsqlDbType.Double,
            var t when t == typeof(byte[]) => NpgsqlDbType.Bytea,
            _ => throw new NotSupportedException($"Type {typeof(T)} is not mapped.")
        };

        await _writer.WriteAsync(value, npgType, token);
    }

    public void Dispose()
    {
        _writer.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _writer.DisposeAsync();
    }
}
