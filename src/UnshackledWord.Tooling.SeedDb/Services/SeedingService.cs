using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;

namespace UnshackledWord.Tooling.SeedDb.Services;

public sealed class SeedingService
{
    private readonly IFileDownloader _fileDownloader;
    private readonly IUnzipService _unzipService;
    private readonly IFileService _fileService;
    private readonly AppSettings _options;

    public SeedingService(IFileDownloader fileDownloader, IUnzipService unzipService, IFileService fileService, IOptions<AppSettings> options)
    {
        _fileDownloader = fileDownloader;
        _unzipService = unzipService;
        _fileService = fileService;
        _options = options.Value;
    }

    public async Task SeedDatabaseAsync(CancellationToken token = default)
    {
        await _fileDownloader.DownloadFileAsync(token);
        var filePath = _fileService.Combine(_options.DatabaseSeeding.FolderLocation, "SR", "SR usfm.zip");
        var destinationPath = _fileService.Combine(_options.DatabaseSeeding.FolderLocation, "SR", "NT-books");
        _unzipService.UnzipArchive(filePath, destinationPath, true, token);
    }
}
