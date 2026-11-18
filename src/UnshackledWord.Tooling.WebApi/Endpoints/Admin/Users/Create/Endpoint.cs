using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.UserAdmin;
using UnshackledWord.Domain.WebApi.UserAdmin;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Admin.Users.Create;

[RequiresLogin]
public sealed class Endpoint : Endpoint<CreateUserRequest, UserDetail>
{
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
        Post("users");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin);
        Summary(s => s.Summary = "Admin: creates a verified account without password (roles user + the selected ones) and emails a 72 h set-password link.");
        Description(b => b.Produces<UserDetail>(201).ProducesProblem(400).ProducesProblem(409));
    }

    public override async Task HandleAsync(CreateUserRequest req, CancellationToken ct)
    {
        var actorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var outcome = await _repository.CreateAsync(actorId, req.Email, req.DisplayName, req.Roles, ct);

        if (outcome.ExistingUserId is { } existing)
        {
            await Send.ResultAsync(Conflict.Problem(UserAdminErrors.EmailTaken, existing));
            return;
        }

        var detail = outcome.Detail!;
        await SendWelcomeAsync(actorId, detail, ct);

        await Send.ResponseAsync(detail, StatusCodes.Status201Created, ct);
    }

    // The account is committed already; a failed mail is logged and the admin can resend it from the user page.
    private async Task SendWelcomeAsync(int actorId, UserDetail user, CancellationToken ct)
    {
        try
        {
            var token = await _authRepository.IssueTokenAsync(user.Id, AuthTokenPurpose.PasswordReset, AuthEmailBuilder.WelcomeLifetime, supersedeOpen: true, ct);
            await _emailSender.SendAsync(_emailBuilder.BuildWelcome(user.Email, user.DisplayName, token, AuthEmailBuilder.ResolveLocale(HttpContext)), ct);

            var expiresAt = DateTimeOffset.UtcNow.Add(AuthEmailBuilder.WelcomeLifetime);
            await _adminLog.AppendAsync(
                new AdminLogEntry(actorId, AdminLogActions.WelcomeSent, user.Id, null, System.Text.Json.JsonSerializer.Serialize(new { expiresAt })), null, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Welcome mail for new user {UserId} was not sent", user.Id);
        }
    }
}

public sealed class CreateUserValidator : Validator<CreateUserRequest>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(254).EmailAddress();
        RuleFor(x => x.DisplayName).Must(n => n is not null && n.Trim().Length is >= 1 and <= 100).WithMessage("Display name must be 1 to 100 characters.");
        RuleFor(x => x.Roles).NotNull();
        RuleForEach(x => x.Roles).Must(r => UserAdminGuards.AssignableRoles.Contains(r)).WithMessage("Only admin, maintainer and reviewer can be assigned.");
        RuleFor(x => x.Roles).Must(r => r.Distinct().Count() == r.Length).WithMessage("Roles must not repeat.");
    }
}
