using System.Data;
using Dapper;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.UserAdmin;
using UnshackledWord.Domain.WebApi.UserAdmin;

namespace UnshackledWord.Infrastructure.Repositories;

/// <summary>User administration queries and writes (roadmap 0020); every write is one transaction with its AdminLog rows.</summary>
public sealed class UserAdminRepository : IUserAdminRepository
{
    private readonly IDbConnectionFactory _factory;
    private readonly IAdminLogRepository _adminLog;

    public UserAdminRepository(IDbConnectionFactory factory, IAdminLogRepository adminLog)
    {
        _factory = factory;
        _adminLog = adminLog;
    }

    public async Task<UserListResponse> ListAsync(UserListQuery query, CancellationToken token = default)
    {
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var page = Math.Max(query.Page, 1);
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();

        using var connection = await _factory.CreateDbConnectionAsync(token);

        var rows = (await connection.QueryAsync<ListRow>(new CommandDefinition(UserAdminSql.List, new
        {
            pattern = search is null ? null : "%" + EscapeLike(search) + "%",
            role = string.IsNullOrWhiteSpace(query.Role) ? null : query.Role,
            status = string.IsNullOrWhiteSpace(query.Status) ? null : query.Status,
            pageSize,
            offset = (page - 1) * pageSize,
        }, cancellationToken: token))).ToList();

        var total = rows.Count > 0 ? (int)rows[0].Total : await CountBeyondLastPageAsync(connection, search, query, token);

        return new UserListResponse(
            rows.Select(r => new UserListItem(r.Id, r.Email, r.DisplayName, r.Roles ?? [], r.Status, r.CreatedAt, r.LastSignInAt)).ToArray(),
            total,
            page,
            pageSize);
    }

