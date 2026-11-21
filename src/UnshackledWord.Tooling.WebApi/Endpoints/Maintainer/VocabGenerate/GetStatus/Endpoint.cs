using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.SeedAdmin;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabGenerate.GetStatus;

[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<VocabGenerateStatusResponse>
{
    private readonly SeedJobRunner _runner;

    public Endpoint(SeedJobRunner runner)
    {
        _runner = runner;
    }

    public override void Configure()
    {
        Get("vocab/status");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "The running or last vocab lexicon generation (log, result with counts); job is null when the last job was not a vocab job or none ran since the API started.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var job = _runner.Snapshot();

        await Send.OkAsync(new VocabGenerateStatusResponse { Job = job?.Operation == VocabGenerateStatusResponse.Operation ? job : null }, ct);
    }
}
