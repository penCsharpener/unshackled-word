using System.Text.Json;
using UnshackledWord.Application.Abstractions;

namespace UnshackledWord.Infrastructure.Seed;

public sealed class SeedManifestStore
{
    private readonly IFileService _fileService;

    public SeedManifestStore(IFileService fileService)
    {
        _fileService = fileService;
    }

    public async Task<SeedManifest?> ReadAsync(string path, CancellationToken token = default)
    {
        if (!_fileService.FileExists(path))
        {
            return null;
        }

        var json = await _fileService.ReadAllTextAsync(path, SeedLayout.Utf8NoBom, token);

        return JsonSerializer.Deserialize<SeedManifest>(json, SeedManifest.JsonOptions);
    }

    /// <summary>
    /// Writes the manifest; when nothing but <c>exportedAt</c> differs from the manifest on disk, the old <c>exportedAt</c> stays, so an
    /// export of an unchanged database is byte-identical.
    /// </summary>
    public async Task WriteKeepingExportTimeAsync(string path, SeedManifest manifest, CancellationToken token = default)
    {
        var existing = await ReadAsync(path, token);

        manifest.ExportedAt = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);

        if (existing is not null && !string.IsNullOrEmpty(existing.ExportedAt))
        {
            var candidate = JsonSerializer.Serialize(manifest, SeedManifest.JsonOptions).Replace(manifest.ExportedAt, existing.ExportedAt);
            var current = JsonSerializer.Serialize(existing, SeedManifest.JsonOptions);

            if (candidate == current)
            {
                manifest.ExportedAt = existing.ExportedAt;
            }
        }

        await WriteAsync(path, manifest, token);
    }

    public async Task WriteAsync(string path, SeedManifest manifest, CancellationToken token = default)
    {
        var json = JsonSerializer.Serialize(manifest, SeedManifest.JsonOptions) + "\n";

        await _fileService.WriteAllTextAsync(path, json, SeedLayout.Utf8NoBom, token);
    }
}
