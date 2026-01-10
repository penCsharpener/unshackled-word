using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.StepBible.Models;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public sealed class StepGreekStrongsStrategy : IFileParserStrategy<List<StepGreekStrongsEntry>
{
    public StepGreekStrongsStrategy()
    {
    }

    public async Task<List<StepGreekStrongsEntry>> SaveToDatabase(string filePath, CancellationToken token = default)
    {
        return [];
    }
}