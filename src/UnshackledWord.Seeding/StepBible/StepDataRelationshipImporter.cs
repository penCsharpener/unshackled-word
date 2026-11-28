using UnshackledWord.Seeding.Abstractions;

namespace UnshackledWord.Seeding.StepBible;

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
