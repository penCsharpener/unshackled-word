using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.DataQuality;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.DataQuality.Refresh;

[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<RefreshResponse>
{
    private readonly DqRefreshCoordinator _coordinator;

    public Endpoint(DqRefreshCoordinator coordinator)
    {
        _coordinator = coordinator;
    }

    public override void Configure()
    {
        Post("refresh");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Recomputes the DqWordFacts snapshot in the background. 202 {refreshing:true}; 409 when a refresh already runs.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!_coordinator.TryStart())
        {
            await Send.ResponseAsync(new RefreshResponse { Refreshing = true }, 409, ct);
            return;
        }

        await Send.ResponseAsync(new RefreshResponse { Refreshing = true }, 202, ct);
    }
}
