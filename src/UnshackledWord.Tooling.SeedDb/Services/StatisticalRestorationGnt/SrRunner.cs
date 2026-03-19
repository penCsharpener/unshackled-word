using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.StatisticalRestorationGnt;

public class SrRunner : IRunner
{
    private readonly ILogger<SrRunner> _logger;
    private readonly GithubFileDownloader _fileDownloader;
    private readonly IFileService _fileService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDbReader _dbReader;
    private readonly DatabaseSeedSettings _options;

    public SrRunner(GithubFileDownloader downloader, IFileService fileService,
        IServiceScopeFactory scopeFactory, IDbReader dbReader,
        IOptions<AppSettings> options, ILogger<SrRunner> logger)
    {
        _fileDownloader = downloader;
        _fileService = fileService;
        _scopeFactory = scopeFactory;
        _dbReader = dbReader;
        _options = options.Value.DatabaseSeeding;
        _logger = logger;
    }

    public async Task Run(CancellationToken token = default)
    {
        await _fileDownloader.DownloadFileAsync(token);

        using var scope = _scopeFactory.CreateScope();
        var countSr = await GetCountTsvAsync(token);

        if (countSr > 0)
        {
            _logger.LogInformation("Statistical Restoration data imported. Skipping import... {countSr} rows", countSr);
            return;
        }

        var srTsvPath = _fileService.Combine(_options.SolutionTempPath, "SeedData/SR/SR.tsv");

        if (_fileService.FileExists(srTsvPath))
        {
            var strategy = scope.ServiceProvider.GetRequiredService<SrTsvParserStrategy>();
            await strategy.SaveToDatabase(srTsvPath, token);
        }

        return;

        var srTxtPath = _fileService.Combine(_options.SolutionTempPath, "SeedData/SR/SR.txt");

        if (_fileService.FileExists(srTxtPath))
        {
            var strategy = scope.ServiceProvider.GetRequiredService<SrTxtParserStrategy>();
            await strategy.SaveToDatabase(srTxtPath, token);
        }
    }

    private async Task<int> GetCountTsvAsync(CancellationToken token = default)
    {
        var sql = $"""
                   select count(*)
                   from "unshackled-word"."SrGntWords"
                   """;

        return await _dbReader.ExecuteScalarAsync<int>(sql);
    }
}
