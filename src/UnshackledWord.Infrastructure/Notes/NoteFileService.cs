using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Notes;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Domain.WebApi.Maintainer;

namespace UnshackledWord.Infrastructure.Notes;

/// <summary>
/// The notes export and import of the maintainer jobs (roadmap 0023.6): files <c>notes-YYYYMMDD-HHmmss.json</c> in <c>{SolutionTempPath}/notes</c>,
/// never in <c>assets/</c>, never part of the seed.
/// </summary>
public sealed partial class NoteFileService
{
    public const string FolderName = "notes";

    private readonly NoteExportService _export;
    private readonly IDbConnectionFactory _factory;
    private readonly string _tempPath;
    private readonly ILogger<NoteFileService> _logger;

    public NoteFileService(NoteExportService export, IDbConnectionFactory factory, IOptions<AppSettings> options, ILogger<NoteFileService> logger)
    {
        _export = export;
        _factory = factory;
        _tempPath = options.Value.DatabaseSeeding?.SolutionTempPath ?? string.Empty;
        _logger = logger;
    }

    [GeneratedRegex(@"^notes-\d{8}-\d{6}\.json$")]
    public static partial Regex FileNamePattern();

    public string Folder => Path.Combine(_tempPath, FolderName);

    /// <summary>Why the notes folder cannot be used (temp path not configured or not reachable), or null.</summary>
    public string? CheckAvailable() =>
        string.IsNullOrWhiteSpace(_tempPath) || !Directory.Exists(_tempPath)
            ? "The temp folder is not reachable; mount the repo's temp folder writable and set AppSettings:DatabaseSeeding:SolutionTempPath."
            : null;

    /// <summary>The export files, newest first.</summary>
    public IReadOnlyList<NoteFileInfo> ListFiles()
    {
        if (!Directory.Exists(Folder))
        {
            return [];
        }

        return new DirectoryInfo(Folder).EnumerateFiles("notes-*.json")
            .Where(f => FileNamePattern().IsMatch(f.Name))
            .OrderByDescending(f => f.Name, StringComparer.Ordinal)
            .Select(f => new NoteFileInfo { Name = f.Name, SizeBytes = f.Length, ModifiedAt = new DateTimeOffset(f.LastWriteTimeUtc, TimeSpan.Zero) })
            .ToList();
    }

    public async Task<SeedStepReport> ExportAsync(string appVersion, DateTime exportedAtUtc, IProgress<string>? progress, CancellationToken token)
    {
        var unavailable = CheckAvailable();

        if (unavailable is not null)
        {
            throw new InvalidOperationException(unavailable);
        }

        progress?.Report("Exporting all notes");
        var file = await _export.BuildAsync(null, appVersion, exportedAtUtc, token);
        Directory.CreateDirectory(Folder);

        var name = NoteExportService.FileName(exportedAtUtc);
        var path = Path.Combine(Folder, name);
        var temp = path + ".tmp";
        await File.WriteAllBytesAsync(temp, NoteExportService.Serialize(file), token);
        File.Move(temp, path, true);

        _logger.LogInformation("Notes export: {Notes} notes written to {Path}", file.Notes.Count, path);
        progress?.Report($"Wrote {file.Notes.Count} notes to {FolderName}/{name}");

        return new SeedStepReport { Kind = "notes", Action = "export" }.WithCount("notes", file.Notes.Count).WithReportFile($"{FolderName}/{name}");
    }

    /// <summary>Imports the named file, or the newest one when <paramref name="fileName"/> is empty.</summary>
    public async Task<SeedStepReport> ImportAsync(string? fileName, IProgress<string>? progress, CancellationToken token)
    {
        var unavailable = CheckAvailable();

        if (unavailable is not null)
        {
            throw new InvalidOperationException(unavailable);
        }

        var name = string.IsNullOrWhiteSpace(fileName) ? ListFiles().FirstOrDefault()?.Name : fileName;

        if (name is null)
        {
            return SeedStepReport.Skip("notes", "import", $"No export file in {FolderName}/.");
        }

        if (!FileNamePattern().IsMatch(name))
        {
            throw new InvalidOperationException($"'{name}' is not a notes export file name.");
        }

        var path = Path.Combine(Folder, name);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"{FolderName}/{name} does not exist.");
        }

        progress?.Report($"Reading {FolderName}/{name}");
        var file = NoteExportService.Deserialize(await File.ReadAllBytesAsync(path, token));

        if (file is null || file.Format != NoteExportFile.FormatName)
        {
            throw new InvalidOperationException($"{name} is not an {NoteExportFile.FormatName} file.");
        }

        if (file.FormatVersion != NoteExportFile.CurrentFormatVersion)
        {
            throw new InvalidOperationException($"Unsupported format version {file.FormatVersion} (expected {NoteExportFile.CurrentFormatVersion}).");
        }

        using var connection = await _factory.CreateDbConnectionAsync(token);
        var report = await NoteImporter.ImportAsync(connection, null, file, progress, token);
        report.SeedVersion = file.AppVersion;
        report.WithReportFile($"{FolderName}/{name}");

        _logger.LogInformation("Notes import from {Name}: {Counts}", name, string.Join(", ", report.Counts.Select(kv => $"{kv.Key}={kv.Value}")));

        return report;
    }
}
