using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.StepBible.Models;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public sealed class StepHebrewStrongsStrategy : IFileParserStrategy<List<StepHebrewStrongsEntry>
{
    public StepHebrewStrongsStrategy()
    {
    }

    public async Task<List<StepHebrewStrongsEntry>> SaveToDatabase(string filePath, CancellationToken token = default)
    {
        return [];
    }
}