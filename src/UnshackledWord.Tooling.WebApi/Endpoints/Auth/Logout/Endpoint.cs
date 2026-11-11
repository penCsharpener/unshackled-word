using FastEndpoints;
using Microsoft.AspNetCore.Authentication;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Auth.Logout;

[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("logout");
        Group<RouteGroupConfig>();
        Summary(s => s.Summary = "Signs the caller out, clearing the session cookie.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await HttpContext.SignOutAsync();
        await Send.NoContentAsync(ct);
    }
}
