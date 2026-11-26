using System.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Infrastructure.Seed.Lxx;

/// <summary>
/// The LXX tag seed files <c>assets/seed/lxx/tags.csv</c> and <c>links.csv</c> (roadmap 0028.8): export with the <c>lxx</c> manifest part, import (maintainer job and the
/// one-time apply of SeedDb on a database without confirmed tags) and the mirror preview. The database logic lives in <see cref="LxxSeed"/>.
/// </summary>
public sealed class LxxSeedService
{
    private readonly IDbConnectionFactory _factory;
    private readonly IFileService _fileService;
    private readonly SeedManifestStore _manifestStore;
    private readonly DatabaseSeedSettings _settings;
    private readonly ILogger<LxxSeedService> _logger;

    public LxxSeedService(IDbConnectionFactory factory, IFileService fileService, SeedManifestStore manifestStore, IOptions<AppSettings> options,
        ILogger<LxxSeedService> logger)
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

    /// <summary>Writes every confirmed tag to <c>lxx/tags.csv</c>, every confirmed link to <c>lxx/links.csv</c> and the <c>lxx</c> part of <c>manifest.json</c>.</summary>
    public async Task<SeedStepReport> ExportAsync(IDbConnection connection, IDbTransaction? transaction, SeedLayout layout, IProgress<string>? progress,
        CancellationToken token)
    {
        progress?.Report("Exporting the confirmed LXX tags and links");
        var tags = await LxxSeed.ReadTagsAsync(connection, transaction, token);
        var (links, unanchored) = await LxxSeed.ReadLinksAsync(connection, transaction, token);
        var (tagBytes, tagHash) = LxxSeed.SerializeTags(tags);
        var (linkBytes, linkHash) = LxxSeed.SerializeLinks(links);

        await WriteAsync(layout, SeedLayout.LxxTagsPath, tagBytes, token);
        await WriteAsync(layout, SeedLayout.LxxLinksPath, linkBytes, token);

        List<SeedFileEntry> entries =
        [
            new() { Path = SeedLayout.LxxTagsPath, Rows = tags.Count, Sha256 = tagHash },
            new() { Path = SeedLayout.LxxLinksPath, Rows = links.Count, Sha256 = linkHash },
        ];
        var manifest = await _manifestStore.ReadAsync(layout.ManifestPath, token) ?? new SeedManifest();
        manifest.Lxx = new SeedKind { SeedVersion = SeedVersion.Compute(entries), Rows = tags.Count + links.Count, Files = entries };
        await _manifestStore.WriteKeepingExportTimeAsync(layout.ManifestPath, manifest, token);

        _logger.LogInformation("LXX seed export: {Tags} tags and {Links} links written ({Unanchored} links without a Hebrew word skipped)", tags.Count, links.Count, unanchored);
        progress?.Report($"Wrote {tags.Count} LXX tags and {links.Count} links to seed/{SeedLayout.LxxFolder}");

        return new SeedStepReport { Kind = LxxSeed.Kind, Action = "export", SeedVersion = manifest.Lxx.SeedVersion }
            .WithCount(LxxSeed.TagsCount, tags.Count)
            .WithCount(LxxSeed.LinksCount, links.Count)
            .WithCount(LxxSeed.Skipped, unanchored);
    }

    private async Task WriteAsync(SeedLayout layout, string relativePath, byte[] bytes, CancellationToken token)
    {
        var path = layout.Resolve(relativePath);
        _fileService.CreateDirectoryIfNotExists(Path.GetDirectoryName(path)!);
        await _fileService.WriteAllBytesAsync(path, bytes, token);
    }

    /// <summary>The lines of the seed files, or null when neither file exists (a missing single file counts as empty).</summary>
    public async Task<(List<LxxTagSeedLine> Tags, List<LxxLinkSeedLine> Links)?> ReadFilesAsync(SeedLayout layout, CancellationToken token)
    {
        var tagPath = layout.Resolve(SeedLayout.LxxTagsPath);
        var linkPath = layout.Resolve(SeedLayout.LxxLinksPath);
        var hasTags = _fileService.FileExists(tagPath);
        var hasLinks = _fileService.FileExists(linkPath);
        if (!hasTags && !hasLinks)
        {
            return null;
        }

        var tags = hasTags ? LxxSeed.ParseTags(await File.ReadAllBytesAsync(tagPath, token)) : [];
        var links = hasLinks ? LxxSeed.ParseLinks(await File.ReadAllBytesAsync(linkPath, token)) : [];

        return (tags, links);
    }

