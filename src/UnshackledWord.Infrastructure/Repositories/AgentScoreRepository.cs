using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.AgentMapping.Scoring;

namespace UnshackledWord.Infrastructure.Repositories;

/// <summary>Reads the proposals and package costs of a run for the reference-set scorer and stores its result (migration 0034).</summary>
public sealed class AgentScoreRepository : IAgentScoreRepository
{
    private readonly IDbConnectionFactory _factory;

    public AgentScoreRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<ScoredProposal>> GetProposalsAsync(int runId, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        return (await connection.QueryAsync<ScoredProposal>(new CommandDefinition(AgentScoreSql.Proposals, new { RunId = runId }, cancellationToken: token))).AsList();
    }

    public async Task<IReadOnlyList<ScorePackageRow>> GetPackagesAsync(int runId, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        return (await connection.QueryAsync<ScorePackageRow>(new CommandDefinition(AgentScoreSql.Packages, new { RunId = runId }, cancellationToken: token))).AsList();
    }

    public async Task SaveScoreAsync(int runId, string scoreJson, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        await connection.ExecuteAsync(new CommandDefinition(AgentScoreSql.SaveScore, new { RunId = runId, Score = scoreJson }, cancellationToken: token));
    }

    public async Task<string?> GetStoredScoreAsync(int runId, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        return await connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(AgentScoreSql.StoredScore, new { RunId = runId }, cancellationToken: token));
    }
}

public static class AgentScoreSql
{
    public const string StoredScore = """
                                      SELECT r."Score"::text
                                      FROM "unshackled-word"."AgentRuns" r
                                      WHERE 1=1
                                          AND r."Id" = @RunId
                                      """;

    public const string Proposals = """
                                    SELECT p."Id"
                                         , p."BibleBookId"
                                         , p."Chapter"
                                         , p."Verse"
                                         , p."Status"
                                         , p."Links"::text "LinksJson"
                                         , p."Lemmas"::text "LemmasJson"
                                         , p."Roles"::text "RolesJson"
                                         , p."Flags"::text "FlagsJson"
                                    FROM "unshackled-word"."AgentVerseProposals" p
                                    WHERE 1=1
                                        AND p."RunId" = @RunId
                                    ORDER BY p."Id"
                                    """;

    public const string Packages = """
                                   SELECT p."Id"
                                        , p."LocalIdMap"::text "LocalIdMap"
                                        , p."InputTokens"
                                        , p."CachedInputTokens"
                                        , p."OutputTokens"
                                        , p."CostUsd"
                                   FROM "unshackled-word"."AgentPackages" p
                                   WHERE 1=1
                                       AND p."RunId" = @RunId
                                   ORDER BY p."FromHebRefId"
                                   """;

    public const string SaveScore = """
                                    UPDATE "unshackled-word"."AgentRuns"
                                    SET "Score" = @Score::jsonb
                                      , "ScoredAt" = now()
                                    WHERE 1=1
                                        AND "Id" = @RunId
                                    """;
}
