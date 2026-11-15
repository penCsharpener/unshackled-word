using UnshackledWord.Domain.WebApi.Maintainer;
using UnshackledWord.Infrastructure.Seed;
using UnshackledWord.Infrastructure.Seed.Mappings;
using UnshackledWord.Infrastructure.Seed.Morphology;
using UnshackledWord.Infrastructure.Seed.Revision;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.SeedAdmin;

/// <summary>
/// The seed round trip of the maintainer page, step by step, on the same importers, exporters and rebase the SeedDb CLI uses:
/// export to <c>assets/seed</c>, import into empty tables, update that keeps locally changed variants/verses, rebase, and the
/// round-trip check (export twice, compare with each other and with the files on disk). Never downloads anything; STEP text is not
/// part of any seed file.
/// </summary>
public sealed class SeedOperationService
{
    private readonly SeedFileExportService _files;
    private readonly MorphologySeedImporter _morphology;
    private readonly MappingSeedImporter _mappings;
    private readonly RevisionSeedImporter _revision;
    private readonly SeedRebaseService _rebase;
    private readonly ILogger<SeedOperationService> _logger;

    public SeedOperationService(
        SeedFileExportService files,
        MorphologySeedImporter morphology,
        MappingSeedImporter mappings,
        RevisionSeedImporter revision,
        SeedRebaseService rebase,
        ILogger<SeedOperationService> logger)
    {
        _files = files;
        _morphology = morphology;
        _mappings = mappings;
        _revision = revision;
        _rebase = rebase;
        _logger = logger;
    }

    public static SeedExportKind ParseKind(string? kind) => kind?.ToLowerInvariant() switch
    {
        ExportSeedRequest.Morphology => SeedExportKind.Morphology,
        ExportSeedRequest.Mappings => SeedExportKind.Mappings,
        ExportSeedRequest.Revision => SeedExportKind.Revision,
        _ => SeedExportKind.All
    };

    /// <summary>Why the operation cannot start here (assets folder not reachable), or null.</summary>
    public string? CheckAvailable() => _files.CheckAssetsAvailable();

    public async Task<SeedJobResult> RunAsync(string operation, SeedExportKind kind, bool confirmBackup, IProgress<string> progress, CancellationToken token)
    {
        var unavailable = CheckAvailable();

        if (unavailable is not null)
        {
            throw new InvalidOperationException(unavailable);
        }

        var result = new SeedJobResult();

        switch (operation)
        {
            case SeedOperations.Export:
                result.Export = await _files.ExportToDiskAsync(kind, progress, token);
                break;
            case SeedOperations.Import:
                result.Steps = await ImportAsync(kind, progress, token);
                break;
            case SeedOperations.Update:
                result.Steps = await UpdateAsync(kind, progress, token);
                break;
            case SeedOperations.Rebase:
                result.Rebase = await _rebase.RebaseAsync(_files.CreateRepoLayout(), confirmBackup, progress, token);
                result.Steps = result.Rebase.Steps;
                break;
            case SeedOperations.RoundTrip:
                result.RoundTrip = await _files.RoundTripCheckAsync(kind, progress, token);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown seed operation.");
        }

        _logger.LogInformation("Seed operation {Operation} {Kind} finished", operation, kind);

        return result;
    }

    private async Task<List<SeedStepReport>> ImportAsync(SeedExportKind kind, IProgress<string> progress, CancellationToken token)
    {
        var layout = _files.CreateRepoLayout();
        var steps = new List<SeedStepReport>();

        if (kind is SeedExportKind.All or SeedExportKind.Morphology)
        {
            progress.Report("Importing the morphology seed (only into an empty table)");
            steps.Add(await _morphology.RunAsync(layout, token));
        }

        if (kind is SeedExportKind.All or SeedExportKind.Mappings)
        {
            progress.Report("Importing the mapping seed (only into empty tables)");
            steps.Add(await _mappings.ImportFromLayoutAsync(layout, token));
        }

        if (kind is SeedExportKind.All or SeedExportKind.Revision)
        {
            progress.Report("Importing the revision seed (only into an empty RevisionVerses)");
            steps.Add(await _revision.ImportFromLayoutAsync(layout, token));
        }

        return steps;
    }

    private async Task<List<SeedStepReport>> UpdateAsync(SeedExportKind kind, IProgress<string> progress, CancellationToken token)
    {
        var layout = _files.CreateRepoLayout();
        var steps = new List<SeedStepReport>();

        if (kind is SeedExportKind.All or SeedExportKind.Morphology)
        {
            // The morphology seed is never updated after the first import (roadmap 0010).
            steps.Add(SeedStepReport.Skip(MorphologySeedImporter.Kind, "update", SeedSkipReason.UpdateUnsupported));
        }

        if (kind is SeedExportKind.All or SeedExportKind.Mappings)
        {
            progress.Report("Updating mappings: verses nobody changed since the last import follow the seed, the others are kept");
            steps.Add(await _mappings.UpdateAsync(layout, token));
        }

        if (kind is SeedExportKind.All or SeedExportKind.Revision)
        {
            progress.Report("Updating the revision: variants nobody changed since the last import follow the seed, the others are kept");
            steps.Add(await _revision.UpdateFromLayoutAsync(layout, token));
        }

        return steps;
    }
}
