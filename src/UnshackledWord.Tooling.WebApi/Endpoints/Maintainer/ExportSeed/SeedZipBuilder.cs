using System.Diagnostics;
using System.IO.Compression;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Infrastructure.Seed;
using UnshackledWord.Tooling.WebApi.Models;

namespace UnshackledWord.Tooling.WebApi.Endpoints.Maintainer.ExportSeed;

/// <summary>
/// Runs the seed exporters (read-only database access) into a temp folder and packs the result as one zip whose entries are paths
/// relative to the repo root, exactly where <c>export-seed</c> writes them: <c>assets/seed/...</c> and <c>assets/Elb1871Mappings/...</c>.
/// Nothing is written below the assets folder; it is only read (base text hash and the existing manifest, so a one-kind export keeps
/// the other kind's manifest entry and an unchanged export keeps its <c>exportedAt</c>).
/// </summary>
public sealed class SeedZipBuilder
{
    private const string ManifestPath = "manifest.json";
    private const string AssetsPrefix = "assets";
    private const string SeedPrefix = "assets/seed";

    private readonly SeedExportRunner _runner;
    private readonly IFileService _fileService;
    private readonly DatabaseSeedSettings _settings;
    private readonly ILogger<SeedZipBuilder> _logger;

    public SeedZipBuilder(SeedExportRunner runner, IFileService fileService, IOptions<Models.AppSettings> options, ILogger<SeedZipBuilder> logger)
    {
        _runner = runner;
        _fileService = fileService;
        _settings = options.Value.DatabaseSeeding ?? new DatabaseSeedSettings();
        _logger = logger;
    }

    /// <summary>Why the kind cannot be exported here (assets folder not mounted), or null when it can.</summary>
    public string? CheckAvailable(SeedExportKind kind)
    {
        if (kind is SeedExportKind.Mappings or SeedExportKind.Revision or SeedExportKind.Vocab || BaseTextExists())
        {
            return null;
        }

        _logger.LogWarning("Seed export {Kind} refused: base text file not reachable below {AssetsPath}", kind, _settings.SolutionAssetsPath);

        return "The base text file is not reachable; mount the repo's assets folder read-only and set AppSettings:DatabaseSeeding:SolutionAssetsPath.";
    }

    /// <summary>The returned stream deletes its temp file when disposed.</summary>
    public async Task<FileStream> BuildAsync(SeedExportKind kind, CancellationToken token)
    {
        var stopwatch = Stopwatch.StartNew();
        var workDir = Path.Combine(Path.GetTempPath(), $"unshackledword-seed-{Guid.NewGuid():N}");
        _fileService.CreateDirectoryIfNotExists(workDir);

        try
        {
            var layout = new SeedLayout(_fileService, _settings, workDir);
            await SeedExistingManifestAsync(layout, token);
            await _runner.ExportAsync(kind, layout, token);

            var zip = await PackAsync(workDir, token);
            _logger.LogInformation("Seed export {Kind} built: {Bytes} zip bytes in {ElapsedMs} ms", kind, zip.Length, stopwatch.ElapsedMilliseconds);

            return zip;
        }
        finally
        {
            Directory.Delete(workDir, recursive: true);
        }
    }

    private bool BaseTextExists() =>
        !string.IsNullOrWhiteSpace(_settings.SolutionAssetsPath)
        && !string.IsNullOrWhiteSpace(_settings.Elberfelder1871TextFile)
        && File.Exists(Path.Combine(_settings.SolutionAssetsPath, _settings.Elberfelder1871TextFile));

    private async Task SeedExistingManifestAsync(SeedLayout layout, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(_settings.SolutionAssetsPath))
        {
            return;
        }

        var existing = Path.Combine(_settings.SolutionAssetsPath, "seed", ManifestPath);

        if (File.Exists(existing))
        {
            await using var source = File.OpenRead(existing);
            await using var target = File.Create(layout.ManifestPath);
            await source.CopyToAsync(target, token);
        }
    }

    private static async Task<FileStream> PackAsync(string workDir, CancellationToken token)
    {
        var zipPath = Path.Combine(Path.GetTempPath(), $"unshackledword-seed-{Guid.NewGuid():N}.zip");

        try
        {
            await using var file = new FileStream(zipPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
            await using var archive = await ZipArchive.CreateAsync(file, ZipArchiveMode.Create, leaveOpen: false, entryNameEncoding: null, token);

            foreach (var path in Directory.EnumerateFiles(workDir, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                var relative = Path.GetRelativePath(workDir, path).Replace(Path.DirectorySeparatorChar, '/');
                await archive.CreateEntryFromFileAsync(path, EntryName(relative), CompressionLevel.Optimal, token);
            }
        }
        catch
        {
            File.Delete(zipPath);

            throw;
        }

        return new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);
    }

    /// <summary>Temp-folder path to repo path: the mappings sit next to <c>assets/seed</c>, everything else below it.</summary>
    private static string EntryName(string relative) => relative.StartsWith(SeedLayout.MappingsFolder + "/", StringComparison.Ordinal)
        ? $"{AssetsPrefix}/{relative}"
        : $"{SeedPrefix}/{relative}";
}
