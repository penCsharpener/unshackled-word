using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Seed;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.SeedAdmin.GetStatus;

[RequiresLogin]
public sealed class Endpoint : EndpointWithoutRequest<SeedStatusResponse>
{
    private const int ImportRows = 20;

    private readonly SeedJobRunner _runner;
    private readonly SeedFileExportService _files;
    private readonly SeedImportsRepository _imports;

    public Endpoint(SeedJobRunner runner, SeedFileExportService files, SeedImportsRepository imports)
    {
        _runner = runner;
        _files = files;
        _imports = imports;
    }

    public override void Configure()
    {
        Get("seed/status");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "The running or last seed job (log and result), whether the repo seed folder is reachable, and the latest SeedImports rows.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var rows = await _imports.ReadLatestAsync(ImportRows);

        await Send.OkAsync(new SeedStatusResponse
        {
            Job = _runner.Snapshot(),
            AssetsProblem = _files.CheckAssetsAvailable(),
            Imports = rows.Select(r => new SeedImportRowResponse
            {
                Id = r.Id,
                Kind = r.Kind,
                SeedVersion = r.SeedVersion,
                ExportedAt = r.ExportedAt,
                ImportedAt = r.ImportedAt,
                Rows = r.Rows,
                Exact = r.Exact,
                Shifted = r.Shifted,
                Modified = r.Modified,
                Orphaned = r.Orphaned,
                VersesUpdated = r.VersesUpdated,
                VersesKeptLocal = r.VersesKeptLocal
            }).ToList()
        }, ct);
    }
}
