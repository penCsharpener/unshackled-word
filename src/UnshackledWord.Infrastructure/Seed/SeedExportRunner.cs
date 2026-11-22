using UnshackledWord.Infrastructure.Seed.Mappings;
using UnshackledWord.Infrastructure.Seed.Morphology;
using UnshackledWord.Infrastructure.Seed.Revision;
using UnshackledWord.Infrastructure.Seed.Vocab;

namespace UnshackledWord.Infrastructure.Seed;

public enum SeedExportKind
{
    All,
    Morphology,
    Mappings,
    Revision,
    Vocab
}

/// <summary>Runs the seed exporters of one kind into a layout; shared by the <c>export-seed</c> verb and the maintainer download.</summary>
public sealed class SeedExportRunner
{
    private readonly MorphologyExporter _morphologyExporter;
    private readonly MappingExporter _mappingExporter;
    private readonly RevisionExporter _revisionExporter;
    private readonly VocabSeedExporter _vocabExporter;

    public SeedExportRunner(MorphologyExporter morphologyExporter, MappingExporter mappingExporter, RevisionExporter revisionExporter, VocabSeedExporter vocabExporter)
    {
        _morphologyExporter = morphologyExporter;
        _mappingExporter = mappingExporter;
        _revisionExporter = revisionExporter;
        _vocabExporter = vocabExporter;
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

        if (kind is SeedExportKind.All or SeedExportKind.Vocab)
        {
            await _vocabExporter.ExportAsync(layout, token);
        }
    }
}
