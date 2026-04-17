using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;

public sealed class Elb1871VerseRunner : IRunner
{
    private readonly IFileService _fileService;
    private readonly Elberfelder1871VerseStrategy _strategy;
    private readonly ILogger<Elb1871VerseRunner> _logger;
    private readonly AppSettings _options;

    public Elb1871VerseRunner(IFileService fileService,
        Elberfelder1871VerseStrategy strategy,
        IOptions<AppSettings> options,
        ILogger<Elb1871VerseRunner> logger)
    {
        _fileService = fileService;
        _strategy = strategy;
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

        await _strategy.SaveToDatabase(_options.DatabaseSeeding.Elberfelder1871TextFile, token);
    }
}
