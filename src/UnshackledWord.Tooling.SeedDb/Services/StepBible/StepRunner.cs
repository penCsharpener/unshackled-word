using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.StepBible;

public sealed class StepRunner : IRunner
{
    private readonly StepDataDbImporter _dataDbImporter;

    public StepRunner(StepDataDbImporter dataDbImporter)
    {
        _dataDbImporter = dataDbImporter;
    }

    public async Task Run(CancellationToken token = default)
    {
        await _dataDbImporter.Run(token);
    }
}
