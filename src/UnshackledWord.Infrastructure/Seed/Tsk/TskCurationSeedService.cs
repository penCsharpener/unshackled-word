using System.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Infrastructure.Seed.Tsk;

/// <summary>
/// The TSK curation seed file <c>assets/seed/tsk/curations.csv</c> (roadmap 0024.7): export with its <c>tsk</c> manifest part, import (maintainer job
/// and the one-time apply of SeedDb on a fresh database) and the mirror preview. The database logic lives in <see cref="TskCurationSeed"/>.
/// </summary>
public sealed class TskCurationSeedService
{
    private readonly IDbConnectionFactory _factory;
    private readonly IFileService _fileService;
    private readonly SeedManifestStore _manifestStore;
    private readonly DatabaseSeedSettings _settings;
    private readonly ILogger<TskCurationSeedService> _logger;

    public TskCurationSeedService(IDbConnectionFactory factory, IFileService fileService, SeedManifestStore manifestStore, IOptions<AppSettings> options,
        ILogger<TskCurationSeedService> logger)
    {
        _factory = factory;
        _fileService = fileService;
        _manifestStore = manifestStore;
        _settings = options.Value.DatabaseSeeding ?? new DatabaseSeedSettings();
        _logger = logger;
    }

    /// <summary>Why <c>assets</c> cannot be used (not configured or not reachable), or null.</summary>
    public string? CheckAvailable() =>
        string.IsNullOrWhiteSpace(_settings.SolutionAssetsPath) || !Directory.Exists(_settings.SolutionAssetsPath)
            ? "The assets folder is not reachable; mount the repo's assets folder writable and set AppSettings:DatabaseSeeding:SolutionAssetsPath."
            : null;

    public SeedLayout CreateRepoLayout() => new(_fileService, _settings);

    public async Task<SeedStepReport> ExportAsync(SeedLayout? layout, IProgress<string>? progress, CancellationToken token)
    {
        layout ??= RequireRepoLayout();
        using var connection = await _factory.CreateDbConnectionAsync(token);

        return await ExportAsync(connection, null, layout, progress, token);
    }

    /// <summary>Writes every <c>TskCurations</c> row to <c>tsk/curations.csv</c> and the <c>tsk</c> part of <c>manifest.json</c>.</summary>
    public async Task<SeedStepReport> ExportAsync(IDbConnection connection, IDbTransaction? transaction, SeedLayout layout, IProgress<string>? progress,
        CancellationToken token)
    {
        progress?.Report("Exporting the TSK curations");
        var lines = await TskCurationSeed.ReadAsync(connection, transaction, token);
        var (bytes, sha256) = TskCurationSeed.Serialize(lines);

        var path = layout.Resolve(SeedLayout.TskCurationsPath);
        _fileService.CreateDirectoryIfNotExists(Path.GetDirectoryName(path)!);
        await _fileService.WriteAllBytesAsync(path, bytes, token);

        var entry = new SeedFileEntry { Path = SeedLayout.TskCurationsPath, Rows = lines.Count, Sha256 = sha256 };
        var manifest = await _manifestStore.ReadAsync(layout.ManifestPath, token) ?? new SeedManifest();
        manifest.Tsk = new SeedKind { SeedVersion = SeedVersion.Compute([entry]), Rows = lines.Count, Files = [entry] };
        await _manifestStore.WriteKeepingExportTimeAsync(layout.ManifestPath, manifest, token);

        _logger.LogInformation("TSK curation export: {Rows} rows written to {Path}", lines.Count, path);
        progress?.Report($"Wrote {lines.Count} TSK curations to seed/{SeedLayout.TskCurationsPath}");

        return new SeedStepReport { Kind = TskCurationSeed.Kind, Action = "export", SeedVersion = manifest.Tsk.SeedVersion }
            .WithCount("rows", lines.Count);
    }

    /// <summary>The lines of the seed file, or null when it does not exist.</summary>
    public async Task<List<TskCurationSeedLine>?> ReadFileAsync(SeedLayout layout, CancellationToken token)
    {
        var path = layout.Resolve(SeedLayout.TskCurationsPath);

        return _fileService.FileExists(path) ? TskCurationSeed.Parse(await File.ReadAllBytesAsync(path, token)) : null;
    }

    public async Task<SeedStepReport> ImportAsync(bool mirror, SeedLayout? layout, IProgress<string>? progress, CancellationToken token)
    {
        layout ??= RequireRepoLayout();
        using var connection = await _factory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();

        var report = await ImportAsync(connection, transaction, layout, mirror, progress, token);

        transaction.Commit();
        _logger.LogInformation("TSK curation import (mirror {Mirror}): {Counts}", mirror, string.Join(", ", report.Counts.Select(kv => $"{kv.Key}={kv.Value}")));

        return report;
    }

    public async Task<SeedStepReport> ImportAsync(IDbConnection connection, IDbTransaction transaction, SeedLayout layout, bool mirror, IProgress<string>? progress,
        CancellationToken token)
    {
        var lines = await ReadFileAsync(layout, token);
        if (lines is null)
        {
            return SeedStepReport.Skip(TskCurationSeed.Kind, "import", SeedSkipReason.NoSeed);
        }

        progress?.Report($"Reading seed/{SeedLayout.TskCurationsPath}");
        var report = await TskCurationSeed.ImportAsync(connection, transaction, lines, mirror, progress, token);
        report.SeedVersion = (await _manifestStore.ReadAsync(layout.ManifestPath, token))?.Tsk?.SeedVersion;

        return report;
    }

    /// <summary>Rows "Exakt wie Datei" would delete, and the file's line count (null = no file).</summary>
    public async Task<(int WouldDelete, int? FileRows)> PreviewAsync(SeedLayout? layout, CancellationToken token)
    {
        layout ??= RequireRepoLayout();
        var lines = await ReadFileAsync(layout, token);
        if (lines is null)
        {
            return (0, null);
        }

        using var connection = await _factory.CreateDbConnectionAsync(token);

        return (await TskCurationSeed.CountMirrorDeletesAsync(connection, null, lines, token), lines.Count);
    }

    /// <summary>
    /// SeedDb, right after the TSK import: applies the file once, only when <c>TskCurations</c> is empty (a fresh database). Later runs leave the table
    /// alone; updates come only from the maintainer import.
    /// </summary>
    public async Task<SeedStepReport> ApplyInitialSeedAsync(IDbConnection connection, IDbTransaction transaction, SeedLayout layout, CancellationToken token)
    {
        if (await TskCurationSeed.CountRowsAsync(connection, transaction, token) > 0)
        {
            return SeedStepReport.Skip(TskCurationSeed.Kind, "import", SeedSkipReason.TablesNotEmpty);
        }

        return await ImportAsync(connection, transaction, layout, mirror: false, progress: null, token);
    }

    public async Task<SeedStepReport> ApplyInitialSeedAsync(CancellationToken token)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();

        var report = await ApplyInitialSeedAsync(connection, transaction, CreateRepoLayout(), token);

        transaction.Commit();

        return report;
    }

    private SeedLayout RequireRepoLayout()
    {
        var unavailable = CheckAvailable();

        return unavailable is null ? CreateRepoLayout() : throw new InvalidOperationException(unavailable);
    }
}
