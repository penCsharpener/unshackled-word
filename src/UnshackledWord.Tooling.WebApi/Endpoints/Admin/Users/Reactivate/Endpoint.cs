using System.Security.Claims;
using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.UserAdmin;
using UnshackledWord.Domain.WebApi.UserAdmin;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Admin.Users.Reactivate;

[RequiresLogin]
public sealed class Endpoint : Endpoint<GetUserRequest, UserDetail>
{
    private readonly IUserAdminRepository _repository;

    public Endpoint(IUserAdminRepository repository)
    {
        _repository = repository;
    }

    public override void Configure()
    {
        Post("users/{id}/reactivate");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin);
        Summary(s => s.Summary = "Admin: lifts a deactivation; password and passkeys stay, the person signs in as before.");
        Description(b => b.Produces<UserDetail>(200).ProducesProblem(404).ProducesProblem(409));
    }

    public override async Task HandleAsync(GetUserRequest req, CancellationToken ct)
    {
        var actorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var outcome = await _repository.ReactivateAsync(actorId, req.Id, ct);

        if (outcome.NotFound)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (outcome.Error is { } code)
        {
            await Send.ResultAsync(Conflict.Problem(code));
            return;
        }

        await Send.OkAsync(outcome.Detail!, ct);
    }
}
