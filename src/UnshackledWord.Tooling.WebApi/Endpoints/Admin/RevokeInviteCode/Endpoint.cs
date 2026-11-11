using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Admin.RevokeInviteCode;

[RequiresLogin]
public sealed class Endpoint : Endpoint<RevokeInviteCodeRequest>
{
    private readonly IInviteAdminRepository _repository;

    public Endpoint(IInviteAdminRepository repository)
    {
        _repository = repository;
    }

    public override void Configure()
    {
        Delete("invite-codes/{id}");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin);
        Description(b => b.Produces(204).Produces(404));
        Summary(s => s.Summary = "Admin: revokes an invite code (sets RevokedAt; redemptions stay as audit trail). Idempotent.");
    }

    public override async Task HandleAsync(RevokeInviteCodeRequest req, CancellationToken ct)
    {
        if (await _repository.RevokeAsync(req.Id, ct))
        {
            await Send.NoContentAsync(ct);
        }
        else
        {
            await Send.NotFoundAsync(ct);
        }
    }
}
