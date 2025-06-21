using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;

public class ElberfelderTextRunner : IRunner
{
    private readonly IFileService _fileService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AppSettings _options;

    public ElberfelderTextRunner(IFileService fileService, IServiceScopeFactory scopeFactory, IOptions<AppSettings> options)
    {
        _fileService = fileService;
        _scopeFactory = scopeFactory;
        _options = options.Value;
    }

    public async Task Run(CancellationToken token = default)
    {
        using var scope = _scopeFactory.CreateScope();

        if (_fileService.FileExists(_options.DatabaseSeeding.Elberfelder1871TextFile))
        {
            var strategy = scope.ServiceProvider.GetRequiredService<Elberfelder1871Strategy>();

            await strategy.SaveToDatabase(_options.DatabaseSeeding.Elberfelder1871TextFile, token);
        }
    }
}
