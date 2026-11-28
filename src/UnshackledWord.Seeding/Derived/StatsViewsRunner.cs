using Dapper;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Seeding.Abstractions;

namespace UnshackledWord.Seeding.Derived;

/// <summary>
/// Setup step <c>stats-views</c> (roadmap 0031.9): refreshes the six materialized views in dependency order. <c>DqWordFacts</c> comes first, the four
/// mapping statistics (<c>StatsStrongsRenderings</c>, <c>StatsStrongs</c>, <c>StatsGermanLemma</c>, <c>StatsGermanForms</c>) read it, and
/// <c>StatsVocabRenderings</c> reads the raw mapping tables and is refreshed by <see cref="VocabRenderingsRefresher"/> (which stamps its own time).
/// A view that was never filled is refreshed plain (CONCURRENTLY needs data), a filled one CONCURRENTLY so readers keep the old snapshot; the refresh is
/// idempotent, which is why this is the one step the page lets you run again while green. The command timeout is the seed one
/// (<c>Seeding:CommandTimeoutSeconds</c>, default 900 s) for every statement. The data quality check counts are not touched: the maintainer refreshes those.
/// </summary>
public sealed class StatsViewsRunner : IRunner
{
    /// <summary>The views refreshed here before the vocab view, in dependency order.</summary>
    public static readonly IReadOnlyList<string> Views =
        ["DqWordFacts", "StatsStrongsRenderings", "StatsStrongs", "StatsGermanLemma", "StatsGermanForms"];

    private const string SelectPopulated = """
        SELECT v.matviewname
        FROM pg_matviews v
        WHERE 1=1
            AND v.schemaname = 'unshackled-word'
            AND v.ispopulated
            AND v.matviewname = ANY(@Names)
        """;

    private const string UpdateStatsRefreshedAt = """
        UPDATE "unshackled-word"."DqRefreshState"
        SET "StatsRefreshedAt" = now()
        WHERE 1=1
            AND "Id" = 1
        """;

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly VocabRenderingsRefresher _vocab;
    private readonly IOptions<SeedingOptions> _options;
    private readonly ILogger<StatsViewsRunner> _logger;

    public StatsViewsRunner(IDbConnectionFactory connectionFactory, VocabRenderingsRefresher vocab, IOptions<SeedingOptions> options, ILogger<StatsViewsRunner> logger)
    {
        _connectionFactory = connectionFactory;
        _vocab = vocab;
        _options = options;
        _logger = logger;
    }

    /// <summary>One REFRESH statement per view in <see cref="Views"/> order: plain while a view is unpopulated, CONCURRENTLY otherwise.</summary>
    public static IReadOnlyList<string> RefreshStatements(IReadOnlySet<string> populated) =>
    [
        .. Views.Select(view => populated.Contains(view)
            ? $"REFRESH MATERIALIZED VIEW CONCURRENTLY \"unshackled-word\".\"{view}\""
            : $"REFRESH MATERIALIZED VIEW \"unshackled-word\".\"{view}\"")
    ];

    public async Task Run(CancellationToken token = default)
    {
        var timeout = _options.Value.CommandTimeoutSeconds > 0 ? _options.Value.CommandTimeoutSeconds : SeedingOptions.DefaultCommandTimeoutSeconds;

        using (var connection = await _connectionFactory.CreateDbConnectionAsync(token))
        {
            var populated = (await connection.QueryAsync<string>(new CommandDefinition(SelectPopulated, new { Names = Views.ToArray() }, cancellationToken: token))).ToHashSet();
            var statements = RefreshStatements(populated);
            for (var i = 0; i < statements.Count; i++)
            {
                _logger.LogInformation("Refreshing {View}", Views[i]);
                await connection.ExecuteAsync(new CommandDefinition(statements[i], commandTimeout: timeout, cancellationToken: token));
            }

            await connection.ExecuteAsync(new CommandDefinition(UpdateStatsRefreshedAt, cancellationToken: token));
        }

        _logger.LogInformation("Refreshing StatsVocabRenderings");
        await _vocab.RefreshAsync(null, token);
    }
}
