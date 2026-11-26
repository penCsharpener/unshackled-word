using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.Lxx;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.SeedAdmin;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.Lxx.GetStatus;

[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<LxxStatusResponse>
{
    private readonly SeedJobRunner _runner;
    private readonly ILxxNtFormRepository _repository;

    public Endpoint(SeedJobRunner runner, ILxxNtFormRepository repository)
    {
        _runner = runner;
        _repository = repository;
    }

    public override void Configure()
    {
        Get("lxx/status");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Counts for the LXX card: verses, words, tags per status and source, and the running or last prefill job of this process (poll this while it runs).");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var counts = await _repository.GetStatusCountsAsync(ct);
        var snapshot = _runner.Snapshot();
        var job = snapshot is { Operation: StartLxxPrefillResponse.Operation } ? snapshot : null;

        await Send.OkAsync(new LxxStatusResponse
        {
            Verses = counts.Verses,
            Words = counts.Words,
            Tags = counts.Tags.Select(t => new LxxTagCountResponse { Status = t.Status, Source = t.Source, Count = t.Count }).ToList(),
            Running = job?.Status == SeedJobResponse.Running,
            Job = job
        }, ct);
    }
}
