namespace UnshackledWord.Domain.WebApi.Auth;

// Request/response bodies of /api/auth/... Shapes match lexio's so the frontend can be ported 1:1.

/// <summary>InviteCode is optional and ignored while registration is open.</summary>
public sealed class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? InviteCode { get; set; }
}

public sealed class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>Response of register, login, magic-link login and passkey login.</summary>
public sealed class AuthUserResponse
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class MeResponse
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public IReadOnlyList<string> Roles { get; set; } = [];
}

/// <summary>Used by verify-email and magic-link-login.</summary>
public sealed class TokenRequest
{
    public string Token { get; set; } = string.Empty;
}

/// <summary>Used by request-password-reset and request-magic-link.</summary>
public sealed class EmailRequest
{
    public string Email { get; set; } = string.Empty;
}

public sealed class ResetPasswordRequest
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>Mode is "open" or "restricted". Never carries the allowlist or any invite code.</summary>
public sealed class RegistrationPolicyResponse
{
    public string Mode { get; set; } = "open";
    public bool InviteCodeRequired { get; set; }
}

/// <summary>Email is optional: given, it scopes the browser prompt to that account's passkeys.</summary>
public sealed class GetPasskeyLoginOptionsRequest
{
    public string? Email { get; set; }
}

public sealed class PasskeySummary
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
}

public sealed class ListPasskeysResponse
{
    public IReadOnlyList<PasskeySummary> Passkeys { get; set; } = [];
}

public sealed class DeletePasskeyRequest
{
    public int Id { get; set; }
}

public sealed class VerifyPasskeyRegistrationResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
