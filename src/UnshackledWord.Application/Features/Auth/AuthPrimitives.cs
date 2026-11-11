using System.Security.Cryptography;
using System.Text;

namespace UnshackledWord.Application.Features.Auth;

/// <summary>The <c>Purpose</c> values stored in <c>AuthTokens</c>.</summary>
public static class AuthTokenPurpose
{
    public const string EmailVerification = "email_verification";
    public const string PasswordReset = "password_reset";
    public const string MagicLink = "magic_link";
}

/// <summary>SHA-256 hash of a raw auth token, hex-encoded. The raw token is never persisted.</summary>
public static class AuthTokenHasher
{
    public static string Hash(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}

/// <summary>The single normalization rule for matching an email against <c>Users.EmailNormalized</c>.</summary>
public static class EmailNormalizer
{
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();
}

/// <summary>Trims, strips spaces and dashes, uppercases: a pasted code matches the generated one.</summary>
public static class InviteCodeNormalizer
{
    public static string Normalize(string code) =>
        code.Trim().Replace(" ", "").Replace("-", "").ToUpperInvariant();
}

public static class InviteCodeGenerator
{
    public static string Generate() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
}
