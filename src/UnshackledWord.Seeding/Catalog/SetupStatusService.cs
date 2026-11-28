using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.WebApi.Admin;

namespace UnshackledWord.Seeding.Catalog;

/// <summary>
/// Reads the row counts of every catalog table and derives the step states (roadmap 0031.5). Two queries: which of the tables are
/// materialized views and whether they are populated (an unpopulated view cannot be counted), then one <c>count(*)</c> per table
/// joined with UNION ALL. The table names come from the constant catalog, never from a request.
/// </summary>
public sealed class SetupStatusService
{
    private const string SelectMaterializedViews = """
        SELECT v.matviewname AS "Name"
             , v.ispopulated AS "Populated"
        FROM pg_matviews v
        WHERE 1=1
            AND v.schemaname = 'unshackled-word'
            AND v.matviewname = ANY(@Names)
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public SetupStatusService(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyDictionary<string, TableCount>> CountTablesAsync(IReadOnlyList<SeedStep> catalog, CancellationToken token)
    {
        var names = catalog.SelectMany(s => s.Tables).Select(t => t.Name).Distinct().ToArray();
        using var connection = await _connectionFactory.CreateDbConnectionAsync(token);

        var views = (await connection.QueryAsync<ViewRow>(new CommandDefinition(SelectMaterializedViews, new { Names = names }, cancellationToken: token)))
            .ToDictionary(v => v.Name, v => v.Populated);

        var countable = names.Where(n => !views.TryGetValue(n, out var populated) || populated).ToArray();
        var counts = new Dictionary<string, TableCount>();
        if (countable.Length > 0)
        {
            var sql = string.Join("\nUNION ALL\n", countable.Select(n => $"""
                SELECT '{n}' AS "Name", count(*) AS "Rows" FROM "unshackled-word"."{n}"
                """));
            foreach (var row in await connection.QueryAsync<CountRow>(new CommandDefinition(sql, cancellationToken: token)))
            {
                counts[row.Name] = new TableCount(row.Name, row.Rows, views.TryGetValue(row.Name, out var p) ? p : null);
            }
        }

        foreach (var (name, populated) in views.Where(v => !v.Value))
        {
            counts[name] = new TableCount(name, 0, false);
        }

        return counts;
    }

    public async Task<SetupStatusResponse> GetStatusAsync(
        IReadOnlySet<string> failedKeys,
        IReadOnlySet<string> completedKeys,
        CancellationToken token)
    {
        var counts = await CountTablesAsync(SeedCatalog.Steps, token);
        return new SetupStatusResponse
        {
            CheckedAt = DateTimeOffset.UtcNow,
            Steps = SetupStatusCalculator.Compute(SeedCatalog.Steps, counts, failedKeys, completedKeys),
        };
    }

    private sealed record ViewRow(string Name, bool Populated);

    private sealed record CountRow(string Name, long Rows);
}
