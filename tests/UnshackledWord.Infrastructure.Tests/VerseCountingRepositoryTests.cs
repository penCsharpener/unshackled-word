using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using UnshackledWord.Domain.Models.BibleStructure.References;
using UnshackledWord.Infrastructure.Repositories;
using UnshackledWord.Infrastructure.Services;
using UnshackledWord.TestTooling;

namespace UnshackledWord.Infrastructure.Tests;

public class VerseCountingRepositoryTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";

    [Fact]
    public async Task GetDifferingAsync_SelectsOnlyRowsWhereTheCountingsDiffer()
    {
        var reader = new FakeDbReader().Returns(new VerseCountingRow(39004005, 39003023));

        var rows = await new VerseCountingRepository(reader).GetDifferingAsync();

        rows.Single().ShouldBe(new VerseCountingRow(39004005, 39003023));
        reader.LastSql.ShouldContain("\"BibleVerseCountingMapping\"");
        reader.LastSql.ShouldContain("\"LxxRefId\" <> \"HebRefId\"");
    }

    [Fact]
    public async Task GetDifferingAsync_AgainstTheDevDatabase_Returns2026Rows()
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvVar);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString), $"{EnvVar} is not set (needs a copy of the dev database).");

        var factory = new DirectFactory(connectionString!);
        var rows = await new VerseCountingRepository(new DbReader(factory, NullLogger<DbReader>.Instance)).GetDifferingAsync();

        rows.Count.ShouldBe(2026);
        var map = new VerseCountingMap(rows);
        map.MapStart(39004005).ShouldBe(39003023);
        map.MapStart(19003001).ShouldBe(19003002);
        map.MapStart(29002028).ShouldBe(29003001);
        map.MapEnd(19051000).ShouldBe(19051002);
        map.MapStart(43003016).ShouldBe(43003016);
    }

    internal sealed class DirectFactory : UnshackledWord.Application.Abstractions.IDbConnectionFactory
    {
        private readonly string _connectionString;

        public DirectFactory(string connectionString)
        {
            _connectionString = connectionString;
            DbConnection = new NpgsqlConnection(connectionString);
        }

        public System.Data.IDbConnection DbConnection { get; set; }

        public Task BulkInsertAsync<T>(string tableName, string[] columns, ICollection<T> dataList, Action<UnshackledWord.Application.Abstractions.IBinaryImporter, T> mapping, CancellationToken token = default) =>
            throw new NotSupportedException();

        public System.Data.IDbConnection CreateDbConnection() => new NpgsqlConnection(_connectionString);

        public async Task<System.Data.IDbConnection> CreateDbConnectionAsync(CancellationToken token = default)
        {
            var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(token);
            return connection;
        }

        public void Dispose() => DbConnection.Dispose();

        public ValueTask DisposeAsync()
        {
            DbConnection.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
