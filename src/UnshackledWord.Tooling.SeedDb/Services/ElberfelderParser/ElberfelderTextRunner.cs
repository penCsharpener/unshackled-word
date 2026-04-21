using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;

/// <summary>
/// Imports the assets/elberfelder1871-theword-export.txt file and from it the Hebrew and LXX verse counting
/// </summary>
public class ElberfelderTextRunner : IRunner
{
    private readonly IFileService _fileService;
    private readonly IDbReader _dbReader;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ElberfelderTextRunner> _logger;
    private readonly AppSettings _options;

    public ElberfelderTextRunner(IFileService fileService,
        IDbReader dbReader,
        IServiceScopeFactory scopeFactory,
        IOptions<AppSettings> options,
        ILogger<ElberfelderTextRunner> logger)
    {
        _fileService = fileService;
        _dbReader = dbReader;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    public async Task Run(CancellationToken token = default)
    {
        var elb1871Path = _fileService.Combine(_options.DatabaseSeeding.SolutionAssetsPath,
            _options.DatabaseSeeding.Elberfelder1871TextFile);

        if (_fileService.FileExists(elb1871Path) is false)
        {
            _logger.LogInformation("Elberfelder 1871 text file does not exist at path: {FilePath}. Skipping import.", elb1871Path);
            return;
        }

        using var scope = _scopeFactory.CreateScope();

        var strategy = scope.ServiceProvider.GetRequiredService<Elberfelder1871Strategy>();
        await strategy.SaveToDatabase(elb1871Path, token);

        var strategy2 = scope.ServiceProvider.GetRequiredService<BibleVerseCoutingMappingStrategy>();
        await strategy2.SaveToDatabase(elb1871Path, token);
    }
}
