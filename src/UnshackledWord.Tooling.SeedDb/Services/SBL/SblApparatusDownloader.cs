using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;

namespace UnshackledWord.Tooling.SeedDb.Services.SBL;

public sealed class SblApparatusDownloader : IFileDownloader
{
    private readonly IFileService _fileService;
    private readonly HttpClient _httpclient;
    private readonly ILogger<SblApparatusDownloader> _logger;
    private readonly AppSettings _options;

    public SblApparatusDownloader(IFileService fileService, IHttpClientFactory httpclient, IOptions<AppSettings> options,
        ILogger<SblApparatusDownloader> logger)
    {
        _fileService = fileService;
        _httpclient = httpclient.CreateClient("Github");
        _logger = logger;
        _options = options.Value;
    }

    public async Task<List<string>> DownloadFileAsync(CancellationToken token = default)
    {
        EnsurePath();
        var fileList = new List<string>();
        var sblSettings = _options.DatabaseSeeding.SblSettings;

        foreach (var file in Constants.SblDownloadFileNames.Keys)
        {
            var filePath = _fileService.Combine(sblSettings.ApparatusFilePath, file);

            if (_fileService.FileExists(filePath))
            {
                fileList.Add(filePath);
                continue;
            }

            var fileUrl = $"{sblSettings.ApparatusDownloadUrl}{file}";
            var response = await _httpclient.GetAsync(fileUrl, HttpCompletionOption.ResponseHeadersRead, token);
            if (response.IsSuccessStatusCode is false)
            {
                _logger.LogError("Failed to download file: {FileName}", fileUrl);
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
        _fileService.CreateDirectoryIfNotExists(new DirectoryInfo(_options.DatabaseSeeding.SblSettings.ApparatusFilePath).Parent.FullName);
        _fileService.CreateDirectoryIfNotExists(_options.DatabaseSeeding.SblSettings.ApparatusFilePath);
    }
}
