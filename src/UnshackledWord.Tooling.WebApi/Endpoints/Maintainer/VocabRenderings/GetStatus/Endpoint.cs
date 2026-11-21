using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.SeedAdmin;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabRenderings.GetStatus;

[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<VocabRenderingsStatusResponse>
{
    private readonly SeedJobRunner _runner;
    private readonly VocabEntryRepository _repository;

    public Endpoint(SeedJobRunner runner, VocabEntryRepository repository)
    {
        _runner = runner;
        _repository = repository;
    }

    public override void Configure()
    {
        Get("vocab/renderings/status");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "The last refresh time of StatsVocabRenderings and the running or last refresh job; job is null when the last job was not a renderings refresh or none ran since the API started.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var job = _runner.Snapshot();

        await Send.OkAsync(new VocabRenderingsStatusResponse
        {
            Job = job?.Operation == VocabRenderingsStatusResponse.Operation ? job : null,
            RefreshedAt = await _repository.GetRenderingsRefreshedAtAsync()
        }, ct);
    }
}
