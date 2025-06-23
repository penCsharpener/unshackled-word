using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;

public class ElberfelderTextRunner : IRunner
{
    private readonly IFileService _fileService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ElberfelderTextRunner> _logger;
    private readonly AppSettings _options;

    public ElberfelderTextRunner(IFileService fileService, IServiceScopeFactory scopeFactory, IOptions<AppSettings> options, ILogger<ElberfelderTextRunner> logger)
    {
        _fileService = fileService;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    public async Task Run(CancellationToken token = default)
    {
        if (_fileService.FileExists(_options.DatabaseSeeding.Elberfelder1871TextFile) is false)
        {
            _logger.LogInformation("Elberfelder 1871 text file does not exist at path: {FilePath}. Skipping import.", _options.DatabaseSeeding.Elberfelder1871TextFile);
            return;
        }

        using var scope = _scopeFactory.CreateScope();

        var strategy = scope.ServiceProvider.GetRequiredService<Elberfelder1871Strategy>();

        await strategy.SaveToDatabase(_options.DatabaseSeeding.Elberfelder1871TextFile, token);
    }
}
