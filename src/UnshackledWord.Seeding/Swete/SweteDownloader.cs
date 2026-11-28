using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;

namespace UnshackledWord.Seeding.Swete;

/// <summary>
/// Downloads Swete's versification and word list (eliranwong/LXX-Swete-1930) from the pinned commit into
/// <c>&lt;SolutionTempPath&gt;/SeedData/Swete/</c> (roadmap 0028.1). Files that are already there are cached; a marker file
/// <c>commit.txt</c> records which commit they came from, so changing <c>AppSettings:DatabaseSeeding:Swete:Commit</c> downloads again.
/// </summary>
public sealed class SweteDownloader : IFileDownloader
{
    private const string MarkerFileName = "commit.txt";

    private readonly IFileService _fileService;
    private readonly HttpClient _httpClient;
    private readonly DatabaseSeedSettings _options;
    private readonly ILogger<SweteDownloader> _logger;

    public SweteDownloader(HttpClient httpClient, IFileService fileService, IOptions<AppSettings> options, ILogger<SweteDownloader> logger)
    {
        _httpClient = httpClient;
        _fileService = fileService;
        _options = options.Value.DatabaseSeeding;
        _logger = logger;
    }

    public string DirectoryPath => _fileService.Combine(_options.SolutionTempPath, "SeedData", "Swete");

    public string VersificationPath => _fileService.Combine(DirectoryPath, _options.Swete.VersificationFile);

    public string WordsPath => _fileService.Combine(DirectoryPath, _options.Swete.WordsFile);

    /// <summary>Returns the local paths of both files (versification first). Throws when a download fails.</summary>
    public async Task<List<string>> DownloadFileAsync(CancellationToken token = default)
    {
        var settings = _options.Swete;
        _fileService.CreateDirectoryIfNotExists(_fileService.Combine(_options.SolutionTempPath, "SeedData"));
        _fileService.CreateDirectoryIfNotExists(DirectoryPath);

        var markerPath = _fileService.Combine(DirectoryPath, MarkerFileName);
        var cachedCommit = _fileService.FileExists(markerPath)
            ? (await _fileService.ReadAllTextAsync(markerPath, System.Text.Encoding.UTF8, token)).Trim()
            : null;
        var stale = cachedCommit != settings.Commit;

        var paths = new List<string>();
        foreach (var fileName in new[] { settings.VersificationFile, settings.WordsFile })
        {
            var savePath = _fileService.Combine(DirectoryPath, fileName);
            paths.Add(savePath);
            if (!stale && _fileService.FileExists(savePath))
            {
                continue;
            }

            var url = $"{settings.RawBaseUrl.TrimEnd('/')}/{settings.Commit}/{fileName}";
            _logger.LogInformation("Downloading {Url}", url);
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            var tempPath = savePath + ".part";
            await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            await using (var httpStream = await response.Content.ReadAsStreamAsync(token))
            {
                await httpStream.CopyToAsync(fileStream, token);
            }

            File.Move(tempPath, savePath, overwrite: true);
        }

        if (stale)
        {
            await _fileService.WriteAllTextAsync(markerPath, settings.Commit, System.Text.Encoding.UTF8, token);
        }

        return paths;
    }
}
