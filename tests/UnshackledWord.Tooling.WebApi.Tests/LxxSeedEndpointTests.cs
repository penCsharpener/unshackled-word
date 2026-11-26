using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Seed;
using UnshackledWord.Infrastructure.Seed.Lxx;
using UnshackledWord.Infrastructure.Services;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.ExportSeed;
using UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.SeedAdmin;
using ExportEndpoint = UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.LxxSeed.Export.Endpoint;
using ImportEndpoint = UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.LxxSeed.Import.Endpoint;
using PreviewEndpoint = UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.LxxSeed.ImportPreview.Endpoint;

namespace UnshackledWord.Tooling.WebApi.Tests;

/// <summary>Roadmap 0028.8: the three LXX seed endpoints are Admin/Maintainer only; 202 with job, 409 while busy, 422 without an assets folder.</summary>
public class LxxSeedEndpointTests : IDisposable
{
    private readonly SeedJobRunner _runner = new(new SeedExportCoordinator(), TimeProvider.System, NullLogger<SeedJobRunner>.Instance);
    private readonly string _assets = Path.Combine(Path.GetTempPath(), "lxx-seed-endpoint-" + Guid.NewGuid().ToString("N"));

    public LxxSeedEndpointTests()
    {
        Directory.CreateDirectory(_assets);
    }

    public void Dispose() => Directory.Delete(_assets, true);

    private LxxSeedService Service(string? assetsPath)
    {
        var fileService = new FileService();
        var settings = new DatabaseSeedSettings { SolutionAssetsPath = assetsPath ?? string.Empty, SolutionTempPath = _assets };

        return new LxxSeedService(Substitute.For<IDbConnectionFactory>(), fileService, new SeedManifestStore(fileService),
            Options.Create(new AppSettings { DatabaseSeeding = settings }), NullLogger<LxxSeedService>.Instance);
    }

    private static IServiceScopeFactory Scopes(LxxSeedService service) =>
        new ServiceCollection().AddSingleton(service).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

    [Fact]
    public void AllEndpoints_RequireLogin_AndOnlyAdminAndMaintainerPass()
    {
        var service = Service(_assets);
        var endpoints = new BaseEndpoint[]
        {
            Factory.Create<ExportEndpoint>(_runner, Scopes(service)),
            Factory.Create<ImportEndpoint>(_runner, Scopes(service)),
            Factory.Create<PreviewEndpoint>(service),
        };

        foreach (var endpoint in endpoints)
        {
            endpoint.GetType().GetCustomAttributes(inherit: true).Select(a => a.GetType().Name).ShouldContain("RequiresLoginAttribute", endpoint.GetType().FullName);
            endpoint.Definition.AllowedRoles.ShouldNotBeNull(endpoint.GetType().FullName);
            endpoint.Definition.AllowedRoles!.OrderBy(r => r).ShouldBe(new[] { AppRoles.Admin, AppRoles.Maintainer }.OrderBy(r => r), endpoint.GetType().FullName);
        }
    }

    [Fact]
    public async Task Export_Answers202WithTheLxxJob()
    {
        var endpoint = Factory.Create<ExportEndpoint>(_runner, Scopes(Service(_assets)));

        await endpoint.HandleAsync(CancellationToken.None);

        endpoint.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status202Accepted);
        (endpoint.Response.Operation, endpoint.Response.Kind).ShouldBe(("lxxSeedExport", "lxx"));
    }

    [Fact]
    public async Task Import_Answers202WithTheLxxJob_AndMirrorIsPassedOn()
    {
        var endpoint = Factory.Create<ImportEndpoint>(_runner, Scopes(Service(_assets)));

        await endpoint.HandleAsync(new ImportLxxSeedRequest { Mirror = true }, CancellationToken.None);

        endpoint.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status202Accepted);
        (endpoint.Response.Operation, endpoint.Response.Kind).ShouldBe(("lxxSeedImport", "lxx"));
    }

    [Fact]
    public async Task ExportAndImport_WhileAJobRuns_Answer409()
    {
        var gate = new TaskCompletionSource();
        _runner.TryStart("other", "all", async (_, _) => { await gate.Task; return new SeedJobResult(); }).ShouldNotBeNull();
        var scopes = Scopes(Service(_assets));

        var export = Factory.Create<ExportEndpoint>(_runner, scopes);
        await export.HandleAsync(CancellationToken.None);
        var import = Factory.Create<ImportEndpoint>(_runner, scopes);
        await import.HandleAsync(new ImportLxxSeedRequest(), CancellationToken.None);

        export.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        import.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status409Conflict);
        gate.SetResult();
    }

    [Fact]
    public async Task AllEndpoints_WithoutAssetsFolder_Answer422()
    {
        var service = Service(Path.Combine(_assets, "missing"));
        var scopes = Scopes(service);

        var export = Factory.Create<ExportEndpoint>(_runner, scopes);
        await export.HandleAsync(CancellationToken.None);
        var import = Factory.Create<ImportEndpoint>(_runner, scopes);
        await import.HandleAsync(new ImportLxxSeedRequest(), CancellationToken.None);
        var preview = Factory.Create<PreviewEndpoint>(service);
        await preview.HandleAsync(CancellationToken.None);

        export.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status422UnprocessableEntity);
        import.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status422UnprocessableEntity);
        preview.HttpContext.Response.StatusCode.ShouldBe(StatusCodes.Status422UnprocessableEntity);
    }

    [Fact]
    public async Task Preview_WithoutFiles_AnswersZerosAndNoFileRows()
    {
        var preview = Factory.Create<PreviewEndpoint>(Service(_assets));

        await preview.HandleAsync(CancellationToken.None);

        preview.Response.WouldDeleteTags.ShouldBe(0);
        preview.Response.WouldDeleteLinks.ShouldBe(0);
        preview.Response.FileTagRows.ShouldBeNull();
        preview.Response.FileLinkRows.ShouldBeNull();
    }
}
