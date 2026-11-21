using System.Security.Claims;
using FastEndpoints;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.SeedAdmin;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.VocabMeanings.StartJob;

/// <summary>Runs <see cref="VocabMeaningDeriver"/> as a background job on the shared seed job runner (one seed or vocab job at a time).</summary>
[RequiresLogin]
public sealed class Endpoint : Endpoint<DeriveVocabMeaningsRequest, SeedJobResponse>
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
        Post("vocab/meanings/derive");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Derives the vocab meanings from the live renderings in the background (scope: all, a course, or a rank range within a course; dryRun writes nothing) and returns the job with status 202; poll GET maintainer/vocab/meanings/status. 409 while another seed or vocab job runs.");
    }

    public override async Task HandleAsync(DeriveVocabMeaningsRequest req, CancellationToken ct)
    {
        if (req.Course is not null)
        {
            using var check = _scopeFactory.CreateScope();
            if (!await check.ServiceProvider.GetRequiredService<VocabMeaningDeriver>().CourseExistsAsync(req.Course, ct))
            {
                AddError(r => r.Course, "No vocab course with this code.");
                await Send.ErrorsAsync(StatusCodes.Status404NotFound, ct);

                return;
            }
        }

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var scope = new VocabMeaningScope(req.Course, req.RankFrom, req.RankTo);
        var dryRun = req.DryRun;

        var job = _runner.TryStart(VocabMeaningsStatusResponse.Operation, VocabMeaningDeriver.Kind, async (progress, token) =>
        {
            // The job outlives this request, so it gets its own scope (and with it its own database connection).
            using var scope2 = _scopeFactory.CreateScope();
            var step = await scope2.ServiceProvider.GetRequiredService<VocabMeaningDeriver>().DeriveAsync(scope, dryRun, userId, progress, token);

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

public sealed class Validator : Validator<DeriveVocabMeaningsRequest>
{
    public Validator()
    {
        RuleFor(x => x.Course).MaximumLength(30);
        RuleFor(x => x.RankFrom).GreaterThanOrEqualTo(1).When(x => x.RankFrom is not null);
        RuleFor(x => x.RankTo).GreaterThanOrEqualTo(1).When(x => x.RankTo is not null);
        RuleFor(x => x.RankTo).GreaterThanOrEqualTo(x => x.RankFrom).When(x => x.RankFrom is not null && x.RankTo is not null);
        RuleFor(x => x.Course).NotEmpty().When(x => x.RankFrom is not null || x.RankTo is not null).WithMessage("A rank range needs a course.");
    }
}
