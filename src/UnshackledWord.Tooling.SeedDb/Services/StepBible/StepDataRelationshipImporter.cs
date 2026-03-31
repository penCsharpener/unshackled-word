using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public sealed class StepDataRelationshipImporter : IRunner
{
    private readonly StepHebrewStrongsNormalizingStrategy _stepHebrewStrongsNormalizingStrategy;

    public StepDataRelationshipImporter(
        StepHebrewStrongsNormalizingStrategy stepHebrewStrongsNormalizingStrategy)
    {
        _stepHebrewStrongsNormalizingStrategy = stepHebrewStrongsNormalizingStrategy;
    }

    public async Task Run(CancellationToken token = default)
    {
        await _stepHebrewStrongsNormalizingStrategy.SaveToDatabase(null!, token);
    }
}
