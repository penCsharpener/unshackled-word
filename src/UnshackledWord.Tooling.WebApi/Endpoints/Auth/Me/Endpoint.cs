using System.Security.Claims;
using FastEndpoints;
using UnshackledWord.Domain.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Auth.Me;

/// <summary>Reflects the session claims resolved at login; no database call.</summary>
[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<MeResponse>
{
    public override void Configure()
    {
        Get("me");
        Group<RouteGroupConfig>();
        Summary(s => s.Summary = "Returns the current session's user id, email, display name and roles.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var user = HttpContext.User;
        await Send.OkAsync(new MeResponse
        {
            UserId = int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!),
            Email = user.FindFirstValue(ClaimTypes.Email)!,
            DisplayName = user.FindFirstValue(AppClaimTypes.DisplayName)!,
            Roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToArray(),
        }, ct);
    }
}
