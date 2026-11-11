using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Admin.ListInviteCodes;

[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<ListInviteCodesResponse>
{
    private readonly IInviteAdminRepository _repository;

    public Endpoint(IInviteAdminRepository repository)
    {
        _repository = repository;
    }

    public override void Configure()
    {
        Get("invite-codes");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin);
        Summary(s => s.Summary = "Admin: lists all invite codes with seats used, expiry and revoked state.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(new ListInviteCodesResponse { InviteCodes = await _repository.ListAsync(ct) }, ct);
    }
}
