using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.AgentMapping;

namespace UnshackledWord.Infrastructure.Repositories;

/// <summary>Reads and closes rows of <c>AgentVerseProposals</c> for the review (roadmap 0016.7). Live data is written by the merge service, never here.</summary>
public sealed class AgentReviewRepository : IAgentReviewRepository
{
    private readonly IDbConnectionFactory _factory;

    public AgentReviewRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<AgentProposalRecord>> ListAsync(int runId, string? status, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        var rows = await connection.QueryAsync<AgentProposalRecord>(new CommandDefinition(AgentReviewSql.List, new { RunId = runId, Status = status }, cancellationToken: token));

        return rows.ToList();
    }

    public async Task<AgentProposalRecord?> GetAsync(long id, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);

        return await connection.QuerySingleOrDefaultAsync<AgentProposalRecord>(new CommandDefinition(AgentReviewSql.Get, new { Id = id }, cancellationToken: token));
    }

    public async Task<bool> TryCloseAsync(long id, string status, int reviewerUserId, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);

        return await connection.ExecuteAsync(new CommandDefinition(AgentReviewSql.Close, new { Id = id, Status = status, ReviewerUserId = reviewerUserId }, cancellationToken: token)) == 1;
    }
}

public static class AgentReviewSql
{
    private const string Select = """
                                  SELECT p."Id"
                                       , p."RunId"
                                       , p."PackageId"
                                       , p."BibleBookId"
                                       , p."Chapter"
                                       , p."Verse"
                                       , p."Language"
                                       , p."Links"::text "LinksJson"
                                       , p."Lemmas"::text "LemmasJson"
                                       , p."Parts"::text "PartsJson"
                                       , p."Roles"::text "RolesJson"
                                       , p."Flags"::text "FlagsJson"
                                       , p."Note"
                                       , p."Status"
                                       , p."CreatedAt"
                                       , p."ReviewedByUserId"
                                       , p."ReviewedAt"
                                       , r."AiUserId"
                                       , r."Profile" "RunProfile"
                                       , r."Model" "RunModel"
                                  FROM "unshackled-word"."AgentVerseProposals" p
                                      JOIN "unshackled-word"."AgentRuns" r ON r."Id" = p."RunId"
                                  """;

    public const string List = Select + """

                                        WHERE 1=1
                                            AND p."RunId" = @RunId
                                            AND (@Status::varchar IS NULL OR p."Status" = @Status)
                                        ORDER BY p."Chapter", p."Verse", p."Id"
                                        """;

    public const string Get = Select + """

                                       WHERE 1=1
                                           AND p."Id" = @Id
                                       """;

    /// <summary>Only an open proposal can be closed; the row count tells the caller whether it won.</summary>
    public const string Close = """
                                UPDATE "unshackled-word"."AgentVerseProposals"
                                SET "Status" = @Status
                                  , "ReviewedByUserId" = @ReviewerUserId
                                  , "ReviewedAt" = now()
                                WHERE 1=1
                                    AND "Id" = @Id
                                    AND "Status" = 'open'
                                """;
}
