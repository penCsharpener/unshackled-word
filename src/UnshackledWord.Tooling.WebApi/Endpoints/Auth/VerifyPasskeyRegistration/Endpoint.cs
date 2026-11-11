using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using Fido2NetLib;
using Fido2NetLib.Objects;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Auth.VerifyPasskeyRegistration;

/// <summary>Deserializes directly from the browser's <c>credential.toJSON()</c> output.</summary>
public sealed class VerifyPasskeyRegistrationRequest
{
    public AuthenticatorAttestationRawResponse Credential { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
}

/// <summary>Finishes the ceremony GetPasskeyRegisterOptions started and stores the new credential.</summary>
[RequiresLogin]
public sealed class Endpoint : Endpoint<VerifyPasskeyRegistrationRequest, VerifyPasskeyRegistrationResponse>
{
    private readonly IAuthRepository _repository;
    private readonly IFido2 _fido2;
    private readonly PasskeyCeremonyStore _ceremonies;
    private readonly ILogger<Endpoint> _logger;

    public Endpoint(IAuthRepository repository, IFido2 fido2, PasskeyCeremonyStore ceremonies, ILogger<Endpoint> logger)
    {
        _repository = repository;
        _fido2 = fido2;
        _ceremonies = ceremonies;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("passkeys/register/verify");
        Group<RouteGroupConfig>();
        Description(b => b.Produces<VerifyPasskeyRegistrationResponse>(200).Produces(400).Produces(404));
        Summary(s =>
        {
            s.Summary = "Finishes a passkey registration ceremony, storing the new credential.";
            s[404] = "No pending ceremony: never started, already consumed, or expired.";
            s[400] = "The attestation failed to verify.";
        });
    }

    public override async Task HandleAsync(VerifyPasskeyRegistrationRequest req, CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var originalOptions = _ceremonies.GetRegistration<CredentialCreateOptions>(userId);
        if (originalOptions is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        RegisteredPublicKeyCredential result;
        try
        {
            result = await _fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
            {
                AttestationResponse = req.Credential,
                OriginalOptions = originalOptions,
                IsCredentialIdUniqueToUserCallback = async (p, innerCt) =>
                    !await _repository.CredentialExistsAsync(p.CredentialId, innerCt),
            }, ct);
        }
        catch (Exception ex)
        {
            // The caller retries; no further diagnosis is handed out.
            _logger.LogWarning(ex, "Passkey registration verification failed for user {UserId}", userId);
            await Send.ResultAsync(TypedResults.BadRequest());
            return;
        }

        var inserted = await _repository.InsertPasskeyAsync(
            userId,
            result.Id,
            result.PublicKey,
            result.SignCount,
            result.Transports.Select(t => t.ToString().ToLowerInvariant()).ToArray(),
            result.AaGuid,
            req.Name,
            ct);

        _ceremonies.RemoveRegistration(userId);

        await Send.OkAsync(new VerifyPasskeyRegistrationResponse
        {
            Id = inserted.Id,
            Name = inserted.Name,
            CreatedAt = inserted.CreatedAt,
        }, ct);
    }
}

public sealed class VerifyPasskeyRegistrationValidator : Validator<VerifyPasskeyRegistrationRequest>
{
    public VerifyPasskeyRegistrationValidator()
    {
        RuleFor(x => x.Credential).NotNull();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}
