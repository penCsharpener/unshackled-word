using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.StatisticalRestorationGnt;

public class SrRunner : IRunner
{
    private readonly ILogger<SrRunner> _logger;
    private readonly IFileDownloader _fileDownloader;
    private readonly IFileService _fileService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DatabaseSeedSettings _options;

    public SrRunner(GithubFileDownloader downloader, IFileService fileService, IServiceScopeFactory scopeFactory,
        IOptions<AppSettings> options, ILogger<SrRunner> logger)
    {
        _fileDownloader = downloader;
        _fileService = fileService;
        _scopeFactory = scopeFactory;
        _options = options.Value.DatabaseSeeding;
        _logger = logger;
    }

    public async Task Run(CancellationToken token = default)
    {
        await _fileDownloader.DownloadFileAsync(token);

        using var scope = _scopeFactory.CreateScope();
        const string srTsvPath = "../../temp/SeedData/SR/SR.tsv";

        if (_fileService.FileExists(srTsvPath))
        {
            var strategy = scope.ServiceProvider.GetRequiredService<SrTsvParserStrategy>();
            await strategy.SaveToDatabase(srTsvPath, token);
        }

        const string srTxtPath = "../../temp/SeedData/SR/SR.txt";

        if (_fileService.FileExists(srTxtPath))
        {
            var strategy = scope.ServiceProvider.GetRequiredService<SrTxtParserStrategy>();
            await strategy.SaveToDatabase(srTxtPath, token);
        }
    }
}
