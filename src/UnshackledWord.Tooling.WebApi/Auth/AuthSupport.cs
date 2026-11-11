namespace UnshackledWord.Tooling.WebApi.Auth;

/// <summary>Claim types beyond the standard ones, embedded in the session cookie at login.</summary>
public static class AppClaimTypes
{
    public const string DisplayName = "display_name";

    /// <summary>The user's <c>SecurityStamp</c> at login time; checked against the DB value on every authenticated request.</summary>
    public const string SecurityStamp = "security_stamp";
}

/// <summary>Marks an endpoint that requires a signed-in user. Every other endpoint is anonymous by default (see Program.cs).</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RequiresLoginAttribute : Attribute;

/// <summary>Bound from the "App" section of appsettings.json.</summary>
public sealed class AppOptions
{
    /// <summary>Base URL of the frontend: links in emails and the WebAuthn origin are derived from it.</summary>
    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";

    /// <summary>Whether the session cookie gets the Secure attribute. Default true; plain-HTTP dev opts out.</summary>
    public bool RequireSecureCookies { get; set; } = true;
}

/// <summary>Bound from the "Email" section of appsettings.json.</summary>
public sealed class EmailOptions
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;

    /// <summary>MailKit SecureSocketOptions name: None (Mailpit only), StartTls (default), SslOnConnect or Auto.</summary>
    public string Security { get; set; } = "StartTls";

    /// <summary>Empty means no SMTP AUTH (Mailpit).</summary>
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
}

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}

/// <summary>BCrypt.Net-Next's SHA384-prehashed "enhanced" variant (avoids the 72-byte truncation).</summary>
public sealed class BCryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.EnhancedHashPassword(password);

    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.EnhancedVerify(password, hash);
}
