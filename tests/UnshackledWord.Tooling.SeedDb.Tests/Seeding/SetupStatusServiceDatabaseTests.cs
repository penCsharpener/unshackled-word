using System.Data;
using Npgsql;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.WebApi.Admin;
using UnshackledWord.Seeding.Catalog;

namespace UnshackledWord.Tooling.SeedDb.Tests.Seeding;

/// <summary>
/// The count query against a real schema. Needs <c>UNSHACKLEDWORD_TEST_POSTGRES</c> (a migrated database, read only) and is skipped otherwise.
/// </summary>
public class SetupStatusServiceDatabaseTests
{
    private const string EnvVar = "UNSHACKLEDWORD_TEST_POSTGRES";

    [Fact]
    public async Task Every_catalog_table_can_be_counted_and_the_views_report_populated()
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvVar);
        Assert.SkipWhen(string.IsNullOrEmpty(connectionString), $"{EnvVar} is not set.");

        var factory = Substitute.For<IDbConnectionFactory>();
        factory.CreateDbConnectionAsync(Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            return (IDbConnection)connection;
        });

        var status = await new SetupStatusService(factory).GetStatusAsync(new HashSet<string>(), new HashSet<string>(), TestContext.Current.CancellationToken);

        status.Steps.Count.ShouldBe(22);
        var views = status.Steps.Single(s => s.Key == "stats-views").Tables;
        views.ShouldAllBe(t => t.Populated != null);
        views.Where(t => t.Populated == false).ShouldAllBe(t => t.Rows == 0);
        status.Steps.Single(s => s.Key == "books").Tables.Single(t => t.Name == "BibleBooks").Rows.ShouldBe(66);
        status.Steps.Single(s => s.Key == "stts").Tables.Single().Populated.ShouldBeNull();
    }
}
