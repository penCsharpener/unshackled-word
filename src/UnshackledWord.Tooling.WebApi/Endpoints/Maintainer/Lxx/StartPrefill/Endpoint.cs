using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Application.Features.Lxx;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.SeedAdmin;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.Lxx.StartPrefill;

/// <summary>Runs <see cref="LxxNtFormTagger"/> (roadmap 0028.6, pass 1) as a background job on the shared seed job runner.</summary>
[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<StartLxxPrefillResponse>
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
        Post("lxx/prefill");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Starts the LXX pass 1 (NT forms with exactly one analysis become proposals, source nt-form, no user) in the background and returns the job with status 202; the log lists coverage per book. Never touches confirmed, ai or human rows; a second run changes nothing. 409 while another seed or LXX job runs. Poll GET maintainer/lxx/status.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var job = _runner.TryStart(StartLxxPrefillResponse.Operation, "all", async (progress, token) =>
        {
            // The job outlives this request, so it gets its own scope (and with it its own database connections).
            using var scope = _scopeFactory.CreateScope();
            var tagger = scope.ServiceProvider.GetRequiredService<LxxNtFormTagger>();
            await tagger.RunAsync(progress, token);

            return new SeedJobResult();
        });

        if (job is null)
        {
            AddError("Another seed or LXX job is already running.");
            await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);

            return;
        }

        await Send.ResponseAsync(new StartLxxPrefillResponse { Job = job }, StatusCodes.Status202Accepted, ct);
    }
}
