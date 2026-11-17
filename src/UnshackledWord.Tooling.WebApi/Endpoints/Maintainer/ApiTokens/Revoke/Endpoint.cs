using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.ApiTokens;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.ApiTokens.Revoke;

[RequiresLogin]
public sealed class Endpoint : Endpoint<ApiTokenIdRequest>
{
    private readonly IApiTokenService _service;

    public Endpoint(IApiTokenService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("api-tokens/{id}/revoke");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Revokes an MCP token; the next submit with it fails. 404 when the token does not exist or was revoked before.");
    }

    public override async Task HandleAsync(ApiTokenIdRequest req, CancellationToken ct)
    {
        if (!await _service.RevokeAsync(req.Id, ct))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.NoContentAsync(ct);
    }
}

public sealed class Validator : Validator<ApiTokenIdRequest>
{
    public Validator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}
