using Dapper;
using Npgsql;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.UserAdmin;
using UnshackledWord.Domain.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Auth;

/// <summary>Admin-side management of invite codes (list, create, revoke, invite by email).</summary>
public interface IInviteAdminRepository
{
    Task<IReadOnlyList<InviteCodeSummary>> ListAsync(CancellationToken ct);

    /// <summary>Inserts the code and its <c>invite_created</c> log row in one transaction.</summary>
    Task<(int Id, string Code)> CreateAsync(int maxSeats, DateTimeOffset expiresAt, int createdBy, string? note, string? boundEmailNormalized, CancellationToken ct);

    /// <summary>Reissues: revokes live codes bound to the address, then inserts a new single-seat one; logs <c>invite_revoked</c> per revoked code and <c>invite_created</c>.</summary>
    Task<(int Id, string Code)> CreateInvitationAsync(string emailNormalized, DateTimeOffset expiresAt, int createdBy, string? note, CancellationToken ct);

    /// <summary>False means the id does not exist. Idempotent; only a code that was still live gets an <c>invite_revoked</c> log row.</summary>
    Task<bool> RevokeAsync(int id, int actorId, CancellationToken ct);
}

public sealed class InviteAdminRepository : IInviteAdminRepository
{
    // 16 random bytes: a collision is astronomically unlikely, but it should be a retry, never a 500.
    private const int MaxGenerationAttempts = 5;

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IAdminLogRepository _adminLog;

    public InviteAdminRepository(IDbConnectionFactory connectionFactory, IAdminLogRepository adminLog)
    {
        _connectionFactory = connectionFactory;
        _adminLog = adminLog;
    }

    private const string SelectInviteCodes = """
        SELECT ic."Id",
               ic."Code",
               ic."BoundEmailNormalized"                                                                        AS "BoundEmail",
               ic."MaxSeats",
               count(DISTINCT r."UserId")                                                                       AS "SeatsUsed",
               ic."ExpiresAt",
               ic."RevokedAt",
               ic."Note",
               ic."CreatedAt",
               creator."DisplayName"                                                                            AS "CreatedByDisplayName",
               COALESCE(array_agg(DISTINCT redeemer."DisplayName") FILTER (WHERE redeemer."DisplayName" IS NOT NULL), '{}') AS "RedeemedByDisplayNames"
        FROM "unshackled-word"."InviteCodes" ic
                 JOIN "unshackled-word"."Users" creator ON creator."Id" = ic."CreatedBy"
                 LEFT JOIN "unshackled-word"."InviteCodeRedemptions" r ON r."InviteCodeId" = ic."Id"
                 LEFT JOIN "unshackled-word"."Users" redeemer ON redeemer."Id" = r."UserId"
        GROUP BY ic."Id", creator."DisplayName"
        ORDER BY ic."CreatedAt" DESC
        """;

    public async Task<IReadOnlyList<InviteCodeSummary>> ListAsync(CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        var rows = await connection.QueryAsync<InviteCodeSummary>(new CommandDefinition(SelectInviteCodes, cancellationToken: ct));
        return rows.ToList();
    }

    private const string InsertInviteCode = """
        INSERT INTO "unshackled-word"."InviteCodes" ("Code", "BoundEmailNormalized", "MaxSeats", "ExpiresAt", "CreatedBy", "Note")
        VALUES (@code, @boundEmailNormalized, @maxSeats, @expiresAt, @createdBy, @note)
        RETURNING "Id"
        """;

    public Task<(int Id, string Code)> CreateAsync(
        int maxSeats, DateTimeOffset expiresAt, int createdBy, string? note, string? boundEmailNormalized, CancellationToken ct) =>
        InsertLoggedAsync(false, boundEmailNormalized, maxSeats, expiresAt, createdBy, note, ct);

    // "Live" = not revoked and not expired. Redeemed/expired ones are left alone to keep the audit trail intact.
    private const string RevokeLiveInvitations = """
        UPDATE "unshackled-word"."InviteCodes"
        SET "RevokedAt" = now()
        WHERE "BoundEmailNormalized" = @emailNormalized
          AND "RevokedAt" IS NULL
          AND "ExpiresAt" > now()
        RETURNING "Id"
        """;

    public Task<(int Id, string Code)> CreateInvitationAsync(
        string emailNormalized, DateTimeOffset expiresAt, int createdBy, string? note, CancellationToken ct) =>
        InsertLoggedAsync(true, emailNormalized, 1, expiresAt, createdBy, note, ct);

    // One transaction per attempt: a unique violation aborts it, so a generated-code collision restarts the whole unit (revoke, insert, log rows).
    private async Task<(int Id, string Code)> InsertLoggedAsync(
        bool reissue,
        string? boundEmailNormalized,
        int maxSeats,
        DateTimeOffset expiresAt,
        int createdBy,
        string? note,
        CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxGenerationAttempts; attempt++)
        {
            var code = InviteCodeGenerator.Generate();

            using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
            using var transaction = connection.BeginTransaction();

            try
            {
                var revoked = reissue
                    ? (await connection.QueryAsync<int>(new CommandDefinition(
                        RevokeLiveInvitations, new { emailNormalized = boundEmailNormalized }, transaction, cancellationToken: ct))).ToList()
                    : [];

                var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    InsertInviteCode,
                    new { code, boundEmailNormalized, maxSeats, expiresAt, createdBy, note },
                    transaction,
                    cancellationToken: ct));

                foreach (var revokedId in revoked)
                {
                    await _adminLog.AppendAsync(
                        new AdminLogEntry(createdBy, AdminLogActions.InviteRevoked, null, revokedId, System.Text.Json.JsonSerializer.Serialize(new { email = boundEmailNormalized })),
                        transaction,
                        ct);
                }

                await _adminLog.AppendAsync(
                    new AdminLogEntry(
                        createdBy,
                        AdminLogActions.InviteCreated,
                        null,
                        id,
                        System.Text.Json.JsonSerializer.Serialize(new { maxSeats, expiresAt, boundEmail = boundEmailNormalized })),
                    transaction,
                    ct);

                transaction.Commit();
                return (id, code);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation && attempt < MaxGenerationAttempts)
            {
                transaction.Rollback();
            }
        }

        throw new InvalidOperationException($"Could not generate a unique invite code after {MaxGenerationAttempts} attempts.");
    }

    // Sets RevokedAt instead of deleting: redemptions are the audit trail for existing accounts.
    private const string LockInviteCodeForRevoke = """
        SELECT "RevokedAt" IS NULL
        FROM "unshackled-word"."InviteCodes"
        WHERE "Id" = @id
        FOR UPDATE
        """;

    private const string RevokeInviteCode = """
        UPDATE "unshackled-word"."InviteCodes"
        SET "RevokedAt" = now()
        WHERE "Id" = @id
        """;

    public async Task<bool> RevokeAsync(int id, int actorId, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        using var transaction = connection.BeginTransaction();

        var wasLive = await connection.ExecuteScalarAsync<bool?>(new CommandDefinition(LockInviteCodeForRevoke, new { id }, transaction, cancellationToken: ct));

        if (wasLive is null)
        {
            transaction.Rollback();
            return false;
        }

        if (wasLive.Value)
        {
            await connection.ExecuteAsync(new CommandDefinition(RevokeInviteCode, new { id }, transaction, cancellationToken: ct));
            await _adminLog.AppendAsync(new AdminLogEntry(actorId, AdminLogActions.InviteRevoked, null, id, "{}"), transaction, ct);
        }

        transaction.Commit();
        return true;
    }
}
