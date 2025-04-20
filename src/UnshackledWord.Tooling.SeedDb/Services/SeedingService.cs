using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services;

public sealed class SeedingService
{
    private readonly IFileDownloader _githubDownloader;
    private readonly IFileDownloader _elbDownloader;
    private readonly IUnzipService _unzipService;
    private readonly IFileService _fileService;
    private readonly IFileParserFactory _parserFactory;
    private readonly AppSettings _options;

    public SeedingService(GithubFileDownloader githubDownloader,
        IFileDownloader elbDownloader,
        IUnzipService unzipService,
        IFileService fileService,
        IFileParserFactory parserFactory,
        IOptions<AppSettings> options)
    {
        _githubDownloader = githubDownloader;
        _elbDownloader = elbDownloader;
        _unzipService = unzipService;
        _fileService = fileService;
        _parserFactory = parserFactory;
        _options = options.Value;
    }

    public async Task SeedDatabaseAsync(CancellationToken token = default)
    {
        var files = await _githubDownloader.DownloadFileAsync(token);
        var elbFile = await _elbDownloader.DownloadFileAsync(token);
        files.AddRange(elbFile);
        var zipFiles = files.Where(x => x.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)).ToList();
        var destinationPathSr = _fileService.Combine(_options.DatabaseSeeding.FolderLocation, "SR", "NT-books");
        var destinationPathELb = _fileService.Combine(_options.DatabaseSeeding.FolderLocation, "Elb");

        _unzipService.UnzipArchive(zipFiles[0], destinationPathSr, true, token);
        _unzipService.UnzipArchive(zipFiles[1], destinationPathELb, true, token);

        var filesInFolder = _fileService.SearchFiles(_options.DatabaseSeeding.FolderLocation, "*", SearchOption.AllDirectories);

        foreach (var file in filesInFolder)
        {
            var parser = _parserFactory.GetParser(file);
            await parser.SaveToDatabase(file);
        }
    }
}
