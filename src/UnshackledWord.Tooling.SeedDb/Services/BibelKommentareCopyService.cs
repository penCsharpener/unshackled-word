using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;

namespace UnshackledWord.Tooling.SeedDb.Services;

public class BibelKommentareCopyService : IFileDownloader
{
    private readonly IFileService _fileService;
    private readonly ILogger<GithubFileDownloader> _logger;
    private readonly AppSettings _options;

    public BibelKommentareCopyService(IFileService fileService, IOptions<AppSettings> options, ILogger<GithubFileDownloader> logger)
    {
        _fileService = fileService;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<List<string>> DownloadFileAsync(CancellationToken token = default)
    {
        EnsurePath();
        var fileList = new List<string>();

        var fileName = _fileService.GetFileName(_options.DatabaseSeeding.BibleKommentareElberfelderUrl);
        var filePath = _fileService.Combine(_options.DatabaseSeeding.FolderLocation, "Elb", fileName);

        if (_fileService.FileExists(filePath))
        {
            fileList.Add(filePath);
            return fileList;
        }

        _fileService.Copy(_options.DatabaseSeeding.BibleKommentareSourcePath, _options.DatabaseSeeding.BibleKommentareDestinationPath, true);

        fileList.Add(filePath);

        return fileList;
    }

    public void EnsurePath()
    {
        _fileService.CreateDirectoryIfNotExists(_options.DatabaseSeeding.FolderLocation);
        var directoryPath = _fileService.Combine(_options.DatabaseSeeding.FolderLocation, "Elb");
        _fileService.CreateDirectoryIfNotExists(directoryPath);
    }
}
