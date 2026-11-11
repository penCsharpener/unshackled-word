using System.Data;
using System.Security.Cryptography;
using Dapper;
using Npgsql;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Auth;

namespace UnshackledWord.Tooling.WebApi.Auth;

/// <summary>Everything the user's own row, their roles, single-use tokens and passkeys need from Postgres.</summary>
public sealed class AuthUser
{
    public int Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? PasswordHash { get; init; }
    public DateTime? EmailVerifiedAt { get; init; }
    public string SecurityStamp { get; init; } = string.Empty;
    public string[] Roles { get; init; } = [];
}

public sealed class PasskeyLoginUser
{
    public int PasskeyId { get; init; }
    public byte[] PublicKey { get; init; } = [];
    public long StoredSignCount { get; init; }
    public int UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string SecurityStamp { get; init; } = string.Empty;
    public DateTime? EmailVerifiedAt { get; init; }
    public string[] Roles { get; init; } = [];
}

public sealed class PasskeyRow
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? LastUsedAt { get; init; }
}

public interface IAuthRepository : IAdmissionRepository
{
    Task<AuthUser?> FindByEmailAsync(string emailNormalized, CancellationToken ct);

    Task<AuthUser?> FindByIdAsync(int userId, CancellationToken ct);

    /// <summary>Null when the email is taken or the invite code lost a seat race.</summary>
    Task<int?> CreateUserAsync(string email, string emailNormalized, string passwordHash, string displayName, string? inviteCodeNormalized, CancellationToken ct);

    Task<string> IssueTokenAsync(int userId, string purpose, TimeSpan lifetime, CancellationToken ct);

    Task<int?> ConsumeEmailVerificationAsync(string tokenHash, CancellationToken ct);

    Task<int?> ConsumePasswordResetAsync(string tokenHash, string newPasswordHash, CancellationToken ct);

    /// <summary>Consumes the magic link, marks the email verified and returns the user to sign in; null if invalid.</summary>
    Task<AuthUser?> ConsumeMagicLinkAsync(string tokenHash, CancellationToken ct);

    Task<string?> GetSecurityStampAsync(int userId, CancellationToken ct);

    Task<IReadOnlyList<byte[]>> GetCredentialIdsByEmailAsync(string emailNormalized, CancellationToken ct);

    Task<IReadOnlyList<byte[]>> GetCredentialIdsForUserAsync(int userId, CancellationToken ct);

    Task<PasskeyLoginUser?> FindByCredentialIdAsync(byte[] credentialId, CancellationToken ct);

    Task UpdatePasskeyAfterUseAsync(int passkeyId, long signCount, CancellationToken ct);

    Task<bool> CredentialExistsAsync(byte[] credentialId, CancellationToken ct);

    Task<PasskeyRow> InsertPasskeyAsync(int userId, byte[] credentialId, byte[] publicKey, long signCount, string[] transports, Guid aaGuid, string name, CancellationToken ct);

    Task<IReadOnlyList<PasskeyRow>> ListPasskeysAsync(int userId, CancellationToken ct);

    Task<bool> DeletePasskeyAsync(int id, int userId, CancellationToken ct);
}

