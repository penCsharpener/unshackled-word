using System.Security.Claims;
using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.UserAdmin;
using UnshackledWord.Domain.WebApi.UserAdmin;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Admin.Users.PasswordReset;

[RequiresLogin]
public sealed class Endpoint : Endpoint<SendPasswordResetRequest>
{
    private static readonly TimeSpan ResetLifetime = TimeSpan.FromHours(1);

    private readonly IUserAdminRepository _repository;
    private readonly IAdminLogRepository _adminLog;
    private readonly IAuthRepository _authRepository;
    private readonly AuthEmailBuilder _emailBuilder;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<Endpoint> _logger;

    public Endpoint(
        IUserAdminRepository repository,
        IAdminLogRepository adminLog,
        IAuthRepository authRepository,
        AuthEmailBuilder emailBuilder,
        IEmailSender emailSender,
        ILogger<Endpoint> logger)
    {
        _repository = repository;
        _adminLog = adminLog;
        _authRepository = authRepository;
        _emailBuilder = emailBuilder;
        _emailSender = emailSender;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("users/{id}/password-reset");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin);
        Summary(s => s.Summary = "Admin: emails a password-reset link (1 h); while the account has no password it is the 72 h welcome mail again. 204 on success.");
        Description(b => b.Produces(204).ProducesProblem(404).ProducesProblem(409).ProducesProblem(502));
    }

    public override async Task HandleAsync(SendPasswordResetRequest req, CancellationToken ct)
    {
        var actorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var target = await _repository.FindResetTargetAsync(req.Id, ct);

        if (target is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (target.IsAi)
        {
            await Send.ResultAsync(Conflict.Problem(UserAdminErrors.AiUser));
            return;
        }

        if (await _authRepository.IsDeactivatedAsync(target.Id, ct))
        {
            await Send.ResultAsync(Conflict.Problem(UserAdminErrors.Deactivated));
            return;
        }

        var welcome = !target.HasPassword;
        var lifetime = welcome ? AuthEmailBuilder.WelcomeLifetime : ResetLifetime;
        var locale = AuthEmailBuilder.ResolveLocale(HttpContext);

        try
        {
            var token = await _authRepository.IssueTokenAsync(target.Id, AuthTokenPurpose.PasswordReset, lifetime, supersedeOpen: true, ct);
            var message = welcome
                ? _emailBuilder.BuildWelcome(target.Email, target.DisplayName, token, locale)
                : _emailBuilder.BuildPasswordReset(target.Email, target.DisplayName, token, locale);
            await _emailSender.SendAsync(message, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Unlike create/update the mail is the whole point here, so the admin must see the failure.
            _logger.LogError(ex, "Password-reset mail for user {UserId} was not sent", target.Id);
            await Send.ResultAsync(Results.Problem(statusCode: StatusCodes.Status502BadGateway, title: "mail_failed", extensions: new Dictionary<string, object?> { ["code"] = "mail_failed" }));
            return;
        }

        var expiresAt = DateTimeOffset.UtcNow.Add(lifetime);
        await _adminLog.AppendAsync(
            new AdminLogEntry(
                actorId,
                welcome ? AdminLogActions.WelcomeSent : AdminLogActions.PasswordResetSent,
                target.Id,
                null,
                System.Text.Json.JsonSerializer.Serialize(new { expiresAt })),
            null,
            ct);

        await Send.NoContentAsync(ct);
    }
}
