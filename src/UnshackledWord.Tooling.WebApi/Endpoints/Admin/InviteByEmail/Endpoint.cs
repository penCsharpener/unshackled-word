using System.Security.Claims;
using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Admin.InviteByEmail;

[RequiresLogin]
public sealed class Endpoint : Endpoint<InviteByEmailRequest, InviteByEmailResponse>
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(14);

    private readonly IInviteAdminRepository _repository;
    private readonly InvitationEmailBuilder _emailBuilder;
    private readonly IEmailSender _emailSender;

    public Endpoint(IInviteAdminRepository repository, InvitationEmailBuilder emailBuilder, IEmailSender emailSender)
    {
        _repository = repository;
        _emailBuilder = emailBuilder;
        _emailSender = emailSender;
    }

    public override void Configure()
    {
        Post("invitations");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin);
        Summary(s => s.Summary = "Admin: creates a single-seat invite code bound to the address (reissuing any live one) and emails the registration link.");
    }

    public override async Task HandleAsync(InviteByEmailRequest req, CancellationToken ct)
    {
        var email = req.Email.Trim();
        var createdBy = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var (id, code) = await _repository.CreateInvitationAsync(
            EmailNormalizer.Normalize(email), DateTimeOffset.UtcNow.Add(InvitationLifetime), createdBy, req.Note, ct);

        await _emailSender.SendAsync(_emailBuilder.Build(email, code, AuthEmailBuilder.ResolveLocale(HttpContext)), ct);

        await Send.OkAsync(new InviteByEmailResponse { Id = id, Email = email }, ct);
    }
}

public sealed class InviteByEmailValidator : Validator<InviteByEmailRequest>
{
    public InviteByEmailValidator()
    {
        Include(new InviteByEmailRequestValidator());
    }
}
