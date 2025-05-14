using UnshackledWord.Application.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.BibelKommentare;
using UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;
using UnshackledWord.Tooling.SeedDb.Services.EliranWongData;
using UnshackledWord.Tooling.SeedDb.Services.StatisticalRestorationGnt;

namespace UnshackledWord.Tooling.SeedDb.Services;

/// <summary>
/// Returns the appropriate file parser based on the file name
/// </summary>
public sealed class FileParserFactory : IFileParserFactory
{
    private readonly IFileService _fileService;
    private readonly IServiceScopeFactory _scopeFactory;

    public FileParserFactory(IFileService fileService, IServiceScopeFactory scopeFactory)
    {
        _fileService = fileService;
        _scopeFactory = scopeFactory;
    }

    public IFileParserStrategy GetParser(string filePath)
    {
        var file = _fileService.GetFileName(filePath);
        using var scope = _scopeFactory.CreateScope();

        return file.Split(".") switch
        {
            ["SR", "tsv"] => scope.ServiceProvider.GetRequiredService<SrTsvParserStrategy>(),
            ["SR", "txt"] => scope.ServiceProvider.GetRequiredService<SrTxtParserStrategy>(),
            ["elberfelder1871", "txt"] => scope.ServiceProvider.GetRequiredService<Elberfelder1871Strategy>(),
            ["bible_elb_bk_mybible", "xml"] => scope.ServiceProvider.GetRequiredService<ElbParserStrategy>(),
            ["bible_elb_bk_mydbible", "xml"] => scope.ServiceProvider.GetRequiredService<RalfsLxxParserStrategy>(),
            _ => new DevNullParserStrategy()
        };
    }
}
