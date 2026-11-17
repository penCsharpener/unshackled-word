using System.Security.Cryptography;
using System.Text;
using UnshackledWord.Application.Features.Auth;

namespace UnshackledWord.Application.Features.ApiTokens;

public static class ApiTokenScopes
{
    /// <summary>May submit agent proposals through the MCP server and build packages bound to the token's user.</summary>
    public const string Propose = "propose";
}

public sealed class ApiTokenRecord
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TokenPrefix { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

/// <summary>A user a token can be issued for (role ai, maintainer or admin).</summary>
public sealed class ApiTokenUserRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public IReadOnlyList<string> Roles { get; set; } = [];
}

public sealed class ApiTokenInsert
{
    public int UserId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string TokenHash { get; init; } = string.Empty;
    public string TokenPrefix { get; init; } = string.Empty;
    public string Scope { get; init; } = ApiTokenScopes.Propose;
    public int CreatedByUserId { get; init; }
    public DateTime ExpiresAt { get; init; }
}

public interface IApiTokenRepository
{
    /// <summary>Roles of a user; null when the user does not exist.</summary>
    Task<IReadOnlyList<string>?> GetUserRolesAsync(int userId, CancellationToken token = default);

    Task<ApiTokenRecord> InsertAsync(ApiTokenInsert token, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApiTokenRecord>> ListAsync(CancellationToken token = default);

    /// <summary>Users with role ai, maintainer or admin (the ones a token can be issued for), with all their roles.</summary>
    Task<IReadOnlyList<ApiTokenUserRecord>> ListEligibleUsersAsync(CancellationToken token = default);

    /// <summary>Sets <c>RevokedAt</c> if not set yet; false when the token does not exist or was revoked before.</summary>
    Task<bool> RevokeAsync(int id, CancellationToken token = default);

    Task<ApiTokenRecord?> FindByHashAsync(string tokenHash, CancellationToken token = default);

    Task TouchAsync(int id, CancellationToken token = default);

    /// <summary>Verse proposals stored today (UTC) through runs of this token, superseded ones included.</summary>
    Task<int> CountProposalsTodayAsync(int id, CancellationToken token = default);
}

public enum ApiTokenCheck
{
    Valid,
    Missing,
    Malformed,
    Unknown,
    Revoked,
    Expired,

    /// <summary>The token has another scope than the one asked for.</summary>
    WrongScope,

    /// <summary>The user of the token lost the role ai/maintainer/admin (or no longer exists).</summary>
    UserNotAllowed,
}

public sealed class ApiTokenValidation
{
    public ApiTokenCheck Check { get; init; }
    public int TokenId { get; init; }
    public int UserId { get; init; }

    public bool IsValid => Check == ApiTokenCheck.Valid;

    public string Message => Check switch
    {
        ApiTokenCheck.Valid => "ok",
        ApiTokenCheck.Missing => "no API token: send 'Authorization: Bearer <token>' (a maintainer creates the token).",
        ApiTokenCheck.Malformed => "the API token is malformed.",
        ApiTokenCheck.Unknown => "unknown API token.",
        ApiTokenCheck.Revoked => "the API token was revoked.",
        ApiTokenCheck.Expired => "the API token has expired.",
        ApiTokenCheck.WrongScope => "the API token does not have the required scope.",
        _ => "the user of this API token is not allowed to propose.",
    };
}

public sealed class CreatedApiToken
{
    public required ApiTokenRecord Record { get; init; }

    /// <summary>The token string; exists only in this object, only its hash is stored.</summary>
    public required string Secret { get; init; }
}

public enum ApiTokenCreateOutcome
{
    Created,
    UserNotFound,
    UserNotAllowed,
}

public sealed class ApiTokenCreateResult
{
    public ApiTokenCreateOutcome Outcome { get; init; }
    public CreatedApiToken? Created { get; init; }
}

public interface IApiTokenService
{
    Task<ApiTokenCreateResult> CreateAsync(int userId, string name, int expiresInDays, int createdByUserId, CancellationToken token = default);

    Task<IReadOnlyList<ApiTokenRecord>> ListAsync(CancellationToken token = default);

    /// <summary>Users a token can be issued for: role ai, maintainer or admin, <c>ai:&lt;model&gt;</c> users first.</summary>
    Task<IReadOnlyList<ApiTokenUserRecord>> ListEligibleUsersAsync(CancellationToken token = default);

    Task<bool> RevokeAsync(int id, CancellationToken token = default);

    /// <summary>Checks the bearer value (with or without the "Bearer " prefix); on success the last-use time is updated.</summary>
    Task<ApiTokenValidation> ValidateAsync(string? bearer, string requiredScope, CancellationToken token = default);

