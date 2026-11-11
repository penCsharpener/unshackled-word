using System.Text;
using FastEndpoints;
using FluentValidation;
using Fido2NetLib;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Auth.PasskeyLogin;

public sealed class PasskeyLoginRequest
{
    public string FlowId { get; set; } = string.Empty;
    public AuthenticatorAssertionRawResponse Credential { get; set; } = null!;
}

/// <summary>Finishes the ceremony GetPasskeyLoginOptions started and signs the caller in.</summary>
public sealed class Endpoint : Endpoint<PasskeyLoginRequest, AuthUserResponse>
{
    private readonly IAuthRepository _repository;
    private readonly IFido2 _fido2;
    private readonly PasskeyCeremonyStore _ceremonies;
    private readonly SessionSignIn _sessionSignIn;
    private readonly ILogger<Endpoint> _logger;

    public Endpoint(
        IAuthRepository repository,
        IFido2 fido2,
        PasskeyCeremonyStore ceremonies,
        SessionSignIn sessionSignIn,
        ILogger<Endpoint> logger)
    {
        _repository = repository;
        _fido2 = fido2;
        _ceremonies = ceremonies;
        _sessionSignIn = sessionSignIn;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("passkeys/login/verify");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Throttle(hitLimit: 20, durationSeconds: 60, headerName: null);
        Description(b => b.Produces<AuthUserResponse>(200).Produces(401).Produces(404));
        Summary(s =>
        {
            s.Summary = "Finishes a passkey sign-in ceremony and signs in.";
            s[404] = "No pending ceremony for this flow id: never started, already consumed, or expired.";
            s.Description = "Returns 401 for no such credential, an unverified email, or an assertion that failed to verify: always the same generic response.";
        });
    }

    public override async Task HandleAsync(PasskeyLoginRequest req, CancellationToken ct)
    {
        var originalOptions = _ceremonies.GetLogin<AssertionOptions>(req.FlowId);
        if (originalOptions is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var user = await _repository.FindByCredentialIdAsync(req.Credential.RawId, ct);

        if (user is null || user.EmailVerifiedAt is null)
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        try
        {
            var result = await _fido2.MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = req.Credential,
                OriginalOptions = originalOptions,
                StoredPublicKey = user.PublicKey,
                StoredSignatureCounter = (uint)user.StoredSignCount,
                IsUserHandleOwnerOfCredentialIdCallback = (p, _) => Task.FromResult(
                    p.UserHandle is not null
                    && Encoding.UTF8.GetBytes(user.UserId.ToString()).SequenceEqual(p.UserHandle)),
            }, ct);

            await _repository.UpdatePasskeyAfterUseAsync(user.PasskeyId, result.SignCount, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Passkey login verification failed for credential owned by user {UserId}", user.UserId);
            await Send.UnauthorizedAsync(ct);
            return;
        }

        _ceremonies.RemoveLogin(req.FlowId);

        await _sessionSignIn.SignInAsync(HttpContext, user.UserId, user.Email, user.DisplayName, user.SecurityStamp, user.Roles);
        await Send.OkAsync(new AuthUserResponse { UserId = user.UserId, Email = user.Email, DisplayName = user.DisplayName }, ct);
    }
}

public sealed class PasskeyLoginValidator : Validator<PasskeyLoginRequest>
{
    public PasskeyLoginValidator()
    {
        RuleFor(x => x.FlowId).NotEmpty();
        RuleFor(x => x.Credential).NotNull();
    }
}