    // A page past the end has no rows to carry the window count, so ask for the total separately.
    private static async Task<int> CountBeyondLastPageAsync(IDbConnection connection, string? search, UserListQuery query, CancellationToken token)
    {
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(UserAdminSql.Count, new
        {
            pattern = search is null ? null : "%" + EscapeLike(search) + "%",
            role = string.IsNullOrWhiteSpace(query.Role) ? null : query.Role,
            status = string.IsNullOrWhiteSpace(query.Status) ? null : query.Status,
        }, cancellationToken: token));
    }

    public async Task<UserDetail?> GetDetailAsync(int id, int actorId, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        return await ReadDetailAsync(connection, null, id, actorId, token);
    }

    public async Task<CreateUserOutcome> CreateAsync(int actorId, string email, string displayName, IReadOnlyCollection<string> roles, CancellationToken token = default)
    {
        var trimmedEmail = email.Trim();
        var normalized = EmailNormalizer.Normalize(trimmedEmail);
        var name = displayName.Trim();
        var assigned = roles.Where(r => UserAdminGuards.AssignableRoles.Contains(r)).Distinct().Order().ToArray();

        using var connection = await _factory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();

        var id = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            UserAdminSql.InsertUser, new { email = trimmedEmail, normalized, name }, transaction, cancellationToken: token));

        if (id is null)
        {
            transaction.Rollback();
            var existing = await connection.ExecuteScalarAsync<int>(new CommandDefinition(UserAdminSql.IdByEmail, new { normalized }, cancellationToken: token));
            return new CreateUserOutcome(existing, null);
        }

        await connection.ExecuteAsync(new CommandDefinition(UserAdminSql.EnsureBaseRole, new { targetId = id }, transaction, cancellationToken: token));
        await connection.ExecuteAsync(new CommandDefinition(UserAdminSql.GrantRoles, new { targetId = id, toGrant = assigned, actorId }, transaction, cancellationToken: token));

        var details = System.Text.Json.JsonSerializer.Serialize(new { email = trimmedEmail, displayName = name, roles = assigned });
        await _adminLog.AppendAsync(new AdminLogEntry(actorId, AdminLogActions.UserCreated, id, null, details), transaction, token);

        transaction.Commit();
        return new CreateUserOutcome(null, await GetDetailAsync(id.Value, actorId, token));
    }

    public async Task<UpdateUserOutcome> UpdateAsync(int actorId, int targetId, string displayName, string email, CancellationToken token = default)
    {
        var newEmail = email.Trim();
        var newNormalized = EmailNormalizer.Normalize(newEmail);
        var newName = displayName.Trim();

        using var connection = await _factory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();

        var current = await connection.QuerySingleOrDefaultAsync<EditRow>(
            new CommandDefinition(UserAdminSql.LockForEdit, new { targetId }, transaction, cancellationToken: token));

        if (current is null)
        {
            transaction.Rollback();
            return new UpdateUserOutcome(true, null, null, null, null);
        }

        if (current.IsAi)
        {
            transaction.Rollback();
            return new UpdateUserOutcome(false, UserAdminErrors.AiUser, null, null, null);
        }

        var nameChanged = !string.Equals(current.DisplayName, newName, StringComparison.Ordinal);
        var emailChanged = !string.Equals(current.EmailNormalized, newNormalized, StringComparison.Ordinal);

        if (!nameChanged && !emailChanged)
        {
            transaction.Rollback();
            return new UpdateUserOutcome(false, null, null, await GetDetailAsync(targetId, actorId, token), null);
        }

        if (emailChanged)
        {
            var other = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
                UserAdminSql.IdByEmail, new { normalized = newNormalized }, transaction, cancellationToken: token));

            if (other is not null)
            {
                transaction.Rollback();
                return new UpdateUserOutcome(false, UserAdminErrors.EmailTaken, other, null, null);
            }
        }

        await connection.ExecuteAsync(new CommandDefinition(
            UserAdminSql.UpdateDetails, new { targetId, newName, newEmail, newNormalized, nameChanged, emailChanged }, transaction, cancellationToken: token));

        if (emailChanged)
        {
            // Links mailed to the old address must not work any more.
            await connection.ExecuteAsync(new CommandDefinition(UserAdminSql.ConsumeOpenTokens, new { targetId }, transaction, cancellationToken: token));
        }

        var changes = new Dictionary<string, object>();

        if (nameChanged)
        {
            changes["displayName"] = new { old = current.DisplayName, @new = newName };
        }

        if (emailChanged)
        {
            changes["email"] = new { old = current.Email, @new = newEmail };
        }

        await _adminLog.AppendAsync(
            new AdminLogEntry(actorId, AdminLogActions.UserUpdated, targetId, null, System.Text.Json.JsonSerializer.Serialize(changes)), transaction, token);

        transaction.Commit();
        return new UpdateUserOutcome(false, null, null, await GetDetailAsync(targetId, actorId, token), emailChanged ? current.Email : null);
    }

    public async Task<ResetTarget?> FindResetTargetAsync(int id, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        return await connection.QuerySingleOrDefaultAsync<ResetTarget>(
            new CommandDefinition(UserAdminSql.ResetTargetById, new { id }, cancellationToken: token));
    }

    public async Task<SetRolesOutcome> SetRolesAsync(int actorId, int targetId, IReadOnlyCollection<string> roles, CancellationToken token = default)
    {
        var requested = roles.Where(r => UserAdminGuards.AssignableRoles.Contains(r)).ToHashSet();

        using var connection = await _factory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition(UserAdminSql.LockRoleRows, new { targetId }, transaction, cancellationToken: token));
        var snapshot = await connection.QuerySingleOrDefaultAsync<SnapshotRow>(
            new CommandDefinition(UserAdminSql.LockAndSnapshot, new { targetId }, transaction, cancellationToken: token));

        if (snapshot is null)
        {
            transaction.Rollback();
            return new SetRolesOutcome(true, null, null);
        }

        var current = (await connection.QueryAsync<string>(
            new CommandDefinition(UserAdminSql.RolesOf, new { targetId }, transaction, cancellationToken: token)))
            .Where(r => UserAdminGuards.AssignableRoles.Contains(r))
            .ToHashSet();

        var toGrant = requested.Except(current).Order().ToArray();
        var toRevoke = current.Except(requested).Order().ToArray();

        if (toGrant.Length == 0 && toRevoke.Length == 0)
        {
            transaction.Rollback();
            return new SetRolesOutcome(false, null, await GetDetailAsync(targetId, actorId, token));
        }

        var guard = UserAdminGuards.CheckSetRoles(
            new GuardSnapshot(actorId, targetId, snapshot.IsAi, snapshot.IsDeactivated, snapshot.IsAdmin, snapshot.ActiveAdminCount),
            requested);

        if (guard is not null)
        {
            transaction.Rollback();
            return new SetRolesOutcome(false, guard, null);
        }

        await connection.ExecuteAsync(new CommandDefinition(UserAdminSql.RevokeRoles, new { targetId, toRevoke }, transaction, cancellationToken: token));
        await connection.ExecuteAsync(new CommandDefinition(UserAdminSql.GrantRoles, new { targetId, toGrant, actorId }, transaction, cancellationToken: token));
        await connection.ExecuteAsync(new CommandDefinition(UserAdminSql.EnsureBaseRole, new { targetId }, transaction, cancellationToken: token));
        await connection.ExecuteAsync(new CommandDefinition(UserAdminSql.RotateStamp, new { targetId }, transaction, cancellationToken: token));

        foreach (var role in toGrant)
        {
            await _adminLog.AppendAsync(new AdminLogEntry(actorId, AdminLogActions.RoleGranted, targetId, null, RoleDetails(role)), transaction, token);
        }

        foreach (var role in toRevoke)
        {
            await _adminLog.AppendAsync(new AdminLogEntry(actorId, AdminLogActions.RoleRevoked, targetId, null, RoleDetails(role)), transaction, token);
        }

        transaction.Commit();
        return new SetRolesOutcome(false, null, await GetDetailAsync(targetId, actorId, token));
    }

    public async Task<DeactivationOutcome> DeactivateAsync(int actorId, int targetId, string? reason, CancellationToken token = default)
    {
        var cleanReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

        using var connection = await _factory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();

        var snapshot = await LockSnapshotAsync(connection, transaction, targetId, token);

        if (snapshot is null)
        {
            transaction.Rollback();
            return new DeactivationOutcome(true, null, null);
        }

        var guard = UserAdminGuards.CheckDeactivate(
            new GuardSnapshot(actorId, targetId, snapshot.IsAi, snapshot.IsDeactivated, snapshot.IsAdmin, snapshot.ActiveAdminCount));

        if (guard is not null)
        {
            transaction.Rollback();
            return new DeactivationOutcome(false, guard, null);
        }

        await connection.ExecuteAsync(new CommandDefinition(UserAdminSql.Deactivate, new { targetId, actorId, reason = cleanReason }, transaction, cancellationToken: token));
        // Open emailed links (reset, magic link, verification, welcome) die with the account.
        await connection.ExecuteAsync(new CommandDefinition(UserAdminSql.ConsumeOpenTokens, new { targetId }, transaction, cancellationToken: token));

        var details = cleanReason is null
            ? "{}"
            : System.Text.Json.JsonSerializer.Serialize(new { reason = cleanReason });
        await _adminLog.AppendAsync(new AdminLogEntry(actorId, AdminLogActions.UserDeactivated, targetId, null, details), transaction, token);

        transaction.Commit();
        return new DeactivationOutcome(false, null, await GetDetailAsync(targetId, actorId, token));
    }

    public async Task<DeactivationOutcome> ReactivateAsync(int actorId, int targetId, CancellationToken token = default)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();

        var snapshot = await LockSnapshotAsync(connection, transaction, targetId, token);

        if (snapshot is null)
        {
            transaction.Rollback();
            return new DeactivationOutcome(true, null, null);
        }

        if (!snapshot.IsDeactivated)
        {
            transaction.Rollback();
            return new DeactivationOutcome(false, UserAdminErrors.NotDeactivated, null);
        }

        var previous = await connection.QuerySingleAsync<PreviousDeactivationRow>(
            new CommandDefinition(UserAdminSql.ReadDeactivation, new { targetId }, transaction, cancellationToken: token));

        // No stamp rotation: a deactivated account has no sessions. Password and passkeys stay.
        await connection.ExecuteAsync(new CommandDefinition(UserAdminSql.Reactivate, new { targetId }, transaction, cancellationToken: token));

        var details = System.Text.Json.JsonSerializer.Serialize(new { deactivatedAt = previous.DeactivatedAt, reason = previous.DeactivationReason });
        await _adminLog.AppendAsync(new AdminLogEntry(actorId, AdminLogActions.UserReactivated, targetId, null, details), transaction, token);

        transaction.Commit();
        return new DeactivationOutcome(false, null, await GetDetailAsync(targetId, actorId, token));
    }

    // Locks admin role rows first (stable order), then reads the guard snapshot under row lock; null when the user does not exist.
    private static async Task<SnapshotRow?> LockSnapshotAsync(IDbConnection connection, IDbTransaction transaction, int targetId, CancellationToken token)
    {
        await connection.ExecuteAsync(new CommandDefinition(UserAdminSql.LockRoleRows, new { targetId }, transaction, cancellationToken: token));
        return await connection.QuerySingleOrDefaultAsync<SnapshotRow>(
            new CommandDefinition(UserAdminSql.LockAndSnapshot, new { targetId }, transaction, cancellationToken: token));
    }

    private static string RoleDetails(string role) => System.Text.Json.JsonSerializer.Serialize(new { role });

    private static async Task<UserDetail?> ReadDetailAsync(IDbConnection connection, IDbTransaction? transaction, int id, int actorId, CancellationToken token)
    {
        var row = await connection.QuerySingleOrDefaultAsync<DetailRow>(
            new CommandDefinition(UserAdminSql.Detail, new { id }, transaction, cancellationToken: token));

        if (row is null)
        {
            return null;
        }

        var roles = row.Roles ?? [];
        var deactivation = row.DeactivatedAt is { } at
            ? new DeactivationInfo(at, row.DeactivatedByUserId ?? 0, row.DeactivatedByDisplayName ?? string.Empty, row.DeactivationReason)
            : null;

        return new UserDetail(
            row.Id, row.Email, row.DisplayName, roles, row.Status, row.CreatedAt, row.LastSignInAt,
            row.EmailVerified, row.HasPassword, row.PasskeyCount, deactivation,
            row.Id == actorId,
            row.IsActiveAdmin && row.ActiveAdminCount <= 1);
    }

    /// <summary>Escapes LIKE wildcards (backslash is Postgres' default escape character).</summary>
    internal static string EscapeLike(string value) => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private sealed class ListRow
    {
        public int Id { get; init; }
        public string Email { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string[]? Roles { get; init; }
        public string Status { get; init; } = string.Empty;
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset? LastSignInAt { get; init; }
        public long Total { get; init; }
    }

    private sealed class DetailRow
    {
        public int Id { get; init; }
        public string Email { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string[]? Roles { get; init; }
        public string Status { get; init; } = string.Empty;
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset? LastSignInAt { get; init; }
        public bool EmailVerified { get; init; }
        public bool HasPassword { get; init; }
        public int PasskeyCount { get; init; }
        public DateTimeOffset? DeactivatedAt { get; init; }
        public int? DeactivatedByUserId { get; init; }
        public string? DeactivatedByDisplayName { get; init; }
        public string? DeactivationReason { get; init; }
        public bool IsActiveAdmin { get; init; }
        public int ActiveAdminCount { get; init; }
    }

    private sealed class EditRow
    {
        public string Email { get; init; } = string.Empty;
        public string EmailNormalized { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public bool IsAi { get; init; }
    }

    private sealed class PreviousDeactivationRow
    {
        public DateTimeOffset? DeactivatedAt { get; init; }
        public string? DeactivationReason { get; init; }
    }

    private sealed class SnapshotRow
    {
        public bool IsDeactivated { get; init; }
        public bool IsAi { get; init; }
        public bool IsAdmin { get; init; }
        public int ActiveAdminCount { get; init; }
    }
}

internal static class UserAdminSql
{
    private const string StatusExpression = """
        CASE
            WHEN bool_or(r."Role" = 'ai') THEN 'ai'
            WHEN u."DeactivatedAt" IS NOT NULL THEN 'deactivated'
            ELSE 'active'
        END
        """;

    internal const string List = $"""
        SELECT u."Id",
               u."Email",
               u."DisplayName",
               u."CreatedAt",
               u."LastSignInAt",
               array_agg(r."Role" ORDER BY r."Role") FILTER (WHERE r."Role" IS NOT NULL) AS "Roles",
               {StatusExpression}                                                        AS "Status",
               count(*) OVER ()                                                          AS "Total"
        FROM "unshackled-word"."Users" u
        LEFT JOIN "unshackled-word"."UserRoles" r ON r."UserId" = u."Id"
        WHERE (@pattern IS NULL OR u."DisplayName" ILIKE @pattern OR u."Email" ILIKE @pattern)
        GROUP BY u."Id"
        HAVING (@role IS NULL OR bool_or(r."Role" = @role))
           AND (@status IS NULL OR {StatusExpression} = @status)
        ORDER BY lower(u."DisplayName"), u."Id"
        LIMIT @pageSize OFFSET @offset
        """;

    internal const string Count = $"""
        SELECT count(*)
        FROM (SELECT u."Id"
              FROM "unshackled-word"."Users" u
              LEFT JOIN "unshackled-word"."UserRoles" r ON r."UserId" = u."Id"
              WHERE (@pattern IS NULL OR u."DisplayName" ILIKE @pattern OR u."Email" ILIKE @pattern)
              GROUP BY u."Id"
              HAVING (@role IS NULL OR bool_or(r."Role" = @role))
                 AND (@status IS NULL OR {StatusExpression} = @status)) x
        """;

    internal const string Detail = """
        SELECT u."Id",
               u."Email",
               u."DisplayName",
               u."CreatedAt",
               u."LastSignInAt",
               u."EmailVerifiedAt" IS NOT NULL                                                                   AS "EmailVerified",
               u."PasswordHash" IS NOT NULL                                                                      AS "HasPassword",
               (SELECT count(*) FROM "unshackled-word"."PasskeyCredentials" p WHERE p."UserId" = u."Id")::int4   AS "PasskeyCount",
               (SELECT array_agg(r."Role" ORDER BY r."Role")
                FROM "unshackled-word"."UserRoles" r
                WHERE r."UserId" = u."Id")                                                                       AS "Roles",
               CASE
                   WHEN EXISTS (SELECT 1 FROM "unshackled-word"."UserRoles" r WHERE r."UserId" = u."Id" AND r."Role" = 'ai') THEN 'ai'
                   WHEN u."DeactivatedAt" IS NOT NULL THEN 'deactivated'
                   ELSE 'active'
               END                                                                                               AS "Status",
               u."DeactivatedAt",
               u."DeactivatedByUserId",
               d."DisplayName"                                                                                   AS "DeactivatedByDisplayName",
               u."DeactivationReason",
               (u."DeactivatedAt" IS NULL
                AND EXISTS (SELECT 1 FROM "unshackled-word"."UserRoles" r WHERE r."UserId" = u."Id" AND r."Role" = 'admin')) AS "IsActiveAdmin",
               (SELECT count(*)
                FROM "unshackled-word"."UserRoles" r
                JOIN "unshackled-word"."Users" a ON a."Id" = r."UserId"
                WHERE r."Role" = 'admin'
                  AND a."DeactivatedAt" IS NULL)::int4                                                           AS "ActiveAdminCount"
        FROM "unshackled-word"."Users" u
        LEFT JOIN "unshackled-word"."Users" d ON d."Id" = u."DeactivatedByUserId"
        WHERE u."Id" = @id
        """;

    // Locks every admin role row plus the target's rows in a stable order, so two concurrent demotions queue instead of deadlocking.
    internal const string LockRoleRows = """
        SELECT ur."UserId"
        FROM "unshackled-word"."UserRoles" ur
        WHERE ur."Role" = 'admin'
           OR ur."UserId" = @targetId
        ORDER BY ur."UserId", ur."Role"
        FOR UPDATE
        """;

    internal const string LockAndSnapshot = """
        SELECT u."DeactivatedAt" IS NOT NULL                                                                              AS "IsDeactivated",
               EXISTS (SELECT 1 FROM "unshackled-word"."UserRoles" r WHERE r."UserId" = u."Id" AND r."Role" = 'ai')     AS "IsAi",
               EXISTS (SELECT 1 FROM "unshackled-word"."UserRoles" r WHERE r."UserId" = u."Id" AND r."Role" = 'admin')  AS "IsAdmin",
               (SELECT count(*)
                FROM "unshackled-word"."UserRoles" r
                JOIN "unshackled-word"."Users" a ON a."Id" = r."UserId"
                WHERE r."Role" = 'admin'
                  AND a."DeactivatedAt" IS NULL)::int4                                                                    AS "ActiveAdminCount"
        FROM "unshackled-word"."Users" u
        WHERE u."Id" = @targetId
        FOR UPDATE OF u
        """;

    internal const string RolesOf = """
        SELECT r."Role"
        FROM "unshackled-word"."UserRoles" r
        WHERE r."UserId" = @targetId
        """;

    internal const string RevokeRoles = """
        DELETE FROM "unshackled-word"."UserRoles"
        WHERE "UserId" = @targetId
          AND "Role" = ANY(@toRevoke)
        """;

    internal const string GrantRoles = """
        INSERT INTO "unshackled-word"."UserRoles" ("UserId", "Role", "GrantedByUserId")
        SELECT @targetId, unnest(@toGrant), @actorId
        ON CONFLICT DO NOTHING
        """;

    internal const string EnsureBaseRole = """
        INSERT INTO "unshackled-word"."UserRoles" ("UserId", "Role")
        VALUES (@targetId, 'user')
        ON CONFLICT DO NOTHING
        """;

    internal const string InsertUser = """
        INSERT INTO "unshackled-word"."Users" ("Email", "EmailNormalized", "PasswordHash", "DisplayName", "EmailVerifiedAt")
        VALUES (@email, @normalized, NULL, @name, now())
        ON CONFLICT ("EmailNormalized") DO NOTHING
        RETURNING "Id"
        """;

    internal const string IdByEmail = """
        SELECT "Id"
        FROM "unshackled-word"."Users"
        WHERE "EmailNormalized" = @normalized
        """;

    internal const string LockForEdit = """
        SELECT u."Email",
               u."EmailNormalized",
               u."DisplayName",
               EXISTS (SELECT 1 FROM "unshackled-word"."UserRoles" r WHERE r."UserId" = u."Id" AND r."Role" = 'ai') AS "IsAi"
        FROM "unshackled-word"."Users" u
        WHERE u."Id" = @targetId
        FOR UPDATE OF u
        """;

    internal const string UpdateDetails = """
        UPDATE "unshackled-word"."Users"
        SET "DisplayName"     = CASE WHEN @nameChanged THEN @newName ELSE "DisplayName" END,
            "Email"           = CASE WHEN @emailChanged THEN @newEmail ELSE "Email" END,
            "EmailNormalized" = CASE WHEN @emailChanged THEN @newNormalized ELSE "EmailNormalized" END,
            "EmailVerifiedAt" = CASE WHEN @emailChanged THEN now() ELSE "EmailVerifiedAt" END,
            "SecurityStamp"   = CASE WHEN @emailChanged THEN gen_random_uuid()::varchar ELSE "SecurityStamp" END,
            "UpdatedAt"       = now()
        WHERE "Id" = @targetId
        """;

    internal const string ConsumeOpenTokens = """
        UPDATE "unshackled-word"."AuthTokens"
        SET "ConsumedAt" = now()
        WHERE "UserId" = @targetId
          AND "ConsumedAt" IS NULL
        """;

    internal const string ResetTargetById = """
        SELECT u."Id",
               u."Email",
               u."DisplayName",
               u."PasswordHash" IS NOT NULL                                                                           AS "HasPassword",
               EXISTS (SELECT 1 FROM "unshackled-word"."UserRoles" r WHERE r."UserId" = u."Id" AND r."Role" = 'ai')  AS "IsAi"
        FROM "unshackled-word"."Users" u
        WHERE u."Id" = @id
        """;

    internal const string Deactivate = """
        UPDATE "unshackled-word"."Users"
        SET "DeactivatedAt"       = now(),
            "DeactivatedByUserId" = @actorId,
            "DeactivationReason"  = NULLIF(btrim(@reason), ''),
            "SecurityStamp"       = gen_random_uuid()::varchar,
            "UpdatedAt"           = now()
        WHERE "Id" = @targetId
          AND "DeactivatedAt" IS NULL
        """;
    internal const string ReadDeactivation = """
        SELECT "DeactivatedAt", "DeactivationReason"
        FROM "unshackled-word"."Users"
        WHERE "Id" = @targetId
        """;
    internal const string Reactivate = """
        UPDATE "unshackled-word"."Users"
        SET "DeactivatedAt"       = NULL,
            "DeactivatedByUserId" = NULL,
            "DeactivationReason"  = NULL,
            "UpdatedAt"           = now()
        WHERE "Id" = @targetId
        """;
    internal const string RotateStamp = """
        UPDATE "unshackled-word"."Users"
        SET "SecurityStamp" = gen_random_uuid()::varchar,
            "UpdatedAt"     = now()
        WHERE "Id" = @targetId
        """;
}
