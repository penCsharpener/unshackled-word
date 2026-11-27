using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.SeedAdmin;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabRoots.PrefillJob;

/// <summary>Runs <see cref="VocabRootPrefillService"/> as a background job on the shared seed job runner (one seed or vocab job at a time).</summary>
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
        Post("vocab/roots/prefill");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Starts the deterministic root prefill in the background (Greek compounds from the Abbott-Smith split, Hebrew/Aramaic verb roots; writes only keys without a root or with origin prefill; idempotent) and returns the job with status 202; poll GET maintainer/vocab/roots/status. 409 while another seed or vocab job runs.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var job = _runner.TryStart(VocabRootStatusResponse.Operation, VocabRootPrefillService.Kind, async (progress, token) =>
        {
            // The job outlives this request, so it gets its own scope (and with it its own database connection).
            using var scope = _scopeFactory.CreateScope();
            var step = await scope.ServiceProvider.GetRequiredService<VocabRootPrefillService>().PrefillAsync(progress, token);

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
