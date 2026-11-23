using UnshackledWord.Infrastructure.Seed.Mappings;
using UnshackledWord.Infrastructure.Seed.Morphology;
using UnshackledWord.Infrastructure.Seed.Revision;
using UnshackledWord.Infrastructure.Seed.Tsk;
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

/// <summary>
/// Runs the seed exporters of one kind into a layout; shared by the <c>export-seed</c> verb and the maintainer download. <see cref="SeedExportKind.All"/>
/// also writes the TSK curation change file <c>tsk/curations.csv</c> (roadmap 0024.7); on its own it has its own maintainer job.
/// </summary>
public sealed class SeedExportRunner
{
    private readonly MorphologyExporter _morphologyExporter;
    private readonly MappingExporter _mappingExporter;
    private readonly RevisionExporter _revisionExporter;
    private readonly VocabSeedExporter _vocabExporter;
    private readonly TskCurationSeedService _tskExporter;

    public SeedExportRunner(MorphologyExporter morphologyExporter, MappingExporter mappingExporter, RevisionExporter revisionExporter, VocabSeedExporter vocabExporter,
        TskCurationSeedService tskExporter)
    {
        _morphologyExporter = morphologyExporter;
        _mappingExporter = mappingExporter;
        _revisionExporter = revisionExporter;
        _vocabExporter = vocabExporter;
        _tskExporter = tskExporter;
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

        if (kind is SeedExportKind.All)
        {
            await _tskExporter.ExportAsync(layout, null, token);
        }
    }
}
