using Microsoft.Extensions.Options;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Domain.Models.Settings;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.BibelKommentare;

namespace UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;

public class ElbRunner : IRunner
{
    private readonly IFileService _fileService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DatabaseSeedSettings _options;

    public ElbRunner(IFileService fileService, IServiceScopeFactory scopeFactory, IOptions<AppSettings> options)
    {
        _fileService = fileService;
        _scopeFactory = scopeFactory;
        _options = options.Value.DatabaseSeeding;
    }

    public async Task Run(CancellationToken token = default)
    {
        using var scope = _scopeFactory.CreateScope();
        IList<ElbVerse> bkList = null!;
        IList<Elb1871Verse> elb1871List = null!;

        /*
        var bkFilePath = _fileService.Combine(_options.SolutionTempPath, "SeedData/Elb/bible_elb_bk_mybible.xml") ;
        if (_fileService.FileExists(bkFilePath))
        {
            var strategy = scope.ServiceProvider.GetRequiredService<ElbParserStrategy>();

            await strategy.SaveToDatabase(bkFilePath, token);
            bkList = strategy.ElberfelderStrongsVerses;
        }
        */

        if (_fileService.FileExists(_options.Elberfelder1871TextFile))
        {
            var strategy = scope.ServiceProvider.GetRequiredService<Elberfelder1871Strategy>();

            await strategy.SaveToDatabase(_options.Elberfelder1871TextFile, token);
            elb1871List = strategy.Elberfelder1871Verses;
        }

        // var mergeStrategy = scope.ServiceProvider.GetRequiredService<ElberfelderMergeStrategy>();

        // await mergeStrategy.SaveToDatabaseAsync(bkList, elb1871List, token);
    }
}
