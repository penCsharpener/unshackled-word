using System.Security.Claims;
using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.UserAdmin;
using UnshackledWord.Domain.WebApi.UserAdmin;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Admin.Users.Get;

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
        Get("users/{id}");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin);
        Summary(s => s.Summary = "Admin: one account with roles, status, sign-in methods and the guard hints (isSelf, isLastActiveAdmin).");
        Description(b => b.Produces<UserDetail>(200).ProducesProblem(404));
    }

    public override async Task HandleAsync(GetUserRequest req, CancellationToken ct)
    {
        var actorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var detail = await _repository.GetDetailAsync(req.Id, actorId, ct);

        if (detail is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(detail, ct);
    }
}
