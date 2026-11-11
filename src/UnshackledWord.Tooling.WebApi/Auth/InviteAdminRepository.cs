using Dapper;
using Npgsql;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Auth;

/// <summary>Admin-side management of invite codes (list, create, revoke, invite by email).</summary>
public interface IInviteAdminRepository
{
    Task<IReadOnlyList<InviteCodeSummary>> ListAsync(CancellationToken ct);

    Task<(int Id, string Code)> CreateAsync(int maxSeats, DateTimeOffset expiresAt, int createdBy, string? note, string? boundEmailNormalized, CancellationToken ct);

    /// <summary>Reissues: revokes live codes bound to the address, then inserts a new single-seat one.</summary>
    Task<(int Id, string Code)> CreateInvitationAsync(string emailNormalized, DateTimeOffset expiresAt, int createdBy, string? note, CancellationToken ct);

    /// <summary>False means the id does not exist. Idempotent.</summary>
    Task<bool> RevokeAsync(int id, CancellationToken ct);
}

public sealed class InviteAdminRepository : IInviteAdminRepository
{
    // 16 random bytes: a collision is astronomically unlikely, but it should be a retry, never a 500.
    private const int MaxGenerationAttempts = 5;

    private readonly IDbConnectionFactory _connectionFactory;

    public InviteAdminRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
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

    public async Task<(int Id, string Code)> CreateAsync(
        int maxSeats, DateTimeOffset expiresAt, int createdBy, string? note, string? boundEmailNormalized, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        return await InsertWithRetryAsync(connection, null, maxSeats, expiresAt, createdBy, note, boundEmailNormalized, ct);
    }

    // "Live" = not revoked and not expired. Redeemed/expired ones are left alone to keep the audit trail intact.
    private const string RevokeLiveInvitations = """
        UPDATE "unshackled-word"."InviteCodes"
        SET "RevokedAt" = now()
        WHERE "BoundEmailNormalized" = @emailNormalized
          AND "RevokedAt" IS NULL
          AND "ExpiresAt" > now()
        """;

    public async Task<(int Id, string Code)> CreateInvitationAsync(
        string emailNormalized, DateTimeOffset expiresAt, int createdBy, string? note, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        using var transaction = connection.BeginTransaction();

        await connection.ExecuteAsync(new CommandDefinition(RevokeLiveInvitations, new { emailNormalized }, transaction, cancellationToken: ct));
        var result = await InsertWithRetryAsync(connection, transaction, 1, expiresAt, createdBy, note, emailNormalized, ct);

        transaction.Commit();
        return result;
    }

    private static async Task<(int Id, string Code)> InsertWithRetryAsync(
        System.Data.IDbConnection connection,
        System.Data.IDbTransaction? transaction,
        int maxSeats,
        DateTimeOffset expiresAt,
        int createdBy,
        string? note,
        string? boundEmailNormalized,
        CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxGenerationAttempts; attempt++)
        {
            var code = InviteCodeGenerator.Generate();

            // A unique violation inside a transaction aborts it, so retry only outside one.
            try
            {
                var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    InsertInviteCode,
                    new { code, boundEmailNormalized, maxSeats, expiresAt, createdBy, note },
                    transaction,
                    cancellationToken: ct));
                return (id, code);
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation
                && transaction is null
                && attempt < MaxGenerationAttempts)
            {
            }
        }

        throw new InvalidOperationException($"Could not generate a unique invite code after {MaxGenerationAttempts} attempts.");
    }

    // Sets RevokedAt instead of deleting: redemptions are the audit trail for existing accounts.
    private const string RevokeInviteCode = """
        UPDATE "unshackled-word"."InviteCodes"
        SET "RevokedAt" = COALESCE("RevokedAt", now())
        WHERE "Id" = @id
        """;

    public async Task<bool> RevokeAsync(int id, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        var rows = await connection.ExecuteAsync(new CommandDefinition(RevokeInviteCode, new { id }, cancellationToken: ct));
        return rows > 0;
    }
}
