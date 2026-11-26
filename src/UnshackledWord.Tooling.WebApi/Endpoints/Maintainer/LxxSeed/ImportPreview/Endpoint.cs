using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Seed.Lxx;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.LxxSeed.ImportPreview;

[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<LxxSeedImportPreviewResponse>
{
    private readonly LxxSeedService _service;

    public Endpoint(LxxSeedService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("lxx/seed/import/preview");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Dry run of the LXX seed import with \"Exakt wie Datei\" (roadmap 0028.8): `wouldDeleteTags` / `wouldDeleteLinks` = confirmed rows the files in `assets/seed/lxx` do not have; `fileTagRows` / `fileLinkRows` = lines of the files (omitted without files). Runs the import in a transaction that is rolled back, so nothing is written. 422 when the assets folder is not reachable.");
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

        var (tags, links, fileTags, fileLinks) = await _service.PreviewAsync(null, ct);

        await Send.OkAsync(new LxxSeedImportPreviewResponse { WouldDeleteTags = tags, WouldDeleteLinks = links, FileTagRows = fileTags, FileLinkRows = fileLinks }, ct);
    }
}
