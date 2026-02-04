using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;

namespace UnshackledWord.Tooling.SeedDb.Services.BibelKommentare;

public class BkRunner : IRunner
{
    private readonly IFileService _fileService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DatabaseSeedSettings _options;

    public BkRunner(IFileService fileService, IServiceScopeFactory scopeFactory, IOptions<AppSettings> options)
    {
        _fileService = fileService;
        _scopeFactory = scopeFactory;
        _options = options.Value.DatabaseSeeding;
    }

    public async Task Run(CancellationToken token = default)
    {
        using var scope = _scopeFactory.CreateScope();
        IList<ElbVerse> bkList = null!;

        var bkFilePath = _fileService.Combine(_options.SolutionTempPath, "SeedData/Elb/bible_elb_bk_mybible.xml") ;
        if (_fileService.FileExists(bkFilePath))
        {
            var strategy = scope.ServiceProvider.GetRequiredService<ElbParserStrategy>();

            await strategy.SaveToDatabase(bkFilePath, token);
            bkList = strategy.ElberfelderStrongsVerses;
        }

        // bkList used in ElberfelderMergeStrategy. see ElbRunner
    }
}
