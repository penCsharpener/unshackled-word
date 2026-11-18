using System.Data;
using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.UserAdmin;

namespace UnshackledWord.Infrastructure.Repositories;

/// <summary>Table <c>AdminLog</c> (migration 0035); a database trigger refuses UPDATE and DELETE.</summary>
public sealed class AdminLogRepository : IAdminLogRepository
{
    private readonly IDbConnectionFactory _factory;

    public AdminLogRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task AppendAsync(AdminLogEntry entry, IDbTransaction? transaction = null, CancellationToken token = default)
    {
        var parameters = new
        {
            entry.ActorUserId,
            entry.TargetUserId,
            entry.InviteCodeId,
            entry.Action,
            entry.DetailsJson,
        };

        if (transaction?.Connection is { } existing)
        {
            await existing.ExecuteAsync(new CommandDefinition(AdminLogSql.Insert, parameters, transaction, cancellationToken: token));
            return;
        }

        using var connection = await _factory.CreateDbConnectionAsync(token);
        await connection.ExecuteAsync(new CommandDefinition(AdminLogSql.Insert, parameters, cancellationToken: token));
    }

    public async Task<IReadOnlyList<AdminLogRecord>> QueryAsync(AdminLogQuery query, CancellationToken token = default) =>
        (await QueryPageAsync(query, token)).Items;

    public async Task<AdminLogPage> QueryPageAsync(AdminLogQuery query, CancellationToken token = default)
    {
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var page = Math.Max(query.Page, 1);

        using var connection = await _factory.CreateDbConnectionAsync(token);

        var items = (await connection.QueryAsync<AdminLogRecord>(new CommandDefinition(AdminLogSql.Query, new
        {
            query.TargetUserId,
            query.Action,
            PageSize = pageSize,
            Offset = (page - 1) * pageSize,
        }, cancellationToken: token))).ToList();

        // A page past the end has no row to carry the window count, so ask for the total separately.
        var total = items.Count > 0
            ? (int)items[0].Total
            : await connection.ExecuteScalarAsync<int>(new CommandDefinition(AdminLogSql.Count, new { query.TargetUserId, query.Action }, cancellationToken: token));

        return new AdminLogPage(items, total, page, pageSize);
    }
}

internal static class AdminLogSql
{
    internal const string Insert = """
        INSERT INTO "unshackled-word"."AdminLog" ("ActorUserId", "TargetUserId", "InviteCodeId", "Action", "Details")
        VALUES (@ActorUserId, @TargetUserId, @InviteCodeId, @Action, CAST(@DetailsJson AS jsonb))
        """;

    internal const string Query = """
        SELECT l."Id",
               l."ActorUserId",
               l."TargetUserId",
               l."InviteCodeId",
               l."Action",
               l."Details"::text AS "DetailsJson",
               l."CreatedAt",
               actor."DisplayName"  AS "ActorDisplayName",
               target."DisplayName" AS "TargetDisplayName",
               count(*) OVER ()     AS "Total"
        FROM "unshackled-word"."AdminLog" l
        JOIN "unshackled-word"."Users" actor ON actor."Id" = l."ActorUserId"
        LEFT JOIN "unshackled-word"."Users" target ON target."Id" = l."TargetUserId"
        WHERE (@TargetUserId IS NULL OR l."TargetUserId" = @TargetUserId)
          AND (@Action IS NULL OR l."Action" = @Action)
        ORDER BY l."CreatedAt" DESC, l."Id" DESC
        LIMIT @PageSize OFFSET @Offset
        """;

    internal const string Count = """
        SELECT count(*)
        FROM "unshackled-word"."AdminLog" l
        WHERE (@TargetUserId IS NULL OR l."TargetUserId" = @TargetUserId)
          AND (@Action IS NULL OR l."Action" = @Action)
        """;
}
