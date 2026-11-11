using FastEndpoints;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Auth.RegistrationPolicy;

/// <summary>Anonymous and read-only: tells the frontend whether to show the invite-code field. Never reveals the allowlist.</summary>
public sealed class Endpoint : EndpointWithoutRequest<RegistrationPolicyResponse>
{
    private readonly RegistrationOptions _options;

    public Endpoint(IOptions<RegistrationOptions> options)
    {
        _options = options.Value;
    }

    public override void Configure()
    {
        Get("registration-policy");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Throttle(hitLimit: 30, durationSeconds: 60, headerName: null);
        Summary(s => s.Summary = "Reports whether registration is open or invite-only.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var restricted = _options.Mode == RegistrationMode.Restricted;
        await Send.OkAsync(new RegistrationPolicyResponse
        {
            Mode = restricted ? "restricted" : "open",
            InviteCodeRequired = restricted,
        }, ct);
    }
}