    public async Task<SeedStepReport> ImportAsync(bool mirror, SeedLayout? layout, IProgress<string>? progress, CancellationToken token)
    {
        layout ??= RequireRepoLayout();
        using var connection = await _factory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();

        var report = await ImportAsync(connection, transaction, layout, mirror, progress, token);

        transaction.Commit();
        _logger.LogInformation("LXX seed import (mirror {Mirror}): {Counts}", mirror, string.Join(", ", report.Counts.Select(kv => $"{kv.Key}={kv.Value}")));

        return report;
    }

    public async Task<SeedStepReport> ImportAsync(IDbConnection connection, IDbTransaction transaction, SeedLayout layout, bool mirror, IProgress<string>? progress,
        CancellationToken token)
    {
        var files = await ReadFilesAsync(layout, token);
        if (files is null)
        {
            return SeedStepReport.Skip(LxxSeed.Kind, "import", SeedSkipReason.NoSeed);
        }

        progress?.Report($"Reading seed/{SeedLayout.LxxFolder}");
        var report = await LxxSeed.ImportAsync(connection, transaction, files.Value.Tags, files.Value.Links, mirror, progress, token);
        report.SeedVersion = (await _manifestStore.ReadAsync(layout.ManifestPath, token))?.Lxx?.SeedVersion;

        return report;
    }

    /// <summary>What "Exakt wie Datei" would delete (confirmed tags and links the files lack) and the files' line counts (null = no files). Writes nothing.</summary>
    public async Task<(int Tags, int Links, int? FileTags, int? FileLinks)> PreviewAsync(SeedLayout? layout, CancellationToken token)
    {
        layout ??= RequireRepoLayout();
        var files = await ReadFilesAsync(layout, token);
        if (files is null)
        {
            return (0, 0, null, null);
        }

        using var connection = await _factory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();
        var (tags, links) = await LxxSeed.CountMirrorDeletesAsync(connection, transaction, files.Value.Tags, files.Value.Links, token);
        transaction.Rollback();

        return (tags, links, files.Value.Tags.Count, files.Value.Links.Count);
    }

    /// <summary>
    /// SeedDb, after the LXX text import: applies the files once, only while <c>LxxWordTags</c> has no confirmed row (a fresh database; proposals do not count). Later
    /// runs leave the tags alone; updates come only from the maintainer import.
    /// </summary>
    public async Task<SeedStepReport> ApplyInitialSeedAsync(IDbConnection connection, IDbTransaction transaction, SeedLayout layout, CancellationToken token)
    {
        if (await LxxSeed.CountConfirmedTagsAsync(connection, transaction, token) > 0)
        {
            return SeedStepReport.Skip(LxxSeed.Kind, "import", SeedSkipReason.TablesNotEmpty);
        }

        return await ImportAsync(connection, transaction, layout, mirror: false, progress: null, token);
    }

    /// <summary>
    /// SeedDb, after a STEP import that inserted Hebrew words (it inserts only into an empty <c>StepHebrewWords</c>, so every id is new): the stored links point to
    /// the old numbering. Deletes them all and re-resolves the links of the seed files by their Hebrew <c>SourceKey</c> (tags are not touched; they do not depend on
    /// STEP). Links confirmed after the last export are lost; export before a STEP re-import. Null when there was no link (a fresh database).
    /// </summary>
    public async Task<SeedStepReport?> RelinkAfterStepImportAsync(IDbConnection connection, IDbTransaction transaction, SeedLayout layout, CancellationToken token)
    {
        var stale = await LxxSeed.DeleteAllLinksAsync(connection, transaction, token);
        if (stale == 0)
        {
            return null;
        }

        var files = await ReadFilesAsync(layout, token);
        var report = files is null
            ? SeedStepReport.Skip(LxxSeed.Kind, "import", SeedSkipReason.NoSeed)
            : await LxxSeed.ImportAsync(connection, transaction, [], files.Value.Links, mirror: false, progress: null, token);
        _logger.LogWarning("STEP Hebrew words were re-imported: deleted {Stale} stale LXX Hebrew links and re-resolved the links of the seed files", stale);

        return report.WithCount("staleLinksDeleted", stale);
    }

    public async Task<SeedStepReport?> RelinkAfterStepImportAsync(CancellationToken token)
    {
        using var connection = await _factory.CreateDbConnectionAsync(token);
        using var transaction = connection.BeginTransaction();

        var report = await RelinkAfterStepImportAsync(connection, transaction, CreateRepoLayout(), token);

        transaction.Commit();

        return report;
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
