using System.Security.Claims;
using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Admin.CreateInviteCode;

[RequiresLogin]
public sealed class Endpoint : Endpoint<CreateInviteCodeRequest, CreateInviteCodeResponse>
{
    private readonly IInviteAdminRepository _repository;

    public Endpoint(IInviteAdminRepository repository)
    {
        _repository = repository;
    }

    public override void Configure()
    {
        Post("invite-codes");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin);
        Summary(s => s.Summary = "Admin: creates an invite code (seats, expiry, optional note, optional restricting email).");
    }

    public override async Task HandleAsync(CreateInviteCodeRequest req, CancellationToken ct)
    {
        var createdBy = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var bound = string.IsNullOrWhiteSpace(req.BoundEmail) ? null : EmailNormalizer.Normalize(req.BoundEmail);

        var (id, code) = await _repository.CreateAsync(req.MaxSeats, req.ExpiresAt, createdBy, req.Note, bound, ct);

        await Send.OkAsync(new CreateInviteCodeResponse
        {
            Id = id,
            Code = code,
            MaxSeats = req.MaxSeats,
            ExpiresAt = req.ExpiresAt,
            Note = req.Note,
            BoundEmail = bound,
        }, ct);
    }
}

public sealed class CreateInviteCodeValidator : Validator<CreateInviteCodeRequest>
{
    public CreateInviteCodeValidator()
    {
        Include(new CreateInviteCodeRequestValidator());
    }
}
