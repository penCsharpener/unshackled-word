using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.CrossReferences;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Infrastructure.Repositories;
using UnshackledWord.Infrastructure.Services;
using UnshackledWord.TestTooling;
using UnshackledWord.Tooling.SeedDb.Services.Tsk;

namespace UnshackledWord.Tooling.SeedDb.Tests.Services.Tsk;

/// <summary>
/// Roadmap 0024.2: the TSK seed writes <c>Sort</c> and the Elberfelder keys (<see cref="TskKeySql"/>), and the chapter cross references it produces equal
/// the former read path. Re-seeds 1Mo 1–3 and Röm 8 from <c>assets/tskxref.txt</c> inside one transaction that is rolled back. Needs
/// <c>UNSHACKLEDWORD_TEST_POSTGRES</c> pointing at a COPY of a seeded database with migration 0044 applied; skipped otherwise.
/// </summary>
public class TskSeedDatabaseTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";

    private static readonly (int Book, int Chapter)[] Chapters = [(1, 1), (1, 2), (1, 3), (45, 8)];

    [Fact]
    public async Task Reseed_WritesSortAndKeys_AndKeepsTheChapterCrossReferences()
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvVar);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString), $"{EnvVar} is not set.");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var reader = new TransactionDbAccess(connection, transaction);

        var before = await ChapterResponsesAsync(new ChapterCrossReferenceService(new LegacyTskRepository(reader)));

        var deleted = await connection.ExecuteAsync("""
            DELETE FROM "unshackled-word"."Tsk"
            WHERE "LxxRefId" BETWEEN 1001000 AND 1003999
                OR "LxxRefId" BETWEEN 45008000 AND 45008999
            """, transaction: transaction);
        deleted.ShouldBeGreaterThan(0);

        var entries = (await NewTextReader().ReadAsync(TestContext.Current.CancellationToken))
            .Where(e => Chapters.Contains((e.Reference.BookId, e.Reference.Chapter)))
            .ToList();
        await new TskStrategy(reader, NewTextReader()).InsertAsync(entries, TestContext.Current.CancellationToken);

        var after = await ChapterResponsesAsync(new ChapterCrossReferenceService(new TskRepository(reader)));
        var seeded = (await connection.QueryAsync<(int LxxRefId, int? Sort, int? SourceHebRefId, int? TargetStartHebRefId, int? TargetEndHebRefId, string Scope)>("""
            SELECT t."LxxRefId", t."Sort", t."SourceHebRefId", t."TargetStartHebRefId", t."TargetEndHebRefId", t."Scope"
            FROM "unshackled-word"."Tsk" t
            WHERE t."LxxRefId" BETWEEN 1001000 AND 1003999
                OR t."LxxRefId" BETWEEN 45008000 AND 45008999
            ORDER BY t."Id"
            """, transaction: transaction)).ToList();

        await transaction.RollbackAsync(TestContext.Current.CancellationToken);

        seeded.Count.ShouldBe(deleted);
        seeded.ShouldAllBe(r => r.Sort != null && r.SourceHebRefId == r.LxxRefId && r.TargetStartHebRefId != null);
        // 1Mo 1,1: "beginning" is the file's sort 2, its first target Spr 8,22-24 is kept as a range.
        seeded.First(r => r.LxxRefId == 1001001 && r.Scope == "beginning").ShouldBe((1001001, 2, 1001001, 20008022, 20008024, "beginning"));
        after.ShouldBe(before);
    }

    private static async Task<List<string>> ChapterResponsesAsync(ChapterCrossReferenceService service)
    {
        var responses = new List<string>();
        foreach (var (book, chapter) in Chapters)
        {
            var response = await service.GetAsync(book, chapter);
            response.Verses.ShouldNotBeEmpty();
            responses.Add(JsonSerializer.Serialize(response));
        }

        return responses;
    }

    private static TskTextReader NewTextReader()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "UnshackledWord.slnx")))
        {
            dir = dir.Parent;
        }

        var settings = new AppSettings
        {
            DatabaseSeeding = new DatabaseSeedSettings { SolutionAssetsPath = Path.Combine(dir!.FullName, "assets"), TskFilePath = "tskxref.txt" },
        };

        return new TskTextReader(new FileService(), Options.Create(settings), NullLogger<TskTextReader>.Instance);
    }

    /// <summary>Reader and writer on one open connection and transaction.</summary>
    private sealed class TransactionDbAccess(IDbConnection connection, IDbTransaction transaction) : IDbReader, IDbWriter
    {
        public Task<T?> ReadFirstOrDefaultAsync<T>(string sql, object? param = null) => connection.QueryFirstOrDefaultAsync<T>(sql, param, transaction);

        public async Task<IEnumerable<T>> ReadAsListAsync<T>(string sql, object? param = null) => await connection.QueryAsync<T>(sql, param, transaction);

        public Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null) => connection.ExecuteScalarAsync<T>(sql, param, transaction);

        Task<T?> IDbWriter.ExecuteScalarAsync<T>(string sql, object? param, int? commandTimeout) where T : default =>
            connection.ExecuteScalarAsync<T>(sql, param, transaction, commandTimeout);

        public Task<List<T>> ReadMultipleAsListAsync<T>(string sql, object? param, Func<IMultiDbReader, Task<List<T>>> mappingFunc, CancellationToken token = default) =>
            throw new NotSupportedException();

        public Task<int> WriteAsync<T>(string sql, T parameters) => connection.ExecuteAsync(sql, parameters, transaction);

        public Task<int> WriteAsync(string sql) => connection.ExecuteAsync(sql, transaction: transaction);

        public Task BulkInsertAsync<T>(string tableName, string[] columns, ICollection<T> dataList, Action<IBinaryImporter, T> mapping, CancellationToken token = default) =>
            throw new NotSupportedException();
    }
}
