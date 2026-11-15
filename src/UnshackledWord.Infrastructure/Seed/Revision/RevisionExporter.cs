using Microsoft.Extensions.Logging;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Seed.Revision;
using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Infrastructure.Seed.Revision;

/// <summary>
/// <c>export-seed --only revision</c> (roadmap 0011.11): writes <c>revision/levels.csv</c>, <c>revision/verses/NN-Book.csv</c> and
/// <c>revision/mapping/NN-Book.csv</c> and the revision entry of the manifest. Only active variants, only books that have some; no ids,
/// users or timestamps, and no STEP text (mapping lines carry <c>SrcHash</c> only). Files of books that lost their last variant are
/// removed so the folder is exactly the export. Reads the database only.
/// </summary>
public sealed class RevisionExporter
{
    private readonly RevisionExportRepository _repository;
    private readonly IFileService _fileService;
    private readonly SeedManifestStore _manifestStore;
    private readonly ILogger<RevisionExporter> _logger;

    public RevisionExporter(RevisionExportRepository repository, IFileService fileService, SeedManifestStore manifestStore, ILogger<RevisionExporter> logger)
    {
        _repository = repository;
        _fileService = fileService;
        _manifestStore = manifestStore;
        _logger = logger;
    }

    public async Task ExportAsync(SeedLayout layout, CancellationToken token = default)
    {
        var levels = await _repository.ReadLevelsAsync();

        if (levels.Count == 0)
        {
            _logger.LogWarning("RevisionLevels is empty; the revision seed was not exported and the files on disk stay as they are");
            return;
        }

        var versesFolder = layout.Resolve($"{SeedLayout.RevisionFolder}/{SeedLayout.RevisionVersesFolder}");
        var mappingFolder = layout.Resolve($"{SeedLayout.RevisionFolder}/{SeedLayout.RevisionMappingFolder}");
        _fileService.CreateDirectoryIfNotExists(versesFolder);
        _fileService.CreateDirectoryIfNotExists(mappingFolder);

        var kind = new SeedKind { Folder = null };
        var verseFiles = new List<SeedFileEntry>();
        var mappingFiles = new List<SeedFileEntry>();

        kind.Files.Add(await WriteAsync<RevisionLevelSeedLine, RevisionLevelSeedLineMap>(layout, SeedLayout.RevisionPath(null, SeedLayout.RevisionLevelsFile), levels, token));

        foreach (var bookId in BibleBook.AllBooks.Keys.Order())
        {
            var data = await _repository.ReadBookAsync(bookId);

            if (data.Verses.Count == 0)
            {
                continue;
            }

            var fileName = SeedLayout.BookFileName(bookId);
            verseFiles.Add(await WriteAsync<RevisionVerseSeedLine, RevisionVerseSeedLineMap>(layout, SeedLayout.RevisionPath(SeedLayout.RevisionVersesFolder, fileName), data.Verses, token));

            if (data.Mapping.Count > 0)
            {
                mappingFiles.Add(await WriteAsync<RevisionMappingSeedLine, RevisionMappingSeedLineMap>(layout, SeedLayout.RevisionPath(SeedLayout.RevisionMappingFolder, fileName), data.Mapping, token));
            }
        }

        kind.Files.AddRange(verseFiles);
        kind.Files.AddRange(mappingFiles);
        kind.Rows = kind.Files.Sum(f => f.Rows);
        kind.SeedVersion = SeedVersion.Compute(kind.Files);

        RemoveStaleFiles(versesFolder, verseFiles.Select(f => f.Path));
        RemoveStaleFiles(mappingFolder, mappingFiles.Select(f => f.Path));

        await CheckNothingWasLeftOutAsync(mappingFiles.Sum(f => f.Rows));
        await WriteManifestAsync(layout, kind, token);

        _logger.LogInformation(
            "Exported revision seed: {Levels} levels, {Verses} variants in {VerseFiles} files, {Mapping} mapping lines in {MappingFiles} files, version {Version}",
            levels.Count, verseFiles.Sum(f => f.Rows), verseFiles.Count, mappingFiles.Sum(f => f.Rows), mappingFiles.Count, kind.SeedVersion[..12]);
    }

    private async Task<SeedFileEntry> WriteAsync<T, TMap>(SeedLayout layout, string relativePath, List<T> lines, CancellationToken token)
        where TMap : CsvHelper.Configuration.ClassMap<T>
    {
        var (bytes, sha256) = SeedCsv.Serialize<T, TMap>(lines);

        await _fileService.WriteAllBytesAsync(layout.Resolve(relativePath), bytes, token);

        return new SeedFileEntry { Path = relativePath, Rows = lines.Count, Sha256 = sha256 };
    }

    private void RemoveStaleFiles(string folder, IEnumerable<string> keptRelativePaths)
    {
        var kept = keptRelativePaths.Select(p => Path.GetFileName(p)).ToHashSet(StringComparer.Ordinal);

        foreach (var file in _fileService.SearchFiles(folder, "*.csv", SearchOption.TopDirectoryOnly))
        {
            if (!kept.Contains(Path.GetFileName(file)))
            {
                _fileService.DeleteFile(file);
                _logger.LogInformation("Removed {File}: the book has no active variant (or mapping) any more", file);
            }
        }
    }

    private async Task CheckNothingWasLeftOutAsync(int exportedLines)
    {
        var inTable = await _repository.CountActiveMappingRowsAsync();

        if (inTable != exportedLines)
        {
            _logger.LogWarning("The active variants hold {InTable} mapping rows but the seed holds {InSeed} lines (rows without an original word that are not added words are not exported)", inTable, exportedLines);
        }
    }

    private async Task WriteManifestAsync(SeedLayout layout, SeedKind kind, CancellationToken token)
    {
        var manifest = await _manifestStore.ReadAsync(layout.ManifestPath, token) ?? new SeedManifest();
        manifest.Revision = kind;

        await _manifestStore.WriteKeepingExportTimeAsync(layout.ManifestPath, manifest, token);
    }
}
