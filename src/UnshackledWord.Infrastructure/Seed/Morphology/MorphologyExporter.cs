using Microsoft.Extensions.Logging;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Seed.Anchoring;
using UnshackledWord.Application.Features.Seed.Morphology;
using UnshackledWord.Domain.Models.BibleStructure;

namespace UnshackledWord.Infrastructure.Seed.Morphology;

/// <summary>
/// <c>export-seed --only morphology</c>: writes <c>morphology/NN-Book.csv</c> (every German word, with its lemma and grammar when it
/// has a morphology row), <c>anchors/NN-Book.csv</c> (chapter fingerprints) and the manifest. Reads the database only.
/// </summary>
public sealed class MorphologyExporter
{
    private readonly MorphologyExportRepository _repository;
    private readonly IFileService _fileService;
    private readonly SeedManifestStore _manifestStore;
    private readonly ILogger<MorphologyExporter> _logger;

    public MorphologyExporter(MorphologyExportRepository repository, IFileService fileService, SeedManifestStore manifestStore, ILogger<MorphologyExporter> logger)
    {
        _repository = repository;
        _fileService = fileService;
        _manifestStore = manifestStore;
        _logger = logger;
    }

    public async Task ExportAsync(SeedLayout layout, CancellationToken token = default)
    {
        _fileService.CreateDirectoryIfNotExists(layout.SeedRoot);
        var kind = new SeedKind();
        var wordsWithMorphology = 0;

        foreach (var bookId in BibleBook.AllBooks.Keys.Order())
        {
            var lines = await _repository.ReadBookAsync(bookId);

            if (lines.Count == 0)
            {
                continue;
            }

            kind.Files.Add(await WriteMorphologyAsync(layout, bookId, lines, token));
            kind.AnchorFiles.Add(await WriteAnchorsAsync(layout, bookId, lines, token));
            wordsWithMorphology += lines.Count(l => l.HasMorphology);
        }

        kind.Rows = kind.Files.Sum(f => f.Rows);
        kind.SeedVersion = SeedVersion.Compute(kind.Files.Concat(kind.AnchorFiles));

        await CheckNothingWasLeftOutAsync(kind, wordsWithMorphology);
        await WriteManifestAsync(layout, kind, token);

        _logger.LogInformation("Exported morphology seed: {Files} files, {Rows} words, version {Version}", kind.Files.Count, kind.Rows, kind.SeedVersion[..12]);
    }

    private async Task<SeedFileEntry> WriteMorphologyAsync(SeedLayout layout, int bookId, List<MorphologySeedLine> lines, CancellationToken token)
    {
        var (bytes, sha256) = SeedCsv.Serialize<MorphologySeedLine, MorphologySeedLineMap>(lines);
        var relativePath = SeedLayout.RelativePath(SeedLayout.MorphologyFolder, bookId);

        await WriteFileAsync(layout, relativePath, bytes, token);

        return new SeedFileEntry { Path = relativePath, Rows = lines.Count, Sha256 = sha256 };
    }

    private async Task<SeedFileEntry> WriteAnchorsAsync(SeedLayout layout, int bookId, List<MorphologySeedLine> lines, CancellationToken token)
    {
        var rows = lines
            .Select(l => l.ToToken())
            .GroupBy(t => t.Chapter)
            .OrderBy(g => g.Key)
            .Select(g => new AnchorRow { Chapter = g.Key, Words = g.Count(), Fingerprint = ChapterFingerprint.Compute(g) })
            .ToList();
        var (bytes, sha256) = SeedCsv.Serialize<AnchorRow, AnchorRowMap>(rows);
        var relativePath = SeedLayout.RelativePath(SeedLayout.AnchorsFolder, bookId);

        await WriteFileAsync(layout, relativePath, bytes, token);

        return new SeedFileEntry { Path = relativePath, Rows = rows.Count, Sha256 = sha256 };
    }

    private async Task WriteFileAsync(SeedLayout layout, string relativePath, byte[] bytes, CancellationToken token)
    {
        var path = layout.Resolve(relativePath);
        _fileService.CreateDirectoryIfNotExists(Path.GetDirectoryName(path)!);
        await _fileService.WriteAllBytesAsync(path, bytes, token);
    }

    private async Task CheckNothingWasLeftOutAsync(SeedKind kind, int wordsWithMorphology)
    {
        var inTable = await _repository.CountMorphologyRowsAsync();

        if (inTable != wordsWithMorphology)
        {
            _logger.LogWarning("Elb1871Morphology has {InTable} rows but the seed holds {InSeed} lines with morphology (rows without a matching Elb1871Words row are not exported)", inTable, wordsWithMorphology);
        }
        else
        {
            _logger.LogInformation("All {Rows} Elb1871Morphology rows are in the seed ({Words} words in total, the rest has no morphology row)", inTable, kind.Rows);
        }
    }

    private async Task WriteManifestAsync(SeedLayout layout, SeedKind kind, CancellationToken token)
    {
        var existing = await _manifestStore.ReadAsync(layout.ManifestPath, token);

        var manifest = new SeedManifest
        {
            Format = SeedLayout.FormatVersion,
            BaseText = await DescribeBaseTextAsync(layout, token),
            Morphology = kind,
            Mappings = existing?.Mappings,
            Revision = existing?.Revision,
            Tsk = existing?.Tsk
        };

        await _manifestStore.WriteKeepingExportTimeAsync(layout.ManifestPath, manifest, token);
    }

    private async Task<SeedBaseText> DescribeBaseTextAsync(SeedLayout layout, CancellationToken token)
    {
        var path = layout.BaseTextPath;

        if (!_fileService.FileExists(path))
        {
            _logger.LogWarning("Base text file {Path} not found; manifest records no hash", path);

            return new SeedBaseText { File = Path.GetFileName(path) };
        }

        var bytes = await File.ReadAllBytesAsync(path, token);
        var lines = await _fileService.ReadAllLinesAsync(path, SeedLayout.Utf8NoBom, token);

        return new SeedBaseText { File = Path.GetFileName(path), Sha256 = SeedCsv.Sha256Hex(bytes), Words = Elb1871WordParser.Parse(lines).Count };
    }
}
