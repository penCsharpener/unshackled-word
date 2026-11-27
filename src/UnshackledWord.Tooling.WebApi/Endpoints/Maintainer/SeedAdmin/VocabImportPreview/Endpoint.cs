using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Seed.Vocab;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.SeedAdmin.VocabImportPreview;

[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<VocabSeedImportPreviewResponse>
{
    private readonly SeedOperationService _service;

    public Endpoint(SeedOperationService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("vocab/seed/import/preview");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Dry run of the root part of the vocab import (roadmap 0030.7f): `rootsNew` / `rootsChanged` count roots and word rows, `manualKept` the local manual word rows that stay. Runs in a transaction that is rolled back. 422 when the assets folder is not reachable.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var unavailable = _service.CheckAvailable();

        if (unavailable is not null)
        {
            AddError(unavailable);
            await Send.ErrorsAsync(StatusCodes.Status422UnprocessableEntity, ct);

            return;
        }

        var preview = await _service.PreviewVocabRootsAsync(ct);
        var (counts, fileRoots, fileWordRoots) = preview ?? (new VocabRootSeedCounts(), 0, 0);

        await Send.OkAsync(new VocabSeedImportPreviewResponse
        {
            RootsNew = counts.RootsNew + counts.WordRootsNew,
            RootsChanged = counts.RootsChanged + counts.WordRootsChanged,
            ManualKept = counts.ManualKept,
            Skipped = counts.Skipped,
            FileRoots = fileRoots,
            FileWordRoots = fileWordRoots
        }, ct);
    }
}
