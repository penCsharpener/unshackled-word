using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public sealed class StepDataRelationshipImporter : IRunner
{
    private readonly StepHebrewStrongsNormalizingStrategy _stepHebrewStrongsNormalizingStrategy;
    private readonly StepBibleStructureStrategy _stepBibleStructureStrategy;

    public StepDataRelationshipImporter(
        StepHebrewStrongsNormalizingStrategy stepHebrewStrongsNormalizingStrategy,
        StepBibleStructureStrategy stepBibleStructureStrategy)
    {
        _stepHebrewStrongsNormalizingStrategy = stepHebrewStrongsNormalizingStrategy;
        _stepBibleStructureStrategy = stepBibleStructureStrategy;
    }

    public async Task Run(CancellationToken token = default)
    {
        await _stepHebrewStrongsNormalizingStrategy.SaveToDatabase(null!, token);
        await _stepBibleStructureStrategy.SaveToDatabase(null!, token);
    }
}
