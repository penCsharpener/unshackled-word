using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Infrastructure.Seed;

/// <summary>
/// Export to the repo seed folders and the byte-identity check of the export, shared by the maintainer page and the CLI. Files of a kind are
/// the seed-relative paths <c>morphology/</c>, <c>anchors/</c>, <c>revision/</c>, <c>Elb1871Mappings/</c> plus <c>manifest.json</c>.
/// </summary>
public sealed class SeedFileExportService
{
    private const string ManifestFile = SeedLayout.ManifestFileName;

    private readonly SeedExportRunner _runner;
    private readonly Application.Abstractions.IFileService _fileService;
    private readonly DatabaseSeedSettings _settings;
    private readonly ILogger<SeedFileExportService> _logger;

    public SeedFileExportService(SeedExportRunner runner, Application.Abstractions.IFileService fileService, IOptions<AppSettings> options, ILogger<SeedFileExportService> logger)
    {
        _runner = runner;
        _fileService = fileService;
        _settings = options.Value.DatabaseSeeding ?? new DatabaseSeedSettings();
        _logger = logger;
    }

    /// <summary>Why the repo seed cannot be written here (assets folder missing), or null.</summary>
    public string? CheckAssetsAvailable()
    {
        return string.IsNullOrWhiteSpace(_settings.SolutionAssetsPath) || !Directory.Exists(_settings.SolutionAssetsPath)
            ? "The assets folder is not reachable; mount the repo's assets folder writable and set AppSettings:DatabaseSeeding:SolutionAssetsPath."
            : null;
    }

    public SeedLayout CreateRepoLayout() => new(_fileService, _settings);

    /// <summary>Writes the export into <c>assets/seed</c> (and <c>assets/Elb1871Mappings</c>) and reports which files changed.</summary>
    public async Task<SeedDiskExportResult> ExportToDiskAsync(SeedExportKind kind, IProgress<string>? progress, CancellationToken token)
    {
        var unavailable = CheckAssetsAvailable();

        if (unavailable is not null)
        {
            throw new InvalidOperationException(unavailable);
        }

        var layout = CreateRepoLayout();
        var before = await SnapshotAsync(layout, kind, token);

        progress?.Report($"Exporting {kind} seed to {layout.SeedRoot}");
        await _runner.ExportAsync(kind, layout, token);

        var after = await SnapshotAsync(layout, kind, token);
        var diff = Compare(after, before);
        _logger.LogInformation("Seed export {Kind} to {Path}: {Files} files, {Created} created, {Changed} changed, {Removed} removed", kind, layout.SeedRoot, after.Count, diff.OnlyInNew.Total, diff.Changed.Total, diff.OnlyInOld.Total);

        return new SeedDiskExportResult { Kind = kind.ToString().ToLowerInvariant(), SeedRoot = layout.SeedRoot, Files = after.Count, Diff = diff };
    }

    /// <summary>Exports twice into scratch folders (the second one starts from the first one's manifest) and once compares against the files on disk. Writes nothing below the assets folder.</summary>
    public async Task<SeedRoundTripResult> RoundTripCheckAsync(SeedExportKind kind, IProgress<string>? progress, CancellationToken token)
    {
        var repo = CreateRepoLayout();
        var first = NewWorkDir();
        var second = NewWorkDir();

        try
        {
            var firstLayout = new SeedLayout(_fileService, _settings, first);
            var secondLayout = new SeedLayout(_fileService, _settings, second);

            CopyManifest(repo.ManifestPath, firstLayout.ManifestPath);
            progress?.Report("Round trip 1/3: first export into a scratch folder");
            await _runner.ExportAsync(kind, firstLayout, token);

            CopyManifest(firstLayout.ManifestPath, secondLayout.ManifestPath);
            progress?.Report("Round trip 2/3: second export into another scratch folder");
            await _runner.ExportAsync(kind, secondLayout, token);

            progress?.Report("Round trip 3/3: comparing the exports with each other and with the files on disk");
            var firstFiles = await SnapshotAsync(firstLayout, kind, token);
            var secondFiles = await SnapshotAsync(secondLayout, kind, token);
            var diskFiles = await SnapshotAsync(repo, kind, token);

            var twice = Compare(secondFiles, firstFiles);
            var disk = Compare(firstFiles, diskFiles);

            return new SeedRoundTripResult
            {
                Kind = kind.ToString().ToLowerInvariant(),
                Files = firstFiles.Count,
                SecondExportIdentical = twice.IsEmpty,
                SecondExport = twice,
                MatchesDisk = disk.IsEmpty,
                Disk = disk
            };
        }
        finally
        {
            DeleteQuietly(first);
            DeleteQuietly(second);
        }
    }

