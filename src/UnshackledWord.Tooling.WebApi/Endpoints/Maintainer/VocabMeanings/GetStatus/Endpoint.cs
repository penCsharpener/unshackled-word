using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.SeedAdmin;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabMeanings.GetStatus;

[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<VocabMeaningsStatusResponse>
{
    private readonly SeedJobRunner _runner;

    public Endpoint(SeedJobRunner runner)
    {
        _runner = runner;
    }

    public override void Configure()
    {
        Get("vocab/meanings/status");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "The running or last vocab meaning derivation (log, result with counts and the entries whose top meaning changed); job is null when the last job was not a derivation or none ran since the API started.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var job = _runner.Snapshot();

        await Send.OkAsync(new VocabMeaningsStatusResponse { Job = job?.Operation == VocabMeaningsStatusResponse.Operation ? job : null }, ct);
    }
}
