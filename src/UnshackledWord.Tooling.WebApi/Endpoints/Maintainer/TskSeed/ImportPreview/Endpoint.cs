using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Seed.Tsk;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.TskSeed.ImportPreview;

[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<TskCurationImportPreviewResponse>
{
    private readonly TskCurationSeedService _service;

    public Endpoint(TskCurationSeedService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("tsk/import/preview");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Dry run of the TSK curation import with \"Exakt wie Datei\" (roadmap 0024.7): `wouldDelete` = TskCurations rows whose key `assets/seed/tsk/curations.csv` does not have; `fileRows` = lines of the file (omitted without a file). Writes nothing. 422 when the assets folder is not reachable.");
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

        var (wouldDelete, fileRows) = await _service.PreviewAsync(null, ct);

        await Send.OkAsync(new TskCurationImportPreviewResponse { WouldDelete = wouldDelete, FileRows = fileRows }, ct);
    }
}
