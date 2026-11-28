using UnshackledWord.Infrastructure.Seed.BaseText;
using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Seeding.Abstractions;

namespace UnshackledWord.Seeding.ElberfelderParser;

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
        var elb1871Path = _fileService.Combine(_options.DatabaseSeeding.SolutionAssetsPath,
            _options.DatabaseSeeding.Elberfelder1871TextFile);

        if (!_fileService.FileExists(elb1871Path))
        {
            throw new FileNotFoundException($"Elberfelder 1871 text file does not exist at path: {elb1871Path}. Check AppSettings:DatabaseSeeding:SolutionAssetsPath.", elb1871Path);
        }

        await _strategy.SaveToDatabase(elb1871Path, token);
    }
}
