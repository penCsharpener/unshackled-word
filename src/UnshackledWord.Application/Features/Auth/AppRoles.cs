namespace UnshackledWord.Application.Features.Auth;

/// <summary>Role names exactly as stored in <c>UserRoles.Role</c> and embedded as role claims at login.</summary>
public static class AppRoles
{
    /// <summary>Full access; appoints maintainers and reviewers. The very first account is granted this.</summary>
    public const string Admin = "admin";

    /// <summary>Merges suggestions (the single gatekeeper of the Linux-style editing model).</summary>
    public const string Maintainer = "maintainer";

    /// <summary>Reviews and confirms AI/human mappings and suggestions.</summary>
    public const string Reviewer = "reviewer";

    /// <summary>Every registered account; may suggest changes.</summary>
    public const string User = "user";

    public static readonly string[] All = [Admin, Maintainer, Reviewer, User];

    /// <summary>Roles for an endpoint's <c>Roles(...)</c>: the given ones plus <see cref="Admin"/>, which is a superuser.</summary>
    public static string[] WithAdmin(params string[] roles) => [.. roles, Admin];
}
