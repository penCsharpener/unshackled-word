using System.Text;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.BibleStructure;
using UnshackledWord.Domain.Models.Settings;

namespace UnshackledWord.Infrastructure.Seed;

/// <summary>Where the repo seed lives (<c>assets/seed</c>) and how its files are named.</summary>
public sealed class SeedLayout
{
    public const int FormatVersion = 1;
    public const string ManifestFileName = "manifest.json";
    public const string MorphologyFolder = "morphology";
    public const string AnchorsFolder = "anchors";
    public const string ReportFolder = "seed-report";
    public const string MappingsFolder = "Elb1871Mappings";
    public const int MappingsFormat = 3;

    /// <summary>Revision seed (roadmap 0011.11), below the seed root: <c>revision/levels.csv</c>, <c>revision/verses/NN-Book.csv</c>, <c>revision/mapping/NN-Book.csv</c>.</summary>
    public const string RevisionFolder = "revision";
    public const string RevisionLevelsFile = "levels.csv";
    public const string RevisionVersesFolder = "verses";
    public const string RevisionMappingFolder = "mapping";

    /// <summary>Vocab seed (roadmap 0022.14), below the seed root: <c>vocab/entries.csv</c>, <c>vocab/keys.csv</c>, <c>vocab/meanings.csv</c>.</summary>
    public const string VocabFolder = "vocab";

    public static readonly UTF8Encoding Utf8NoBom = new(false);

    private readonly IFileService _fileService;
    private readonly DatabaseSeedSettings _settings;
    private readonly string? _seedRootOverride;

    public SeedLayout(IFileService fileService, DatabaseSeedSettings settings, string? seedRootOverride = null)
    {
        _fileService = fileService;
        _settings = settings;
        _seedRootOverride = seedRootOverride;
        SeedRoot = seedRootOverride ?? fileService.Combine(settings.SolutionAssetsPath, "seed");
    }

    /// <summary>Folder of the seed files; <c>assets/seed</c> unless the export is directed elsewhere.</summary>
    public string SeedRoot { get; }

    /// <summary>
    /// The mapping CSVs: <c>assets/Elb1871Mappings</c> (next to <c>assets/seed</c>, where they have always been) or, when the export is
    /// directed to another folder, <c>DIR/Elb1871Mappings</c>, so an experiment never overwrites the repo's files.
    /// </summary>
    public string MappingsRoot => _seedRootOverride is null
        ? _fileService.Combine(_settings.SolutionAssetsPath, MappingsFolder)
        : _fileService.Combine(SeedRoot, MappingsFolder);

    public string ManifestPath => _fileService.Combine(SeedRoot, ManifestFileName);

    public string ReportRoot => _fileService.Combine(_settings.SolutionTempPath, ReportFolder);

    public string BaseTextPath => _fileService.Combine(_settings.SolutionAssetsPath, _settings.Elberfelder1871TextFile);

    /// <summary>Seed-relative path with forward slashes, e.g. <c>morphology/01-Genesis.csv</c>, as written into the manifest.</summary>
    public static string RelativePath(string folder, int bookId) => $"{folder}/{BookFileName(bookId)}";

    /// <summary><c>01-Genesis.csv</c>: same naming as the files in <c>assets/Elb1871Mappings</c>.</summary>
    public static string BookFileName(int bookId) => $"{bookId:D2}-{BibleBook.AllBooks[bookId].Name}.csv";

    public string ResolveMapping(string fileName) => _fileService.Combine(MappingsRoot, fileName);

    /// <summary>Seed-relative path of a revision file, e.g. <c>revision/verses/01-Genesis.csv</c>; <paramref name="subFolder"/> null = directly in <c>revision</c>.</summary>
    public static string RevisionPath(string? subFolder, string fileName) => subFolder is null ? $"{RevisionFolder}/{fileName}" : $"{RevisionFolder}/{subFolder}/{fileName}";

    public string Resolve(string relativePath) => _fileService.Combine(SeedRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
}