    Task<int> CountProposalsTodayAsync(int tokenId, CancellationToken token = default);
}

/// <summary>
/// MCP bearer tokens (roadmap 0016.8). Format <c>uwt_</c> + 43 base64url characters (32 random bytes = 256 bit). Only the lower-case hex SHA-256 of the whole
/// string is stored: the token has far more entropy than a password, so a fast hash is enough and the lookup can use the hash as a unique key.
/// </summary>
public sealed class ApiTokenService : IApiTokenService
{
    public const string Prefix = "uwt_";
    public const int RandomBytes = 32;
    public const int MaxDays = 365;
    public const int DefaultDays = 30;
    private const int TokenLength = 4 + 43;

    private static readonly string[] AllowedRoles = [AppRoles.Ai, AppRoles.Maintainer, AppRoles.Admin];

    private readonly IApiTokenRepository _repository;
    private readonly TimeProvider _time;

    public ApiTokenService(IApiTokenRepository repository, TimeProvider time)
    {
        _repository = repository;
        _time = time;
    }

    public static string Generate() => Prefix + Base64Url(RandomNumberGenerator.GetBytes(RandomBytes));

    public static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static string? ExtractBearer(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return null;
        }

        var value = header.Trim();
        if (value.Equals("Bearer", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? value[7..].Trim() : value;
    }

    public async Task<ApiTokenCreateResult> CreateAsync(int userId, string name, int expiresInDays, int createdByUserId, CancellationToken token = default)
    {
        var roles = await _repository.GetUserRolesAsync(userId, token);
        if (roles is null)
        {
            return new ApiTokenCreateResult { Outcome = ApiTokenCreateOutcome.UserNotFound };
        }

        if (!roles.Any(r => AllowedRoles.Contains(r)))
        {
            return new ApiTokenCreateResult { Outcome = ApiTokenCreateOutcome.UserNotAllowed };
        }

        var secret = Generate();
        var days = Math.Clamp(expiresInDays <= 0 ? DefaultDays : expiresInDays, 1, MaxDays);
        var record = await _repository.InsertAsync(new ApiTokenInsert
        {
            UserId = userId,
            Name = name.Trim(),
            TokenHash = Hash(secret),
            TokenPrefix = secret[..8],
            CreatedByUserId = createdByUserId,
            ExpiresAt = _time.GetUtcNow().UtcDateTime.AddDays(days),
        }, token);

        return new ApiTokenCreateResult { Outcome = ApiTokenCreateOutcome.Created, Created = new CreatedApiToken { Record = record, Secret = secret } };
    }

    public Task<IReadOnlyList<ApiTokenRecord>> ListAsync(CancellationToken token = default) => _repository.ListAsync(token);

    public async Task<IReadOnlyList<ApiTokenUserRecord>> ListEligibleUsersAsync(CancellationToken token = default)
    {
        var users = await _repository.ListEligibleUsersAsync(token);

        return users
            .OrderBy(u => u.Roles.Contains(AppRoles.Ai) ? 0 : 1)
            .ThenBy(u => u.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public Task<bool> RevokeAsync(int id, CancellationToken token = default) => _repository.RevokeAsync(id, token);

    public Task<int> CountProposalsTodayAsync(int tokenId, CancellationToken token = default) => _repository.CountProposalsTodayAsync(tokenId, token);

    public async Task<ApiTokenValidation> ValidateAsync(string? bearer, string requiredScope, CancellationToken token = default)
    {
        var secret = ExtractBearer(bearer);
        if (string.IsNullOrEmpty(secret))
        {
            return new ApiTokenValidation { Check = ApiTokenCheck.Missing };
        }

        if (secret.Length != TokenLength || !secret.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return new ApiTokenValidation { Check = ApiTokenCheck.Malformed };
        }

        var record = await _repository.FindByHashAsync(Hash(secret), token);
        if (record is null)
        {
            return new ApiTokenValidation { Check = ApiTokenCheck.Unknown };
        }

        ApiTokenValidation Result(ApiTokenCheck check) => new() { Check = check, TokenId = record.Id, UserId = record.UserId };

        if (record.RevokedAt is not null)
        {
            return Result(ApiTokenCheck.Revoked);
        }

        if (record.ExpiresAt.ToUniversalTime() <= _time.GetUtcNow().UtcDateTime)
        {
            return Result(ApiTokenCheck.Expired);
        }

        if (!string.Equals(record.Scope, requiredScope, StringComparison.Ordinal))
        {
            return Result(ApiTokenCheck.WrongScope);
        }

        var roles = await _repository.GetUserRolesAsync(record.UserId, token);
        if (roles is null || !roles.Any(r => AllowedRoles.Contains(r)))
        {
            return Result(ApiTokenCheck.UserNotAllowed);
        }

        await _repository.TouchAsync(record.Id, token);

        return Result(ApiTokenCheck.Valid);
    }

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
