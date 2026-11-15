using UnshackledWord.Domain.WebApi.Maintainer;
using Microsoft.Extensions.Logging;
using UnshackledWord.Infrastructure.Seed;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Seed.Anchoring;
using UnshackledWord.Application.Features.Seed.Morphology;
using UnshackledWord.Domain.Models.Settings;

namespace UnshackledWord.Infrastructure.Seed.Morphology;

/// <summary>
/// Imports the repo's morphology seed (<c>assets/seed/morphology</c>) on a fresh install (roadmap 0010.4): only when
/// <c>Elb1871Morphology</c> is empty, after the German words exist. Each chapter is placed on the words this installation has
/// (<see cref="MorphologyPlacer"/>), everything that could not be placed is listed in <c>temp/seed-report</c> and counted in
/// <c>SeedImports</c>. All or nothing: one transaction.
/// </summary>
public sealed class MorphologySeedImporter
{
    public const string Kind = "morphology";

    private readonly MorphologySeedRepository _repository;
    private readonly ElbWordTokenReader _wordReader;
    private readonly SeedManifestStore _manifestStore;
    private readonly IFileService _fileService;
    private readonly SeedLayout _layout;
    private readonly ILogger<MorphologySeedImporter> _logger;

    public MorphologySeedImporter(
        MorphologySeedRepository repository,
        ElbWordTokenReader wordReader,
        SeedManifestStore manifestStore,
        IFileService fileService,
        IOptions<AppSettings> options,
        ILogger<MorphologySeedImporter> logger)
    {
        _repository = repository;
        _wordReader = wordReader;
        _manifestStore = manifestStore;
        _fileService = fileService;
        _layout = new SeedLayout(fileService, options.Value.DatabaseSeeding);
        _logger = logger;
    }

    public Task<SeedStepReport> Run(CancellationToken token = default) => RunAsync(_layout, token);

    /// <summary>The import against another seed folder than <c>assets/seed</c> (used by the <c>rebase</c> verb for its snapshot).</summary>
    public async Task<SeedStepReport> RunAsync(SeedLayout layout, CancellationToken token = default)
    {
        var existing = await _repository.CountMorphologyAsync();

        if (existing > 0)
        {
            _logger.LogInformation("Morphology seed skipped: Elb1871Morphology is not empty ({Rows} rows)", existing);

            return SeedStepReport.Skip(Kind, "import", SeedSkipReason.TablesNotEmpty);
        }

        var manifest = await _manifestStore.ReadAsync(layout.ManifestPath, token);

        if (manifest?.Morphology is null)
        {
            _logger.LogInformation("Morphology seed skipped: no morphology seed in {Path}", layout.ManifestPath);

            return SeedStepReport.Skip(Kind, "import", SeedSkipReason.NoSeed);
        }

        if (manifest.Format != SeedLayout.FormatVersion)
        {
            _logger.LogError("Morphology seed skipped: manifest format {Found} is not supported (expected {Expected})", manifest.Format, SeedLayout.FormatVersion);

            return SeedStepReport.Skip(Kind, "import", SeedSkipReason.UnsupportedFormat);
        }

        if (await _repository.CountWordsAsync() == 0)
        {
            _logger.LogWarning("Morphology seed skipped: Elb1871Words is empty, nothing to place the seed on");

            return SeedStepReport.Skip(Kind, "import", SeedSkipReason.NoWords);
        }

        return await ImportAsync(layout, manifest, manifest.Morphology, token);
    }

    private async Task<SeedStepReport> ImportAsync(SeedLayout layout, SeedManifest manifest, SeedKind kind, CancellationToken token)
    {
        using var session = await _repository.BeginImportAsync(token);

        if (session is null)
        {
            _logger.LogInformation("Morphology seed skipped: Elb1871Morphology is not empty");

            return SeedStepReport.Skip(Kind, "import", SeedSkipReason.TablesNotEmpty, kind.SeedVersion);
        }

        var reportLines = new List<PlacedLine>();
        var counts = new int[4];
        var rowsInserted = 0;
        var alignedChapters = 0;

        foreach (var file in kind.Files)
        {
            var bookId = int.Parse(file.Path.Split('/')[1][..2], CultureInfo.InvariantCulture);
            var lines = await ReadSeedFileAsync<MorphologySeedLine, MorphologySeedLineMap>(layout, file, token);
            var fingerprints = await ReadFingerprintsAsync(layout, kind, bookId, token);
            var placement = MorphologyPlacer.Place(lines.OrderBy(l => l.Ref).ThenBy(l => l.Pos).ToList(), await _wordReader.ReadBookAsync(bookId), fingerprints);

            var rows = placement.Placed.Select(p => ToInsertRow(p)).OrderBy(r => r.HebRefId).ThenBy(r => r.PositionInVerse).ToList();
            await session.InsertAsync(rows);

            rowsInserted += rows.Count;
            counts[0] += placement.Exact;
            counts[1] += placement.Shifted;
            counts[2] += placement.Modified;
            counts[3] += placement.Orphaned;
            alignedChapters += placement.ChaptersAligned.Count;
            reportLines.AddRange(placement.Reportable);
        }

        var record = new SeedImportRecord(Kind, kind.SeedVersion, ParseExportedAt(manifest.ExportedAt), rowsInserted, counts[0], counts[1], counts[2], counts[3]);
        var changedVerses = await session.CompleteAsync(record);

        _logger.LogInformation(
            "Morphology seed imported: {Rows} rows (exact {Exact}, shifted {Shifted}, modified {Modified}, orphaned {Orphaned}), {Aligned} chapters needed alignment, lemma text refreshed for {Verses} verses",
            rowsInserted, counts[0], counts[1], counts[2], counts[3], alignedChapters, changedVerses);

        var reportFile = await WriteReportAsync(layout, kind.SeedVersion, reportLines, token);

        return new SeedStepReport { Kind = Kind, Action = "import", SeedVersion = kind.SeedVersion }
            .WithCount("rows", rowsInserted)
            .WithCount("exact", counts[0]).WithCount("shifted", counts[1]).WithCount("modified", counts[2]).WithCount("orphaned", counts[3])
            .WithCount("alignedChapters", alignedChapters)
            .WithReportFile(reportFile);
    }

