using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.SeedAdmin;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabRenderings.StartJob;

/// <summary>Runs <see cref="VocabRenderingsRefresher"/> (about 30 s) as a background job on the shared seed job runner (one seed or vocab job at a time).</summary>
[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<SeedJobResponse>
{
    private readonly SeedJobRunner _runner;
    private readonly IServiceScopeFactory _scopeFactory;

    public Endpoint(SeedJobRunner runner, IServiceScopeFactory scopeFactory)
    {
        _runner = runner;
        _scopeFactory = scopeFactory;
    }

    public override void Configure()
    {
        Post("vocab/renderings/refresh");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Refreshes StatsVocabRenderings (German renderings per Strong's key over the live text) in the background and returns the job with status 202; poll GET maintainer/vocab/renderings/status. 409 while another seed or vocab job runs.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var job = _runner.TryStart(VocabRenderingsStatusResponse.Operation, VocabRenderingsRefresher.Kind, async (progress, token) =>
        {
            // The job outlives this request, so it gets its own scope (and with it its own database connection).
            using var scope = _scopeFactory.CreateScope();
            var step = await scope.ServiceProvider.GetRequiredService<VocabRenderingsRefresher>().RefreshAsync(progress, token);

            return new SeedJobResult { Steps = [step] };
        });

        if (job is null)
        {
            AddError("Another seed or vocab job is already running.");
            await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);

            return;
        }

        await Send.ResponseAsync(job, StatusCodes.Status202Accepted, ct);
    }
}