public sealed class AuthRepository : IAuthRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AuthRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string SelectUserColumns = """
        SELECT u."Id",
               u."Email",
               u."DisplayName",
               u."PasswordHash",
               u."EmailVerifiedAt",
               u."SecurityStamp",
               COALESCE(array_agg(DISTINCT ur."Role") FILTER (WHERE ur."Role" IS NOT NULL), '{}') AS "Roles"
        FROM "unshackled-word"."Users" u
                 LEFT JOIN "unshackled-word"."UserRoles" ur ON ur."UserId" = u."Id"
        """;

    private const string SelectByEmail = SelectUserColumns + """

        WHERE u."EmailNormalized" = @emailNormalized
        GROUP BY u."Id"
        """;

    private const string SelectById = SelectUserColumns + """

        WHERE u."Id" = @userId
        GROUP BY u."Id"
        """;

    public async Task<AuthUser?> FindByEmailAsync(string emailNormalized, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<AuthUser>(
            new CommandDefinition(SelectByEmail, new { emailNormalized }, cancellationToken: ct));
    }

    public async Task<AuthUser?> FindByIdAsync(int userId, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<AuthUser>(
            new CommandDefinition(SelectById, new { userId }, cancellationToken: ct));
    }

    // ---- registration ----

    private const string InsertUser = """
        INSERT INTO "unshackled-word"."Users" ("Email", "EmailNormalized", "PasswordHash", "DisplayName")
        VALUES (@email, @emailNormalized, @passwordHash, @displayName)
        RETURNING "Id"
        """;

    private const string CountUsers = """SELECT count(*) FROM "unshackled-word"."Users" """;

    private const string InsertRoles = """
        INSERT INTO "unshackled-word"."UserRoles" ("UserId", "Role")
        SELECT @userId, unnest(@roles::text[])
        """;

    // Locks the code row so concurrent registrations against the same code serialize on it.
    private const string LockInviteCode = """
        SELECT "Id", "MaxSeats"
        FROM "unshackled-word"."InviteCodes"
        WHERE "Code" = @code
          AND "RevokedAt" IS NULL
          AND "ExpiresAt" > now()
          AND ("BoundEmailNormalized" IS NULL OR "BoundEmailNormalized" = @emailNormalized)
        FOR UPDATE
        """;

    private const string CountRedemptions = """
        SELECT count(*) FROM "unshackled-word"."InviteCodeRedemptions" WHERE "InviteCodeId" = @inviteCodeId
        """;

    private const string InsertRedemption = """
        INSERT INTO "unshackled-word"."InviteCodeRedemptions" ("InviteCodeId", "UserId")
        VALUES (@inviteCodeId, @userId)
        """;

    public async Task<int?> CreateUserAsync(
        string email,
        string emailNormalized,
        string passwordHash,
        string displayName,
        string? inviteCodeNormalized,
        CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        using var transaction = connection.BeginTransaction();

        try
        {
            int? inviteCodeId = null;

            if (inviteCodeNormalized is not null)
            {
                var code = await connection.QuerySingleOrDefaultAsync<InviteCodeLockRow>(new CommandDefinition(
                    LockInviteCode, new { code = inviteCodeNormalized, emailNormalized }, transaction, cancellationToken: ct));

                var seatsUsed = code is null
                    ? 0
                    : await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                        CountRedemptions, new { inviteCodeId = code.Id }, transaction, cancellationToken: ct));

                if (code is null || seatsUsed >= code.MaxSeats)
                {
                    transaction.Rollback();
                    return null;
                }

                inviteCodeId = code.Id;
            }

            var userId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                InsertUser, new { email, emailNormalized, passwordHash, displayName }, transaction, cancellationToken: ct));

            var totalUsers = await connection.ExecuteScalarAsync<long>(
                new CommandDefinition(CountUsers, transaction: transaction, cancellationToken: ct));

            // The very first account is also admin; otherwise nothing could ever grant the first admin role.
            var roles = totalUsers == 1
                ? new[] { AppRoles.User, AppRoles.Admin }
                : new[] { AppRoles.User };

            await connection.ExecuteAsync(new CommandDefinition(
                InsertRoles, new { userId, roles }, transaction, cancellationToken: ct));

            if (inviteCodeId is not null)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    InsertRedemption, new { inviteCodeId, userId }, transaction, cancellationToken: ct));
            }

            transaction.Commit();
            return userId;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            transaction.Rollback();
            return null;
        }
    }

    private sealed class InviteCodeLockRow
    {
        public int Id { get; init; }
        public int MaxSeats { get; init; }
    }

    // ---- admission ----

    public async Task<long> CountUsersAsync(CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(CountUsers, cancellationToken: ct));
    }

    private const string SelectInviteCodeForCheck = """
        SELECT ic."Id",
               ic."BoundEmailNormalized",
               ic."MaxSeats",
               ic."ExpiresAt",
               ic."RevokedAt",
               count(r."UserId") AS "SeatsUsed"
        FROM "unshackled-word"."InviteCodes" ic
                 LEFT JOIN "unshackled-word"."InviteCodeRedemptions" r ON r."InviteCodeId" = ic."Id"
        WHERE ic."Code" = @code
        GROUP BY ic."Id"
        """;

    public async Task<InviteCodeCheckRow?> FindInviteCodeAsync(string code, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<InviteCodeCheckRow>(
            new CommandDefinition(SelectInviteCodeForCheck, new { code }, cancellationToken: ct));
    }

    // ---- single-use tokens ----

    private const string InsertToken = """
        INSERT INTO "unshackled-word"."AuthTokens" ("UserId", "Purpose", "TokenHash", "ExpiresAt")
        VALUES (@userId, @purpose, @tokenHash, @expiresAt)
        """;

    private const string ConsumeToken = """
        UPDATE "unshackled-word"."AuthTokens"
        SET "ConsumedAt" = now()
        WHERE "TokenHash" = @tokenHash
          AND "Purpose" = @purpose
          AND "ConsumedAt" IS NULL
          AND "ExpiresAt" > now()
        RETURNING "UserId"
        """;

    /// <summary>Generates a random token, stores only its hash and returns the raw value for the emailed link.</summary>
    public async Task<string> IssueTokenAsync(int userId, string purpose, TimeSpan lifetime, CancellationToken ct)
    {
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var tokenHash = AuthTokenHasher.Hash(rawToken);
        var expiresAt = DateTimeOffset.UtcNow.Add(lifetime);

        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            InsertToken, new { userId, purpose, tokenHash, expiresAt }, cancellationToken: ct));

        return rawToken;
    }

    private const string MarkEmailVerified = """
        UPDATE "unshackled-word"."Users"
        SET "EmailVerifiedAt" = COALESCE("EmailVerifiedAt", now()),
            "UpdatedAt"       = now()
        WHERE "Id" = @userId
        """;

    // A single UPDATE ... RETURNING, not exists-check + update: a concurrent second use cannot slip through.
    private static Task<int?> ConsumeAsync(IDbConnection connection, IDbTransaction transaction, string tokenHash, string purpose, CancellationToken ct) =>
        connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            ConsumeToken, new { tokenHash, purpose }, transaction, cancellationToken: ct));

    public async Task<int?> ConsumeEmailVerificationAsync(string tokenHash, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        using var transaction = connection.BeginTransaction();

        var userId = await ConsumeAsync(connection, transaction, tokenHash, AuthTokenPurpose.EmailVerification, ct);
        if (userId is null)
        {
            transaction.Rollback();
            return null;
        }

        await connection.ExecuteAsync(new CommandDefinition(MarkEmailVerified, new { userId }, transaction, cancellationToken: ct));
        transaction.Commit();
        return userId;
    }

    private const string UpdatePassword = """
        UPDATE "unshackled-word"."Users"
        SET "PasswordHash"  = @passwordHash,
            "SecurityStamp" = gen_random_uuid()::varchar,
            "UpdatedAt"     = now()
        WHERE "Id" = @userId
        """;

    public async Task<int?> ConsumePasswordResetAsync(string tokenHash, string newPasswordHash, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        using var transaction = connection.BeginTransaction();

        var userId = await ConsumeAsync(connection, transaction, tokenHash, AuthTokenPurpose.PasswordReset, ct);
        if (userId is null)
        {
            transaction.Rollback();
            return null;
        }

        // New stamp: every session issued before this reset stops validating.
        await connection.ExecuteAsync(new CommandDefinition(
            UpdatePassword, new { userId, passwordHash = newPasswordHash }, transaction, cancellationToken: ct));
        transaction.Commit();
        return userId;
    }

    public async Task<AuthUser?> ConsumeMagicLinkAsync(string tokenHash, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        using var transaction = connection.BeginTransaction();

        var userId = await ConsumeAsync(connection, transaction, tokenHash, AuthTokenPurpose.MagicLink, ct);
        if (userId is null)
        {
            transaction.Rollback();
            return null;
        }

        // Clicking a link mailed to this address proves ownership, so it also verifies the email.
        await connection.ExecuteAsync(new CommandDefinition(MarkEmailVerified, new { userId }, transaction, cancellationToken: ct));
        var user = await connection.QuerySingleAsync<AuthUser>(
            new CommandDefinition(SelectById, new { userId }, transaction, cancellationToken: ct));
        transaction.Commit();
        return user;
    }

    // ---- session ----

    private const string SelectStamp = """SELECT "SecurityStamp" FROM "unshackled-word"."Users" WHERE "Id" = @userId""";

    /// <summary>Null when the user no longer exists.</summary>
    public async Task<string?> GetSecurityStampAsync(int userId, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<string?>(
            new CommandDefinition(SelectStamp, new { userId }, cancellationToken: ct));
    }

    // ---- passkeys ----

    private const string SelectCredentialIdsByEmail = """
        SELECT pc."CredentialId"
        FROM "unshackled-word"."PasskeyCredentials" pc
                 JOIN "unshackled-word"."Users" u ON u."Id" = pc."UserId"
        WHERE u."EmailNormalized" = @emailNormalized
        """;

    private const string SelectCredentialIdsForUser = """
        SELECT "CredentialId" FROM "unshackled-word"."PasskeyCredentials" WHERE "UserId" = @userId
        """;

    public async Task<IReadOnlyList<byte[]>> GetCredentialIdsByEmailAsync(string emailNormalized, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        return (await connection.QueryAsync<byte[]>(
            new CommandDefinition(SelectCredentialIdsByEmail, new { emailNormalized }, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<byte[]>> GetCredentialIdsForUserAsync(int userId, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        return (await connection.QueryAsync<byte[]>(
            new CommandDefinition(SelectCredentialIdsForUser, new { userId }, cancellationToken: ct))).ToList();
    }

    private const string SelectByCredentialId = """
        SELECT pc."Id"        AS "PasskeyId",
               pc."PublicKey",
               pc."SignCount" AS "StoredSignCount",
               u."Id"         AS "UserId",
               u."Email",
               u."DisplayName",
               u."SecurityStamp",
               u."EmailVerifiedAt",
               COALESCE(array_agg(DISTINCT ur."Role") FILTER (WHERE ur."Role" IS NOT NULL), '{}') AS "Roles"
        FROM "unshackled-word"."PasskeyCredentials" pc
                 JOIN "unshackled-word"."Users" u ON u."Id" = pc."UserId"
                 LEFT JOIN "unshackled-word"."UserRoles" ur ON ur."UserId" = u."Id"
        WHERE pc."CredentialId" = @credentialId
        GROUP BY pc."Id", u."Id"
        """;

    public async Task<PasskeyLoginUser?> FindByCredentialIdAsync(byte[] credentialId, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<PasskeyLoginUser>(
            new CommandDefinition(SelectByCredentialId, new { credentialId }, cancellationToken: ct));
    }

    private const string UpdateAfterUse = """
        UPDATE "unshackled-word"."PasskeyCredentials"
        SET "SignCount" = @signCount, "LastUsedAt" = now()
        WHERE "Id" = @passkeyId
        """;

    public async Task UpdatePasskeyAfterUseAsync(int passkeyId, long signCount, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(UpdateAfterUse, new { passkeyId, signCount }, cancellationToken: ct));
    }

    private const string ExistsByCredentialId = """
        SELECT EXISTS(SELECT 1 FROM "unshackled-word"."PasskeyCredentials" WHERE "CredentialId" = @credentialId)
        """;

    public async Task<bool> CredentialExistsAsync(byte[] credentialId, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(ExistsByCredentialId, new { credentialId }, cancellationToken: ct));
    }

    private const string InsertCredential = """
        INSERT INTO "unshackled-word"."PasskeyCredentials" ("UserId", "CredentialId", "PublicKey", "SignCount", "Transports", "AaGuid", "Name")
        VALUES (@userId, @credentialId, @publicKey, @signCount, @transports, @aaGuid, @name)
        RETURNING "Id", "Name", "CreatedAt", "LastUsedAt"
        """;

    public async Task<PasskeyRow> InsertPasskeyAsync(
        int userId, byte[] credentialId, byte[] publicKey, long signCount, string[] transports, Guid aaGuid, string name, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        return await connection.QuerySingleAsync<PasskeyRow>(new CommandDefinition(
            InsertCredential, new { userId, credentialId, publicKey, signCount, transports, aaGuid, name }, cancellationToken: ct));
    }

    private const string SelectPasskeys = """
        SELECT "Id", "Name", "CreatedAt", "LastUsedAt"
        FROM "unshackled-word"."PasskeyCredentials"
        WHERE "UserId" = @userId
        ORDER BY "CreatedAt"
        """;

    public async Task<IReadOnlyList<PasskeyRow>> ListPasskeysAsync(int userId, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        return (await connection.QueryAsync<PasskeyRow>(
            new CommandDefinition(SelectPasskeys, new { userId }, cancellationToken: ct))).ToList();
    }

    private const string DeleteOwnedPasskey = """
        DELETE FROM "unshackled-word"."PasskeyCredentials" WHERE "Id" = @id AND "UserId" = @userId
        """;

    /// <summary>False means it does not exist or belongs to someone else; both get the same 404.</summary>
    public async Task<bool> DeletePasskeyAsync(int id, int userId, CancellationToken ct)
    {
        using var connection = await _connectionFactory.CreateDbConnectionAsync(ct);
        return await connection.ExecuteAsync(new CommandDefinition(DeleteOwnedPasskey, new { id, userId }, cancellationToken: ct)) > 0;
    }
}
