using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.RateLimiting;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Auth.RequestPasswordReset;

[RateLimitGroup(RateLimitGroups.Auth)]
public sealed class Endpoint : Endpoint<EmailRequest>
{
    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromHours(1);

    private readonly IAuthRepository _repository;
    private readonly AuthEmailBuilder _emailBuilder;
    private readonly IEmailSender _emailSender;

    public Endpoint(IAuthRepository repository, AuthEmailBuilder emailBuilder, IEmailSender emailSender)
    {
        _repository = repository;
        _emailBuilder = emailBuilder;
        _emailSender = emailSender;
    }

    public override void Configure()
    {
        Post("request-password-reset");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Validator<RequestPasswordResetValidator>(); // the request type is shared with another endpoint
        // Every accepted call sends real mail: tighter than the sign-in endpoints.
        Throttle(hitLimit: 5, durationSeconds: 60, headerName: null);
        Description(b => b.Produces(202));
        Summary(s => s.Summary = "Emails a password-reset link if the address is registered. Always 202, whether or not it exists.");
    }

    public override async Task HandleAsync(EmailRequest req, CancellationToken ct)
    {
        var user = await _repository.FindByEmailAsync(EmailNormalizer.Normalize(req.Email), ct);

        // A deactivated account gets the same 202 as an unknown address and no mail.
        if (user is not null && !user.IsDeactivated)
        {
            var token = await _repository.IssueTokenAsync(user.Id, AuthTokenPurpose.PasswordReset, ResetTokenLifetime, supersedeOpen: true, ct);
            await _emailSender.SendAsync(
                _emailBuilder.BuildPasswordReset(user.Email, user.DisplayName, token, AuthEmailBuilder.ResolveLocale(HttpContext)), ct);
        }

        await Send.ResultAsync(TypedResults.Accepted((string?)null));
    }
}

public sealed class RequestPasswordResetValidator : Validator<EmailRequest>
{
    public RequestPasswordResetValidator()
    {
        Include(new EmailRequestValidator());
    }
}
