using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;

namespace UnshackledWord.Tooling.SeedDb.Services.Elb1871Lemmatizer;

public sealed class LemmatizerDownloader : IFileDownloader
{
    private readonly IFileService _fileService;
    private readonly ILogger<LemmatizerDownloader> _logger;
    private readonly HttpClient _httpclient;
    private readonly Elberfelder1871 _settings;
    private readonly string _solutionTempPath;

    public LemmatizerDownloader(IHttpClientFactory clientFactory,
        IFileService fileService,
        IOptions<AppSettings> settings,
        ILogger<LemmatizerDownloader> logger)
    {
        _fileService = fileService;
        _settings = settings.Value.DatabaseSeeding.Elberfelder1871;
        _solutionTempPath = settings.Value.DatabaseSeeding.SolutionTempPath;
        _httpclient = clientFactory.CreateClient("Github");
        _logger = logger;
    }

    public async Task<List<string>> DownloadFileAsync(CancellationToken token = default)
    {
        var path = _fileService.Combine(_solutionTempPath, "full7z-multext-ge.lem");

        if (_fileService.FileExists(path))
        {
            return [];
        }

        var response = await _httpclient.GetAsync(_settings.LemmatizerGermanLink, HttpCompletionOption.ResponseHeadersRead, token);
        if (response.IsSuccessStatusCode is false)
        {
            _logger.LogError("Failed to download file: {FileName}", _settings.LemmatizerGermanLink);
            return [];
        }

        await using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);

        await using var httpStream = await response.Content.ReadAsStreamAsync(token);
        await httpStream.CopyToAsync(fileStream, token);

        return new List<string>()
        {
            new FileInfo(path).FullName
        };
    }
}
