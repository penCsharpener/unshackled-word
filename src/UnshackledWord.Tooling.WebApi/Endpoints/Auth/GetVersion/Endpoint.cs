using System.Reflection;
using FastEndpoints;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Auth.GetVersion;

public sealed record VersionResponse(string Version);

/// <summary>Anonymous: the app version from Directory.Build.props (same number as the frontend).</summary>
public sealed class Endpoint : EndpointWithoutRequest<VersionResponse>
{
    private static readonly string Version = (typeof(Endpoint).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown").Split('+')[0];

    public override void Configure()
    {
        Get("version");
        Group<RouteGroupConfig>();
        AllowAnonymous();
        Throttle(hitLimit: 30, durationSeconds: 60, headerName: null);
        Summary(s => s.Summary = "Reports the app version.");
    }

    public override async Task HandleAsync(CancellationToken ct) => await Send.OkAsync(new VersionResponse(Version), ct);
}
