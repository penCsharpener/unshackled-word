using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;

namespace UnshackledWord.Tooling.WebApi.Auth;

/// <summary>Assembles the claims principal and signs a user into the cookie session (the one place the claim set is built).</summary>
public sealed class SessionSignIn
{
    public async Task SignInAsync(
        HttpContext httpContext,
        int userId,
        string email,
        string displayName,
        string securityStamp,
        IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(AppClaimTypes.DisplayName, displayName),
            new(AppClaimTypes.SecurityStamp, securityStamp),
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await httpContext.SignInAsync(new ClaimsPrincipal(identity));
    }
}

/// <summary>
/// Short-lived server-side state of the two passkey ceremonies (challenge and options). In-memory: fine for a
/// single-instance deployment; a ceremony lost to a restart simply has to be retried.
/// </summary>
public sealed class PasskeyCeremonyStore
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private readonly IMemoryCache _cache;

    public PasskeyCeremonyStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void SetRegistration<T>(int userId, T options) => _cache.Set($"passkey:reg:{userId}", options, Lifetime);

    public T? GetRegistration<T>(int userId) => _cache.Get<T>($"passkey:reg:{userId}");

    public void RemoveRegistration(int userId) => _cache.Remove($"passkey:reg:{userId}");

    public void SetLogin<T>(string flowId, T options) => _cache.Set($"passkey:login:{flowId}", options, Lifetime);

    public T? GetLogin<T>(string flowId) => _cache.Get<T>($"passkey:login:{flowId}");

    public void RemoveLogin(string flowId) => _cache.Remove($"passkey:login:{flowId}");
}
