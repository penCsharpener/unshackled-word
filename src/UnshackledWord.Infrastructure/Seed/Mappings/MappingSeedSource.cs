using Microsoft.Extensions.Logging;
using UnshackledWord.Infrastructure.Seed.Mappings;
using UnshackledWord.Infrastructure.Seed;
using System.Globalization;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Infrastructure.Seed.Mappings;

/// <summary>
/// The mapping CSVs that are on disk: their format (1 = today's files with STEP text, 2 = anchored), file hashes and the seed version
/// derived from them. Computed from the files themselves, so a folder without a manifest entry still works.
/// </summary>
public sealed class MappingSeedSource
{
    public int Format { get; init; }

    public List<SeedFileEntry> Files { get; init; } = [];

    public string SeedVersion { get; init; } = string.Empty;

    /// <summary>When the maintainer exported the files; null when the manifest does not describe exactly these files.</summary>
    public DateTimeOffset? ExportedAt { get; init; }
}

public sealed class MappingSeedSourceReader
{
    private readonly IFileService _fileService;
    private readonly SeedManifestStore _manifestStore;
    private readonly ILogger<MappingSeedSourceReader> _logger;

    public MappingSeedSourceReader(IFileService fileService, SeedManifestStore manifestStore, ILogger<MappingSeedSourceReader> logger)
    {
        _fileService = fileService;
        _manifestStore = manifestStore;
        _logger = logger;
    }

    /// <summary>Null when the folder holds no mapping file or the files are of mixed formats.</summary>
    public async Task<MappingSeedSource?> LoadAsync(SeedLayout layout, CancellationToken token = default)
    {
        var files = new List<SeedFileEntry>();
        var formats = new HashSet<int>();

        foreach (var bookId in BibleBook.AllBooks.Keys.Order())
        {
            var fileName = SeedLayout.BookFileName(bookId);
            var path = layout.ResolveMapping(fileName);

            if (!_fileService.FileExists(path))
            {
                _logger.LogWarning("Could not find csv file {path}", path);
                continue;
            }

            formats.Add(await MappingSeedFormat.DetectAsync(path, token));
            files.Add(new SeedFileEntry { Path = fileName, Sha256 = SeedCsv.Sha256Hex(await File.ReadAllBytesAsync(path, token)) });
        }

        if (files.Count == 0)
        {
            return null;
        }

        if (formats.Count > 1)
        {
            _logger.LogError("The mapping files in {Folder} are of mixed formats; neither is imported", layout.MappingsRoot);
            return null;
        }

        var version = SeedVersion.Compute(files);
        var manifest = await _manifestStore.ReadAsync(layout.ManifestPath, token);

        if (manifest?.Mappings is { } described && described.SeedVersion != version)
        {
            _logger.LogWarning("The mapping files do not match the SHA-256 in the manifest; they were changed after the export");
        }

        var exportedAt = manifest?.Mappings?.SeedVersion == version
            && DateTimeOffset.TryParse(manifest.ExportedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
                ? parsed
                : (DateTimeOffset?)null;

        return new MappingSeedSource { Format = formats.Single(), Files = files, SeedVersion = version, ExportedAt = exportedAt };
    }
}
