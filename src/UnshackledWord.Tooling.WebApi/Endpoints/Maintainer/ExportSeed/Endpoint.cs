using System.Diagnostics;
using FluentValidation;
using FastEndpoints;
using UnshackledWord.Application.Features.Auth;
using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Seed;
using UnshackledWord.Tooling.WebApi.Auth;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.ExportSeed;

[RequiresLogin]
public sealed class Endpoint : Endpoint<ExportSeedRequest>
{
    private readonly SeedExportCoordinator _coordinator;
    private readonly SeedZipBuilder _builder;
    private readonly ILogger<Endpoint> _logger;

    public Endpoint(SeedExportCoordinator coordinator, SeedZipBuilder builder, ILogger<Endpoint> logger)
    {
        _coordinator = coordinator;
        _builder = builder;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("exportSeed");
        Group<RouteGroupConfig>();
        Roles(AppRoles.Admin, AppRoles.Maintainer);
        Summary(s => s.Summary = "Builds the repo seed (kind all|morphology|mappings|revision|vocab) from the database, read-only, and returns it as a zip of repo-relative paths (assets/seed/..., assets/Elb1871Mappings/..., assets/seed/revision/...). 409 while another export runs.");
    }

    public override async Task HandleAsync(ExportSeedRequest req, CancellationToken ct)
    {
        var kind = ParseKind(req.Kind);
        var unavailable = _builder.CheckAvailable(kind);

        if (unavailable is not null)
        {
            AddError(unavailable);
            await Send.ErrorsAsync(StatusCodes.Status422UnprocessableEntity, ct);

            return;
        }

        if (!_coordinator.TryEnter())
        {
            AddError("A seed export is already running.");
            await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);

            return;
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();
            await using var zip = await _builder.BuildAsync(kind, ct);
            var length = zip.Length;
            var fileName = $"seed-export-{req.Kind.ToLowerInvariant()}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";

            await Send.StreamAsync(zip, fileName, length, "application/zip", cancellation: ct);
            _logger.LogInformation("Seed export {Kind} sent as {FileName} ({Bytes} bytes) after {ElapsedMs} ms", kind, fileName, length, stopwatch.ElapsedMilliseconds);
        }
        finally
        {
            _coordinator.Exit();
        }
    }

    private static SeedExportKind ParseKind(string kind) => kind.ToLowerInvariant() switch
    {
        ExportSeedRequest.Morphology => SeedExportKind.Morphology,
        ExportSeedRequest.Mappings => SeedExportKind.Mappings,
        ExportSeedRequest.Revision => SeedExportKind.Revision,
        ExportSeedRequest.Vocab => SeedExportKind.Vocab,
        _ => SeedExportKind.All
    };
}

public sealed class ExportSeedValidator : Validator<ExportSeedRequest>
{
    public ExportSeedValidator()
    {
        RuleFor(x => x.Kind)
            .Must(k => k is not null && k.ToLowerInvariant() is ExportSeedRequest.All or ExportSeedRequest.Morphology or ExportSeedRequest.Mappings or ExportSeedRequest.Revision or ExportSeedRequest.Vocab)
            .WithMessage("kind must be all, morphology, mappings or revision.");
    }
}
