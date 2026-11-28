using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Application.Features.Lxx;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Seeding.StepBible;

namespace UnshackledWord.Seeding.Swete;

/// <summary>The inputs of <see cref="LxxVersification.Map"/> as read from disk, and the mapping result.</summary>
public sealed record LxxSources(
    IReadOnlyList<SweteVerse> Verses,
    TvtmsGreekTable Table,
    IReadOnlyList<SweteRahlfsException> Exceptions,
    IReadOnlyList<string> ParseErrors,
    LxxVersificationResult Result);

/// <summary>
/// Loads Swete (downloaded, cached), TVTMS (from the STEP download, fetched when missing) and the exception list
/// <c>assets/seed/lxx/swete-to-rahlfs.csv</c> and maps them with <see cref="LxxVersification"/> (roadmap 0028). Shared by <c>lxx-check</c>
/// and <see cref="LxxTextImporter"/>.
/// </summary>
public sealed class LxxSourceLoader
{
    public const string ExceptionFile = "seed/lxx/swete-to-rahlfs.csv";

    private readonly SweteDownloader _sweteDownloader;
    private readonly StepGithubDownloader _stepDownloader;
    private readonly IFileService _fileService;
    private readonly DatabaseSeedSettings _options;
    private readonly ILogger<LxxSourceLoader> _logger;

    public LxxSourceLoader(SweteDownloader sweteDownloader, StepGithubDownloader stepDownloader, IFileService fileService, IOptions<AppSettings> options,
        ILogger<LxxSourceLoader> logger)
    {
        _sweteDownloader = sweteDownloader;
        _stepDownloader = stepDownloader;
        _fileService = fileService;
        _options = options.Value.DatabaseSeeding;
        _logger = logger;
    }

    /// <summary>Null (with the reason logged) when TVTMS or the exception list cannot be found.</summary>
    public async Task<LxxSources?> LoadAsync(CancellationToken token = default)
    {
        var sweteFiles = await _sweteDownloader.DownloadFileAsync(token);
        var tvtmsPath = await EnsureTvtmsAsync(token);
        if (tvtmsPath is null)
        {
            _logger.LogError("TVTMS file not found and could not be downloaded.");
            return null;
        }

        var exceptionPath = _fileService.Combine(_options.SolutionAssetsPath, ExceptionFile);
        if (!_fileService.FileExists(exceptionPath))
        {
            _logger.LogError("Exception list {Path} not found.", exceptionPath);
            return null;
        }

        var verses = SweteParser.Parse(await File.ReadAllLinesAsync(sweteFiles[0], token), await File.ReadAllLinesAsync(sweteFiles[1], token));
        var table = TvtmsGreekParser.Parse(await File.ReadAllLinesAsync(tvtmsPath, token));
        var parseErrors = new List<string>();
        var exceptions = SweteRahlfsExceptionList.Parse(await File.ReadAllLinesAsync(exceptionPath, token), parseErrors);
        var result = LxxVersification.Map(verses, exceptions, table);

        return new LxxSources(verses, table, exceptions, parseErrors, result);
    }

    private async Task<string?> EnsureTvtmsAsync(CancellationToken token)
    {
        var fileName = _fileService.GetFileName(_options.StepBibleData.VersificationFile).Replace("%20", " ");
        var path = _fileService.Combine(_options.SolutionTempPath, "SeedData", "Step", fileName);
        if (!_fileService.FileExists(path))
        {
            _logger.LogInformation("TVTMS not cached; running the STEP downloader.");
            await _stepDownloader.DownloadFileAsync(token);
        }

        return _fileService.FileExists(path) ? path : null;
    }
}
