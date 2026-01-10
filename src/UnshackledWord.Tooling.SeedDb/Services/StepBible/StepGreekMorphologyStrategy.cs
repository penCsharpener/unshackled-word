using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.StepBible.Models;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public sealed class StepGreekMorphologyStrategy : IFileParserStrategy<List<StepGreekMorphologyEntry>
{
    public StepGreekMorphologyStrategy()
    {
    }

    public async Task<List<StepGreekMorphologyEntry>> SaveToDatabase(string filePath, CancellationToken token = default)
    {
        return [];
    }
}