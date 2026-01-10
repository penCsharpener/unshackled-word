using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.StepBible.Models;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public sealed class StepHebrewMorphologyStrategy : IFileParserStrategy<List<StepHebrewMorphologyEntry>
{
    public StepHebrewMorphologyStrategy()
    {
    }

    public async Task<List<StepHebrewMorphologyEntry>> SaveToDatabase(string filePath, CancellationToken token = default)
    {
        return [];
    }
}