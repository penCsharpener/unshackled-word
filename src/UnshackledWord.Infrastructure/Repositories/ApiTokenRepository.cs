using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.ApiTokens;

namespace UnshackledWord.Infrastructure.Repositories;

/// <summary>Table <c>ApiTokens</c> (migration 0029). Only hashes are stored and queried.</summary>
public sealed class ApiTokenRepository : IApiTokenRepository
{
    private readonly IDbConnectionFactory _factory;

    public ApiTokenRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<string>?> GetUserRolesAsync(int userId, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(ApiTokenSql.UserExists, new { UserId = userId }, cancellationToken: token));
        if (!exists)
        {
            return null;
        }

        return (await connection.QueryAsync<string>(new CommandDefinition(ApiTokenSql.UserRoles, new { UserId = userId }, cancellationToken: token))).ToList();
    }

    public async Task<ApiTokenRecord> InsertAsync(ApiTokenInsert insert, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);

        return await connection.QuerySingleAsync<ApiTokenRecord>(new CommandDefinition(ApiTokenSql.Insert, new
        {
            insert.UserId,
            insert.Name,
            insert.TokenHash,
            insert.TokenPrefix,
            insert.Scope,
            insert.CreatedByUserId,
            ExpiresAt = insert.ExpiresAt,
        }, cancellationToken: token));
    }

    public async Task<IReadOnlyList<ApiTokenRecord>> ListAsync(CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);

        return (await connection.QueryAsync<ApiTokenRecord>(new CommandDefinition(ApiTokenSql.List, cancellationToken: token))).ToList();
    }

    public async Task<IReadOnlyList<ApiTokenUserRecord>> ListEligibleUsersAsync(CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        var rows = await connection.QueryAsync<EligibleUserRow>(new CommandDefinition(ApiTokenSql.EligibleUsers, new { Roles = new[] { "ai", "maintainer", "admin" } }, cancellationToken: token));

        return rows
            .GroupBy(r => (r.Id, r.Name))
            .Select(g => new ApiTokenUserRecord { Id = g.Key.Id, Name = g.Key.Name, Roles = g.Select(r => r.Role).OrderBy(r => r, StringComparer.Ordinal).ToList() })
            .ToList();
    }

    private sealed class EligibleUserRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public async Task<bool> RevokeAsync(int id, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);

        return await connection.ExecuteAsync(new CommandDefinition(ApiTokenSql.Revoke, new { Id = id }, cancellationToken: token)) == 1;
    }

    public async Task<ApiTokenRecord?> FindByHashAsync(string tokenHash, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);

        return await connection.QuerySingleOrDefaultAsync<ApiTokenRecord>(new CommandDefinition(ApiTokenSql.FindByHash, new { TokenHash = tokenHash }, cancellationToken: token));
    }

    public async Task TouchAsync(int id, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        await connection.ExecuteAsync(new CommandDefinition(ApiTokenSql.Touch, new { Id = id }, cancellationToken: token));
    }

    public async Task<int> CountProposalsTodayAsync(int id, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(ApiTokenSql.CountProposalsToday, new { Id = id }, cancellationToken: token));
    }
}

public static class ApiTokenSql
{
    private const string Columns = """
                                   t."Id"
                                        , t."UserId"
                                        , t."Name"
                                        , t."TokenPrefix"
                                        , t."Scope"
                                        , t."CreatedByUserId"
                                        , t."CreatedAt"
                                        , t."ExpiresAt"
                                        , t."RevokedAt"
                                        , t."LastUsedAt"
                                   """;

    public const string UserExists = """
                                     SELECT EXISTS (SELECT 1 FROM "unshackled-word"."Users" u WHERE u."Id" = @UserId)
                                     """;

    public const string UserRoles = """
                                    SELECT ur."Role"
                                    FROM "unshackled-word"."UserRoles" ur
                                    WHERE 1=1
                                        AND ur."UserId" = @UserId
                                    """;

    public const string EligibleUsers = """
                                        SELECT u."Id"
                                             , u."DisplayName" "Name"
                                             , ur."Role"
                                        FROM "unshackled-word"."Users" u
                                            JOIN "unshackled-word"."UserRoles" ur ON ur."UserId" = u."Id"
                                        WHERE 1=1
                                            AND EXISTS (SELECT 1 FROM "unshackled-word"."UserRoles" x WHERE x."UserId" = u."Id" AND x."Role" = ANY (@Roles))
                                        ORDER BY u."Id"
                                        """;

    public const string Insert = $"""
                                  INSERT INTO "unshackled-word"."ApiTokens" AS t ("UserId", "Name", "TokenHash", "TokenPrefix", "Scope", "CreatedByUserId", "ExpiresAt")
                                  VALUES (@UserId, @Name, @TokenHash, @TokenPrefix, @Scope, @CreatedByUserId, @ExpiresAt)
                                  RETURNING {Columns}
                                  """;

    public const string List = $"""
                                SELECT {Columns}
                                FROM "unshackled-word"."ApiTokens" t
                                ORDER BY t."Id" DESC
                                """;

    public const string FindByHash = $"""
                                      SELECT {Columns}
                                      FROM "unshackled-word"."ApiTokens" t
                                      WHERE 1=1
                                          AND t."TokenHash" = @TokenHash
                                      """;

    public const string Revoke = """
                                 UPDATE "unshackled-word"."ApiTokens"
                                 SET "RevokedAt" = now()
                                 WHERE 1=1
                                     AND "Id" = @Id
                                     AND "RevokedAt" IS NULL
                                 """;

    public const string Touch = """
                                UPDATE "unshackled-word"."ApiTokens"
                                SET "LastUsedAt" = now()
                                WHERE 1=1
                                    AND "Id" = @Id
                                """;

    public const string CountProposalsToday = """
                                              SELECT count(*)::int
                                              FROM "unshackled-word"."AgentVerseProposals" p
                                                  JOIN "unshackled-word"."AgentRuns" r ON r."Id" = p."RunId"
                                              WHERE 1=1
                                                  AND r."ApiTokenId" = @Id
                                                  AND p."CreatedAt" >= date_trunc('day', now() AT TIME ZONE 'utc') AT TIME ZONE 'utc'
                                              """;
}
