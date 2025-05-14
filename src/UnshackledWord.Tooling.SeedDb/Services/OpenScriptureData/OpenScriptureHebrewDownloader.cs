using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;

namespace UnshackledWord.Tooling.SeedDb.Services.OpenScriptureData;

public sealed class OpenScriptureHebrewDownloader : IFileDownloader
{
    private readonly IFileService _fileService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenScriptureHebrewDownloader> _logger;
    private readonly OpenScripturesGithubSettings _options;
    private readonly string _tempFolder;

    public OpenScriptureHebrewDownloader(
        IFileService fileService,
        HttpClient httpClient,
        IOptions<AppSettings> options,
        ILogger<OpenScriptureHebrewDownloader> logger)
    {
        _fileService = fileService;
        _httpClient = httpClient;
        _logger = logger;
        _tempFolder = options.Value.DatabaseSeeding.FolderLocation;
        _options = options.Value.DatabaseSeeding.OpenScripturesGithub;
    }

    public async Task<List<string>> DownloadFileAsync(CancellationToken token = default)
    {
        var fileName = _options.XmlFiles.Split(',').Select(x => $"{x}.xml").ToArray();
        var fileList = new List<string>();

        foreach (var file in fileName)
        {
            var filePath = _fileService.UrlCombine(_tempFolder, _options.LocalPath, file);

            if (_fileService.FileExists(filePath))
            {
                fileList.Add(filePath);
                continue;
            }

            var downloadUrl = _fileService.UrlCombine(_options.DownloadPath, file);
            var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, token);
            if (response.IsSuccessStatusCode is false)
            {
                _logger.LogError("Failed to download file: {FileName}", fileName);
                continue;
            }

            _fileService.CreateDirectoryIfNotExists(_fileService.Combine(_tempFolder, _options.LocalPath));
            await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);

            await using var httpStream = await response.Content.ReadAsStreamAsync(token);
            await httpStream.CopyToAsync(fileStream, token);
            fileList.Add(filePath);
        }

        return fileList;
    }
}
