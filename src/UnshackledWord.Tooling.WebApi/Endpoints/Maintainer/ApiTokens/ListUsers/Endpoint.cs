using FastEndpoints;
using UnshackledWord.Application.Features.ApiTokens;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.ApiTokens.ListUsers;

[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<ListApiTokenUsersResponse>
{
    private readonly IApiTokenService _service;

    public Endpoint(IApiTokenService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("api-tokens/users");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Users a token can be issued for (role ai, maintainer or admin), ai:<model> users first.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var users = await _service.ListEligibleUsersAsync(ct);
        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.OkAsync(new ListApiTokenUsersResponse { Users = users.Select(u => new ApiTokenUserDto { Id = u.Id, Name = u.Name, Roles = [.. u.Roles] }).ToList() }, ct);
    }
}
