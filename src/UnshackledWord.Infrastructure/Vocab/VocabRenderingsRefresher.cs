using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Infrastructure.Vocab;

/// <summary>
/// Refreshes the materialized view <c>StatsVocabRenderings</c> (roadmap 0022.2, migration 0038): plain the first time (CONCURRENTLY needs data),
/// CONCURRENTLY afterwards, then stamps <c>DqRefreshState.VocabRenderingsRefreshedAt</c>. Takes about 30 s. Used by the maintainer job
/// (<c>POST api/maintainer/vocab/renderings/refresh</c>) and at the end of a seed rebase.
/// </summary>
public sealed class VocabRenderingsRefresher
{
    public const string Kind = "vocab";
    public const string Action = "renderings";

    private const int TimeoutSeconds = 900;

    private const string SelectIsPopulated = """
        SELECT COALESCE(bool_or(v.ispopulated), false)
        FROM pg_matviews v
        WHERE 1=1
            AND v.schemaname = 'unshackled-word'
            AND v.matviewname = 'StatsVocabRenderings'
        """;

    private const string RefreshPlain = """REFRESH MATERIALIZED VIEW "unshackled-word"."StatsVocabRenderings" """;
    private const string RefreshConcurrently = """REFRESH MATERIALIZED VIEW CONCURRENTLY "unshackled-word"."StatsVocabRenderings" """;

    private const string UpdateRefreshedAt = """
        UPDATE "unshackled-word"."DqRefreshState"
        SET "VocabRenderingsRefreshedAt" = now()
        WHERE 1=1
            AND "Id" = 1
        """;

    private const string SelectCounts = """
        SELECT count(*)::int                                  AS "Rows"
             , COALESCE(sum(r."LinkCount"), 0)::int           AS "Links"
             , count(DISTINCT (r."Lang", r."StrongsKey"))::int AS "Keys"
        FROM "unshackled-word"."StatsVocabRenderings" r
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public VocabRenderingsRefresher(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<SeedStepReport> RefreshAsync(IProgress<string>? progress, CancellationToken token)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(token);

        var populated = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(SelectIsPopulated, cancellationToken: token));
        progress?.Report(populated ? "Refreshing StatsVocabRenderings concurrently" : "Filling StatsVocabRenderings for the first time");
        await connection.ExecuteAsync(new CommandDefinition(populated ? RefreshConcurrently : RefreshPlain, commandTimeout: TimeoutSeconds, cancellationToken: token));
        await connection.ExecuteAsync(new CommandDefinition(UpdateRefreshedAt, cancellationToken: token));

        var counts = await connection.QuerySingleAsync<CountRow>(new CommandDefinition(SelectCounts, cancellationToken: token));
        progress?.Report($"StatsVocabRenderings: {counts.Rows} rows, {counts.Keys} Strong's keys, {counts.Links} head links");

        var report = new SeedStepReport { Kind = Kind, Action = Action };
        report.Counts["rows"] = counts.Rows;
        report.Counts["keys"] = counts.Keys;
        report.Counts["links"] = counts.Links;

        return report;
    }

    private sealed class CountRow
    {
        public int Rows { get; init; }
        public int Links { get; init; }
        public int Keys { get; init; }
    }
}
