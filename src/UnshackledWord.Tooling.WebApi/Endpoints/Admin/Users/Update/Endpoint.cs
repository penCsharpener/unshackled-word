using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.UserAdmin;
using UnshackledWord.Domain.WebApi.UserAdmin;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Admin.Users.Update;

[RequiresLogin]
public sealed class Endpoint : Endpoint<UpdateUserRequest, UserDetail>
{
    private readonly IUserAdminRepository _repository;
    private readonly AuthEmailBuilder _emailBuilder;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<Endpoint> _logger;

    public Endpoint(IUserAdminRepository repository, AuthEmailBuilder emailBuilder, IEmailSender emailSender, ILogger<Endpoint> logger)
    {
        _repository = repository;
        _emailBuilder = emailBuilder;
        _emailSender = emailSender;
        _logger = logger;
    }

    public override void Configure()
    {
        Put("users/{id}");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin);
        Summary(s => s.Summary = "Admin: changes display name and email of a user; an email change marks it verified, ends that user's sessions and mails a notice to the old and the new address.");
        Description(b => b.Produces<UserDetail>(200).ProducesProblem(400).ProducesProblem(404).ProducesProblem(409));
    }

    public override async Task HandleAsync(UpdateUserRequest req, CancellationToken ct)
    {
        var actorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var outcome = await _repository.UpdateAsync(actorId, req.Id, req.DisplayName, req.Email, ct);

        if (outcome.NotFound)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (outcome.Error is { } code)
        {
            await Send.ResultAsync(Conflict.Problem(code, outcome.ExistingUserId));
            return;
        }

        var detail = outcome.Detail!;

        if (outcome.PreviousEmail is { } previous)
        {
            await SendNoticesAsync(previous, detail, ct);
        }

        await Send.OkAsync(detail, ct);
    }

    // The change is committed already; a failed notice is logged, not rolled back.
    private async Task SendNoticesAsync(string oldEmail, UserDetail user, CancellationToken ct)
    {
        var locale = AuthEmailBuilder.ResolveLocale(HttpContext);

        foreach (var toNew in new[] { false, true })
        {
            try
            {
                var to = toNew ? user.Email : oldEmail;
                await _emailSender.SendAsync(_emailBuilder.BuildEmailChangedNotice(to, user.DisplayName, oldEmail, user.Email, toNew, locale), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Email-change notice for user {UserId} was not sent (to new address: {ToNew})", user.Id, toNew);
            }
        }
    }
}

public sealed class UpdateUserValidator : Validator<UpdateUserRequest>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(254).EmailAddress();
        RuleFor(x => x.DisplayName).Must(n => n is not null && n.Trim().Length is >= 1 and <= 100).WithMessage("Display name must be 1 to 100 characters.");
    }
}