    /// <summary>Seed-relative path (forward slashes) to SHA-256 of every file that belongs to the kind.</summary>
    public async Task<Dictionary<string, string>> SnapshotAsync(SeedLayout layout, SeedExportKind kind, CancellationToken token)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        async Task AddAsync(string key, string path)
        {
            result[key] = SeedCsv.Sha256Hex(await File.ReadAllBytesAsync(path, token));
        }

        if (File.Exists(layout.ManifestPath))
        {
            await AddAsync(ManifestFile, layout.ManifestPath);
        }

        var folders = new List<string>();

        if (kind is SeedExportKind.All or SeedExportKind.Morphology)
        {
            folders.Add(SeedLayout.MorphologyFolder);
            folders.Add(SeedLayout.AnchorsFolder);
        }

        if (kind is SeedExportKind.All or SeedExportKind.Revision)
        {
            folders.Add(SeedLayout.RevisionFolder);
        }

        foreach (var folder in folders)
        {
            var root = layout.Resolve(folder);

            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                await AddAsync(Path.GetRelativePath(layout.SeedRoot, path).Replace(Path.DirectorySeparatorChar, '/'), path);
            }
        }

        if (kind is SeedExportKind.All or SeedExportKind.Mappings && Directory.Exists(layout.MappingsRoot))
        {
            foreach (var path in Directory.EnumerateFiles(layout.MappingsRoot, "*.csv", SearchOption.TopDirectoryOnly))
            {
                await AddAsync($"{SeedLayout.MappingsFolder}/{Path.GetFileName(path)}", path);
            }
        }

        return result;
    }

    public static SeedFilesDiff Compare(IReadOnlyDictionary<string, string> newer, IReadOnlyDictionary<string, string> older)
    {
        var changed = newer.Where(n => older.TryGetValue(n.Key, out var hash) && hash != n.Value).Select(n => n.Key).Order(StringComparer.Ordinal).ToList();
        var onlyNew = newer.Keys.Where(k => !older.ContainsKey(k)).Order(StringComparer.Ordinal).ToList();
        var onlyOld = older.Keys.Where(k => !newer.ContainsKey(k)).Order(StringComparer.Ordinal).ToList();

        return new SeedFilesDiff
        {
            Compared = newer.Keys.Union(older.Keys).Count(),
            Identical = newer.Count(n => older.TryGetValue(n.Key, out var hash) && hash == n.Value),
            Changed = ToList(changed),
            OnlyInNew = ToList(onlyNew),
            OnlyInOld = ToList(onlyOld)
        };
    }

    private static SeedReportList ToList(List<string> items) => new() { Total = items.Count, Items = items.Take(SeedStepReport.MaxListItems).ToList() };

    private static string NewWorkDir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"unshackledword-seedcheck-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);

        return path;
    }

    private static void CopyManifest(string from, string to)
    {
        if (File.Exists(from))
        {
            File.Copy(from, to, overwrite: true);
        }
    }

    private void DeleteQuietly(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Could not remove the scratch folder {Path}", path);
        }
    }
}
