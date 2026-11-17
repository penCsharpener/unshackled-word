using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.ApiTokens;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.ApiTokens.List;

[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<ListApiTokensResponse>
{
    private readonly IApiTokenService _service;
    private readonly TimeProvider _time;

    public Endpoint(IApiTokenService service, TimeProvider time)
    {
        _service = service;
        _time = time;
    }

    public override void Configure()
    {
        Get("api-tokens");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Lists MCP tokens without any secret (prefix, user, expiry, revoked, last use).");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var tokens = await _service.ListAsync(ct);
        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.OkAsync(new ListApiTokensResponse { Tokens = tokens.Select(t => ApiTokenMapping.ToDto(t, now)).ToList() }, ct);
    }
}
