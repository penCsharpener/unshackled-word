using FastEndpoints;
using Fido2NetLib;
using Fido2NetLib.Objects;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Auth.GetPasskeyLoginOptions;

/// <summary>Wraps the raw <see cref="AssertionOptions"/> with the opaque flow id PasskeyLogin must hand back.</summary>
public sealed class GetPasskeyLoginOptionsResponse
{
    public string FlowId { get; set; } = string.Empty;
    public AssertionOptions Options { get; set; } = null!;
}

/// <summary>Starts a passkey sign-in ceremony. Anonymous: this is how a signed-out visitor authenticates.</summary>
public sealed class Endpoint : Endpoint<GetPasskeyLoginOptionsRequest, GetPasskeyLoginOptionsResponse>
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
        Post("passkeys/login/options");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        // Each call stores a pending ceremony: throttled so an anonymous caller cannot fill the cache.
        Throttle(hitLimit: 20, durationSeconds: 60, headerName: null);
        Summary(s => s.Summary = "Starts a passkey sign-in ceremony, returning WebAuthn assertion options.");
    }

    public override async Task HandleAsync(GetPasskeyLoginOptionsRequest req, CancellationToken ct)
    {
        // Empty for an unknown address and for one without passkeys alike: the response never reveals which.
        var credentialIds = string.IsNullOrEmpty(req.Email)
            ? []
            : await _repository.GetCredentialIdsByEmailAsync(EmailNormalizer.Normalize(req.Email), ct);

        var options = _fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = credentialIds.Select(id => new PublicKeyCredentialDescriptor(id)).ToArray(),
            UserVerification = UserVerificationRequirement.Preferred,
        });

        var flowId = Guid.NewGuid().ToString("N");
        _ceremonies.SetLogin(flowId, options);

        await Send.OkAsync(new GetPasskeyLoginOptionsResponse { FlowId = flowId, Options = options }, ct);
    }
}

public sealed class GetPasskeyLoginOptionsValidator : Validator<GetPasskeyLoginOptionsRequest>
{
    public GetPasskeyLoginOptionsValidator()
    {
        Include(new GetPasskeyLoginOptionsRequestValidator());
    }
}
