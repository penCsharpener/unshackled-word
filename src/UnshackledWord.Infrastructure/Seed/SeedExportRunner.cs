using UnshackledWord.Infrastructure.Seed.Mappings;
using UnshackledWord.Infrastructure.Seed.Morphology;
using UnshackledWord.Infrastructure.Seed.Revision;

namespace UnshackledWord.Infrastructure.Seed;

public enum SeedExportKind
{
    All,
    Morphology,
    Mappings,
    Revision
}

/// <summary>Runs the seed exporters of one kind into a layout; shared by the <c>export-seed</c> verb and the maintainer download.</summary>
public sealed class SeedExportRunner
{
    private readonly MorphologyExporter _morphologyExporter;
    private readonly MappingExporter _mappingExporter;
    private readonly RevisionExporter _revisionExporter;

    public SeedExportRunner(MorphologyExporter morphologyExporter, MappingExporter mappingExporter, RevisionExporter revisionExporter)
    {
        _morphologyExporter = morphologyExporter;
        _mappingExporter = mappingExporter;
        _revisionExporter = revisionExporter;
    }

    public async Task ExportAsync(SeedExportKind kind, SeedLayout layout, CancellationToken token = default)
    {
        if (kind is SeedExportKind.All or SeedExportKind.Morphology)
        {
            await _morphologyExporter.ExportAsync(layout, token);
        }

        if (kind is SeedExportKind.All or SeedExportKind.Mappings)
        {
            await _mappingExporter.ExportAsync(layout, token);
        }

        if (kind is SeedExportKind.All or SeedExportKind.Revision)
        {
            await _revisionExporter.ExportAsync(layout, token);
        }
    }
}
