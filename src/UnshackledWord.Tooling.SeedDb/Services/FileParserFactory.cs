using UnshackledWord.Application.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;

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
            [ "SR", "tsv" ] => scope.ServiceProvider.GetRequiredService<SrTsvParserStrategy>(),
            [ "SR", "txt"] => scope.ServiceProvider.GetRequiredService<SrTxtParserStrategy>(),
            [ "bible_elb_bk_mybible", "xml"] => scope.ServiceProvider.GetRequiredService<ElbParserStrategy>(),
            _ => new DevNullParserStrategy()
        };
    }
}
