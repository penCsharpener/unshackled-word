using System.Text;
using UnshackledWord.Application.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.StepBible.Models;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public sealed class StepHebrewMorphologyStrategy : IFileParserStrategy<List<StepHebrewMorphologyEntry>>
{
    private readonly IFileService _fileService;

    public StepHebrewMorphologyStrategy(IFileService fileService)
    {
        _fileService = fileService;
    }

    public async Task<List<StepHebrewMorphologyEntry>> SaveToDatabase(string filePath, CancellationToken token = default)
    {
        var lines = await _fileService.ReadAllLinesAsync(filePath, Encoding.UTF8, token);
        return [];
    }
}
