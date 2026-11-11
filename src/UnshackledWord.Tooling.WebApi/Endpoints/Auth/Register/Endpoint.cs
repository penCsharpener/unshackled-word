using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Auth.Register;

public sealed class Endpoint : Endpoint<RegisterRequest, AuthUserResponse>
{
    private static readonly TimeSpan VerificationTokenLifetime = TimeSpan.FromHours(24);

    private readonly IAuthRepository _repository;
    private readonly AdmissionCheck _admissionCheck;
    private readonly IPasswordHasher _passwordHasher;
    private readonly AuthEmailBuilder _emailBuilder;
    private readonly IEmailSender _emailSender;

    public Endpoint(
        IAuthRepository repository,
        AdmissionCheck admissionCheck,
        IPasswordHasher passwordHasher,
        AuthEmailBuilder emailBuilder,
        IEmailSender emailSender)
    {
        _repository = repository;
        _admissionCheck = admissionCheck;
        _passwordHasher = passwordHasher;
        _emailBuilder = emailBuilder;
        _emailSender = emailSender;
    }

    public override void Configure()
    {
        Post("register");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Throttle(hitLimit: 10, durationSeconds: 60, headerName: null);
        Description(b => b.Produces<AuthUserResponse>(200).ProducesProblem(400).Produces(409));
        Summary(s => s.Summary = "Registers a new account and emails a verification link. 409 if the email is taken; 400 for validation or admission refusal.");
    }

    public override async Task HandleAsync(RegisterRequest req, CancellationToken ct)
    {
        var email = req.Email.Trim();
        var emailNormalized = EmailNormalizer.Normalize(email);
        var displayName = req.DisplayName.Trim();
        var inviteCodeNormalized = string.IsNullOrWhiteSpace(req.InviteCode)
            ? null
            : InviteCodeNormalizer.Normalize(req.InviteCode);

        // Checked before anything is written or mailed: a refused address must leave no trace.
        var admission = await _admissionCheck.CheckAsync(emailNormalized, inviteCodeNormalized, ct);
        if (!admission.IsAdmitted)
        {
            if (admission.RefusalAttributedToInviteCode)
            {
                AddError(r => r.InviteCode!, admission.RefusalMessage!);
            }
            else
            {
                AddError(r => r.Email, admission.RefusalMessage!);
            }

            ThrowIfAnyErrors();
        }

        var userId = await _repository.CreateUserAsync(
            email,
            emailNormalized,
            _passwordHasher.Hash(req.Password),
            displayName,
            admission.ViaInviteCode ? inviteCodeNormalized : null,
            ct);

        if (userId is null)
        {
            await Send.ResultAsync(TypedResults.Conflict());
            return;
        }

        var token = await _repository.IssueTokenAsync(userId.Value, AuthTokenPurpose.EmailVerification, VerificationTokenLifetime, ct);
        await _emailSender.SendAsync(
            _emailBuilder.BuildVerification(email, displayName, token, AuthEmailBuilder.ResolveLocale(HttpContext)), ct);

        await Send.OkAsync(new AuthUserResponse { UserId = userId.Value, Email = email, DisplayName = displayName }, ct);
    }
}

public sealed class RegisterValidator : Validator<RegisterRequest>
{
    public RegisterValidator()
    {
        Include(new RegisterRequestValidator());
    }
}
