using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.UserAdmin;
using UnshackledWord.Domain.WebApi.UserAdmin;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Admin.Users.Deactivate;

[RequiresLogin]
public sealed class Endpoint : Endpoint<DeactivateRequest, UserDetail>
{
    private readonly IUserAdminRepository _repository;

    public Endpoint(IUserAdminRepository repository)
    {
        _repository = repository;
    }

    public override void Configure()
    {
        Post("users/{id}/deactivate");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin);
        Summary(s => s.Summary = "Admin: deactivates an account (optional reason). Ends its sessions, kills open emailed links and blocks every sign-in path until reactivated.");
        Description(b => b.Produces<UserDetail>(200).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409));
    }

    public override async Task HandleAsync(DeactivateRequest req, CancellationToken ct)
    {
        var actorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var outcome = await _repository.DeactivateAsync(actorId, req.Id, req.Reason, ct);

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

public sealed class DeactivateValidator : Validator<DeactivateRequest>
{
    public DeactivateValidator()
    {
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}
