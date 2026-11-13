using UnshackledWord.Infrastructure.Seed.Mappings;
using UnshackledWord.Infrastructure.Seed.Morphology;

namespace UnshackledWord.Infrastructure.Seed;

public enum SeedExportKind
{
    All,
    Morphology,
    Mappings
}

/// <summary>Runs the seed exporters of one kind into a layout; shared by the <c>export-seed</c> verb and the maintainer download.</summary>
public sealed class SeedExportRunner
{
    private readonly MorphologyExporter _morphologyExporter;
    private readonly MappingExporter _mappingExporter;

    public SeedExportRunner(MorphologyExporter morphologyExporter, MappingExporter mappingExporter)
    {
        _morphologyExporter = morphologyExporter;
        _mappingExporter = mappingExporter;
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
    }
}