    private async Task<List<T>> ReadSeedFileAsync<T, TMap>(SeedLayout layout, SeedFileEntry file, CancellationToken token)
        where TMap : CsvHelper.Configuration.ClassMap<T>
    {
        var path = layout.Resolve(file.Path);

        if (!_fileService.FileExists(path))
        {
            throw new FileNotFoundException($"Seed file listed in the manifest is missing: {file.Path}", path);
        }

        var bytes = await File.ReadAllBytesAsync(path, token);

        if (SeedCsv.Sha256Hex(bytes) != file.Sha256)
        {
            _logger.LogWarning("Seed file {Path} does not match the SHA-256 in the manifest; it was changed after the export", file.Path);
        }

        return SeedCsv.Read<T, TMap>(bytes);
    }

    private async Task<Dictionary<int, string>> ReadFingerprintsAsync(SeedLayout layout, SeedKind kind, int bookId, CancellationToken token)
    {
        var anchorPath = SeedLayout.RelativePath(SeedLayout.AnchorsFolder, bookId);
        var entry = kind.AnchorFiles.FirstOrDefault(f => f.Path == anchorPath);

        if (entry is null)
        {
            return [];
        }

        var rows = await ReadSeedFileAsync<AnchorRow, AnchorRowMap>(layout, entry, token);

        return rows.ToDictionary(r => r.Chapter, r => r.Fingerprint);
    }

    private static MorphologyInsertRow ToInsertRow(PlacedLine placed)
    {
        var line = placed.Line;
        var features = MorphologyFeatures.Parse(line.Feats);

        return new MorphologyInsertRow(
            placed.New!.Value.Ref,
            placed.New.Value.Pos,
            line.Lemma,
            line.Upos,
            NullIfEmpty(line.Stts),
            NullIfEmpty(features.Degree),
            NullIfEmpty(features.VerbForm),
            NullIfEmpty(features.Tense),
            NullIfEmpty(features.Person),
            NullIfEmpty(features.Number),
            NullIfEmpty(features.Mood),
            NullIfEmpty(features.Case),
            NullIfEmpty(features.Gender));
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static DateTimeOffset? ParseExportedAt(string value)
    {
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : null;
    }

    /// <summary>Report of everything that is not an exact match: class, old key, new key (empty for orphans), form and lemma.</summary>
    private async Task<string> WriteReportAsync(SeedLayout layout, string seedVersion, List<PlacedLine> reportLines, CancellationToken token)
    {
        _fileService.CreateDirectoryIfNotExists(layout.ReportRoot);
        var fileName = $"morphology-{seedVersion[..12]}.csv";
        var path = _fileService.Combine(layout.ReportRoot, fileName);
        var builder = new StringBuilder("Class,OldRef,OldPos,NewRef,NewPos,Form,Lemma\n");

        foreach (var placed in reportLines)
        {
            var newRef = placed.New?.Ref.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            var newPos = placed.New?.Pos.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            builder.Append(placed.Class).Append(',')
                .Append(placed.Line.Ref).Append(',').Append(placed.Line.Pos).Append(',')
                .Append(newRef).Append(',').Append(newPos).Append(',')
                .Append(Quote(placed.Line.Form)).Append(',').Append(Quote(placed.Line.Lemma)).Append('\n');
        }

        await _fileService.WriteAllTextAsync(path, builder.ToString(), SeedLayout.Utf8NoBom, token);
        _logger.LogInformation("Morphology seed report ({Lines} lines) written to {Path}", reportLines.Count, path);

        return fileName;
    }

    private static string Quote(string value)
    {
        return value.Contains(',') || value.Contains('"') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}
