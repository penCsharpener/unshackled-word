using Microsoft.Extensions.Logging;
using System.Globalization;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Seed.Revision;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Infrastructure.Seed;
using UnshackledWord.Infrastructure.Seed.Revision;

namespace UnshackledWord.Infrastructure.Seed.Revision;

/// <summary>The revision seed that is on disk (roadmap 0011.11 files): levels, active variants and their mapping, plus the version derived from the files.</summary>
public sealed class RevisionSeedSource
{
    public List<RevisionLevelSeedLine> Levels { get; init; } = [];
    public List<RevisionVerseSeedLine> Verses { get; init; } = [];
    public List<RevisionMappingSeedLine> Mapping { get; init; } = [];

    /// <summary>Same formula as the exporter: path and SHA-256 of levels.csv, then verse files, then mapping files.</summary>
    public string SeedVersion { get; init; } = string.Empty;

    /// <summary>When the maintainer exported the files; null when the manifest does not describe exactly these files.</summary>
    public DateTimeOffset? ExportedAt { get; init; }
}

public sealed class RevisionSeedSourceReader
{
    private readonly IFileService _fileService;
    private readonly SeedManifestStore _manifestStore;
    private readonly ILogger<RevisionSeedSourceReader> _logger;

    public RevisionSeedSourceReader(IFileService fileService, SeedManifestStore manifestStore, ILogger<RevisionSeedSourceReader> logger)
    {
        _fileService = fileService;
        _manifestStore = manifestStore;
        _logger = logger;
    }

    /// <summary>Null when the folder holds neither a levels file nor a verse file.</summary>
    public async Task<RevisionSeedSource?> LoadAsync(SeedLayout layout, CancellationToken token = default)
    {
        var files = new List<SeedFileEntry>();
        var levels = new List<RevisionLevelSeedLine>();
        var verses = new List<RevisionVerseSeedLine>();
        var mapping = new List<RevisionMappingSeedLine>();

        levels.AddRange(await ReadAsync<RevisionLevelSeedLine, RevisionLevelSeedLineMap>(layout, SeedLayout.RevisionPath(null, SeedLayout.RevisionLevelsFile), files, token));

        foreach (var bookId in BibleBook.AllBooks.Keys.Order())
        {
            verses.AddRange(await ReadAsync<RevisionVerseSeedLine, RevisionVerseSeedLineMap>(layout, SeedLayout.RevisionPath(SeedLayout.RevisionVersesFolder, SeedLayout.BookFileName(bookId)), files, token));
        }

        foreach (var bookId in BibleBook.AllBooks.Keys.Order())
        {
            mapping.AddRange(await ReadAsync<RevisionMappingSeedLine, RevisionMappingSeedLineMap>(layout, SeedLayout.RevisionPath(SeedLayout.RevisionMappingFolder, SeedLayout.BookFileName(bookId)), files, token));
        }

        if (files.Count == 0)
        {
            return null;
        }

        var version = SeedVersion.Compute(files);
        var manifest = await _manifestStore.ReadAsync(layout.ManifestPath, token);

        if (manifest?.Revision is { } described && described.SeedVersion != version)
        {
            _logger.LogWarning("The revision files do not match the SHA-256 in the manifest; they were changed after the export");
        }

        var exportedAt = manifest?.Revision?.SeedVersion == version
            && DateTimeOffset.TryParse(manifest.ExportedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
                ? parsed
                : (DateTimeOffset?)null;

        return new RevisionSeedSource { Levels = levels, Verses = verses, Mapping = mapping, SeedVersion = version, ExportedAt = exportedAt };
    }

    private async Task<List<T>> ReadAsync<T, TMap>(SeedLayout layout, string relativePath, List<SeedFileEntry> files, CancellationToken token)
        where TMap : CsvHelper.Configuration.ClassMap<T>
    {
        var path = layout.Resolve(relativePath);

        if (!_fileService.FileExists(path))
        {
            return [];
        }

        var bytes = await File.ReadAllBytesAsync(path, token);
        var lines = SeedCsv.Read<T, TMap>(bytes);
        files.Add(new SeedFileEntry { Path = relativePath, Rows = lines.Count, Sha256 = SeedCsv.Sha256Hex(bytes) });

        return lines;
    }
}
