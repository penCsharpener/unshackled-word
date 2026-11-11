using System.Security.Claims;
using System.Text;
using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using Fido2NetLib;
using Fido2NetLib.Objects;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Auth.GetPasskeyRegisterOptions;

/// <summary>
/// Starts a "register a passkey on my account" ceremony. The response body is a <see cref="CredentialCreateOptions"/>,
/// which already serializes to the JSON shape the browser's parseCreationOptionsFromJSON expects.
/// </summary>
[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<CredentialCreateOptions>
{
    private readonly IAuthRepository _repository;
    private readonly IFido2 _fido2;
    private readonly PasskeyCeremonyStore _ceremonies;

    public Endpoint(IAuthRepository repository, IFido2 fido2, PasskeyCeremonyStore ceremonies)
    {
        _repository = repository;
        _fido2 = fido2;
        _ceremonies = ceremonies;
    }

    public override void Configure()
    {
        Post("passkeys/register/options");
        Group<RouteGroupConfig>();
        Summary(s => s.Summary = "Starts a ceremony to register a new passkey on the caller's own account.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var existingCredentialIds = await _repository.GetCredentialIdsForUserAsync(userId, ct);

        var options = _fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = new Fido2User
            {
                // The WebAuthn "user handle", echoed back on every future assertion.
                Id = Encoding.UTF8.GetBytes(userId.ToString()),
                Name = User.FindFirstValue(ClaimTypes.Email)!,
                DisplayName = User.FindFirstValue(AppClaimTypes.DisplayName)!,
            },
            ExcludeCredentials = existingCredentialIds.Select(id => new PublicKeyCredentialDescriptor(id)).ToArray(),
            AuthenticatorSelection = new AuthenticatorSelection
            {
                ResidentKey = ResidentKeyRequirement.Preferred,
                UserVerification = UserVerificationRequirement.Preferred,
            },
            AttestationPreference = AttestationConveyancePreference.None,
        });

        _ceremonies.SetRegistration(userId, options);
        await Send.OkAsync(options, ct);
    }
}
