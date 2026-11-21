using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.SeedAdmin;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabGenerate.StartJob;

/// <summary>Runs <see cref="VocabLexiconGenerator"/> as a background job on the shared seed job runner (one seed or vocab job at a time).</summary>
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
        Post("vocab/generate");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Starts the vocab lexicon generation in the background (entries, keys, occurrences, ranks; idempotent) and returns the job with status 202; poll GET maintainer/vocab/status. 409 while another seed or vocab job runs.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var job = _runner.TryStart(VocabGenerateStatusResponse.Operation, VocabLexiconGenerator.Kind, async (progress, token) =>
        {
            // The job outlives this request, so it gets its own scope (and with it its own database connection).
            using var scope = _scopeFactory.CreateScope();
            var step = await scope.ServiceProvider.GetRequiredService<VocabLexiconGenerator>().GenerateAsync(progress, token);

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
