using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;

namespace UnshackledWord.Tooling.SeedDb.Services.StatisticalRestorationGnt;

public class GithubFileDownloader : IFileDownloader
{
    private readonly IFileService _fileService;
    private readonly HttpClient _httpclient;
    private readonly ILogger<GithubFileDownloader> _logger;
    private readonly AppSettings _options;

    public GithubFileDownloader(IFileService fileService, HttpClient httpclient, IOptions<AppSettings> options,
        ILogger<GithubFileDownloader> logger)
    {
        _fileService = fileService;
        _httpclient = httpclient;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<List<string>> DownloadFileAsync(CancellationToken token = default)
    {
        EnsurePath();
        var fileList = new List<string>();

        foreach (var file in _options.DatabaseSeeding.SRFileUrls)
        {
            var fileName = _fileService.GetFileName(file);
            var filePath = _fileService.Combine(_options.DatabaseSeeding.FolderLocation, "SR",
                fileName.Replace("%20", " "));

            if (_fileService.FileExists(filePath))
            {
                fileList.Add(filePath);
                continue;
            }

            var response = await _httpclient.GetAsync(file, HttpCompletionOption.ResponseHeadersRead, token);
            if (response.IsSuccessStatusCode is false)
            {
                _logger.LogError("Failed to download file: {FileName}", fileName);
                continue;
            }

            await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);

            await using var httpStream = await response.Content.ReadAsStreamAsync(token);
            await httpStream.CopyToAsync(fileStream, token);
            fileList.Add(filePath);
        }

        return fileList;
    }

    public void EnsurePath()
    {
        _fileService.CreateDirectoryIfNotExists(_options.DatabaseSeeding.FolderLocation);
        var directoryPath = _fileService.Combine(_options.DatabaseSeeding.FolderLocation, "SR");
        _fileService.CreateDirectoryIfNotExists(directoryPath);
    }
}
