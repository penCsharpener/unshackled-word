using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.UserAdmin;
using UnshackledWord.Domain.WebApi.UserAdmin;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Admin.Users.List;

[RequiresLogin]
public sealed class Endpoint : Endpoint<ListUsersRequest, UserListResponse>
{
    private readonly IUserAdminRepository _repository;

    public Endpoint(IUserAdminRepository repository)
    {
        _repository = repository;
    }

    public override void Configure()
    {
        Get("users");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin);
        Summary(s => s.Summary = "Admin: lists accounts with roles and status; filter by search text (name or email), role and status; paged.");
        Description(b => b.Produces<UserListResponse>(200).ProducesProblem(400));
    }

    public override async Task HandleAsync(ListUsersRequest req, CancellationToken ct)
    {
        await Send.OkAsync(await _repository.ListAsync(new UserListQuery(req.Search, req.Role, req.Status, req.Page, req.PageSize), ct), ct);
    }
}

public sealed class ListUsersValidator : Validator<ListUsersRequest>
{
    public ListUsersValidator()
    {
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.Role).Must(r => AppRoles.All.Contains(r!)).When(x => !string.IsNullOrEmpty(x.Role)).WithMessage("Unknown role.");
        RuleFor(x => x.Status).Must(s => UserStatuses.All.Contains(s!)).When(x => !string.IsNullOrEmpty(x.Status)).WithMessage("Unknown status.");
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}
