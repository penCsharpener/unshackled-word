using Microsoft.Extensions.Logging;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Seed.Mappings;
using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Infrastructure.Seed.Mappings;

/// <summary>
/// <c>export-seed --only mappings</c>: writes <c>Elb1871Mappings/NN-Book.csv</c> in format 2 (German side keyed like the morphology seed,
/// STEP side as <c>LxxRefId</c>, position and hash) and the mappings entry of the manifest. Reads the database only. The legacy tables
/// are the source until roadmap 0005 cuts over.
/// </summary>
public sealed class MappingExporter
{
    private readonly MappingExportRepository _repository;
    private readonly IFileService _fileService;
    private readonly SeedManifestStore _manifestStore;
    private readonly ILogger<MappingExporter> _logger;

    public MappingExporter(MappingExportRepository repository, IFileService fileService, SeedManifestStore manifestStore, ILogger<MappingExporter> logger)
    {
        _repository = repository;
        _fileService = fileService;
        _manifestStore = manifestStore;
        _logger = logger;
    }

    public async Task ExportAsync(SeedLayout layout, CancellationToken token = default)
    {
        _fileService.CreateDirectoryIfNotExists(layout.SeedRoot);
        _fileService.CreateDirectoryIfNotExists(layout.MappingsRoot);

        var kind = new SeedKind { Format = SeedLayout.MappingsFormat, Folder = SeedLayout.MappingsFolder };

        foreach (var bookId in BibleBook.AllBooks.Keys.Order())
        {
            var lines = await _repository.ReadBookAsync(bookId);

            if (lines.Count == 0)
            {
                continue;
            }

            kind.Files.Add(await WriteBookAsync(layout, bookId, lines, token));
        }

        kind.Rows = kind.Files.Sum(f => f.Rows);
        kind.SeedVersion = SeedVersion.Compute(kind.Files);

        await CheckNothingWasLeftOutAsync(kind);
        await WriteManifestAsync(layout, kind, token);

        _logger.LogInformation("Exported mapping seed: {Files} files, {Rows} lines, version {Version}", kind.Files.Count, kind.Rows, kind.SeedVersion[..12]);
    }

    private async Task<SeedFileEntry> WriteBookAsync(SeedLayout layout, int bookId, List<MappingSeedLine> lines, CancellationToken token)
    {
        var (bytes, sha256) = SeedCsv.Serialize<MappingSeedLine, MappingSeedLineMap>(lines);
        var fileName = SeedLayout.BookFileName(bookId);

        await _fileService.WriteAllBytesAsync(layout.ResolveMapping(fileName), bytes, token);

        return new SeedFileEntry { Path = fileName, Rows = lines.Count, Sha256 = sha256 };
    }

    private async Task CheckNothingWasLeftOutAsync(SeedKind kind)
    {
        var inTables = await _repository.CountMappingRowsAsync();
        var withoutWord = await _repository.CountRowsWithoutWordAsync();

        if (inTables != kind.Rows)
        {
            _logger.LogWarning(
                "The mapping tables hold {InTables} rows but the seed holds {InSeed} lines ({WithoutWord} rows point at no German word and are not exported)",
                inTables, kind.Rows, withoutWord);
        }
        else
        {
            _logger.LogInformation("All {Rows} mapping rows are in the seed", inTables);
        }
    }

    private async Task WriteManifestAsync(SeedLayout layout, SeedKind kind, CancellationToken token)
    {
        var existing = await _manifestStore.ReadAsync(layout.ManifestPath, token);

        var manifest = existing ?? new SeedManifest();
        manifest.Mappings = kind;

        await _manifestStore.WriteKeepingExportTimeAsync(layout.ManifestPath, manifest, token);
    }
}
