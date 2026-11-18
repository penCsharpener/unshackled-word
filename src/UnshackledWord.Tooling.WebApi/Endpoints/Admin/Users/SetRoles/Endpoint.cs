using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.UserAdmin;
using UnshackledWord.Domain.WebApi.UserAdmin;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Admin.Users.SetRoles;

[RequiresLogin]
public sealed class Endpoint : Endpoint<SetRolesRequest, UserDetail>
{
    private readonly IUserAdminRepository _repository;

    public Endpoint(IUserAdminRepository repository)
    {
        _repository = repository;
    }

    public override void Configure()
    {
        Put("users/{id}/roles");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin);
        Summary(s => s.Summary = "Admin: sets the assignable roles (admin, maintainer, reviewer) of a user; ends that user's sessions when something changed.");
        Description(b => b.Produces<UserDetail>(200).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409));
    }

    public override async Task HandleAsync(SetRolesRequest req, CancellationToken ct)
    {
        var actorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var outcome = await _repository.SetRolesAsync(actorId, req.Id, req.Roles, ct);

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

public sealed class SetRolesValidator : Validator<SetRolesRequest>
{
    public SetRolesValidator()
    {
        RuleFor(x => x.Roles).NotNull();
        RuleForEach(x => x.Roles).Must(r => UserAdminGuards.AssignableRoles.Contains(r)).WithMessage("Only admin, maintainer and reviewer can be assigned.");
        RuleFor(x => x.Roles).Must(r => r.Distinct().Count() == r.Length).WithMessage("Roles must not repeat.");
    }
}
