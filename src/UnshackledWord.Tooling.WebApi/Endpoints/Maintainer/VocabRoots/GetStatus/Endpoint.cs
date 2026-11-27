using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.SeedAdmin;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabRoots.GetStatus;

[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<VocabRootStatusResponse>
{
    private readonly SeedJobRunner _runner;
    private readonly VocabRootRepository _repository;

    public Endpoint(SeedJobRunner runner, VocabRootRepository repository)
    {
        _runner = runner;
        _repository = repository;
    }

    public override void Configure()
    {
        Get("vocab/roots/status");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Root counts of the active vocab entries (prefill, agent, manual, without root, not applicable) and the running or last prefill job; job is null when the last job was not a root prefill or none ran since the API started.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var job = _runner.Snapshot();

        await Send.OkAsync(new VocabRootStatusResponse
        {
            Job = job?.Operation == VocabRootStatusResponse.Operation ? job : null,
            Counts = await _repository.GetCountsAsync()
        }, ct);
    }
}
