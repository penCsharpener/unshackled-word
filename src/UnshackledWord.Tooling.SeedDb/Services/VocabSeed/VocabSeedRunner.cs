using UnshackledWord.Infrastructure.Seed.Vocab;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.VocabSeed;

/// <summary>
/// Syncs the vocab entries from <c>assets/seed/vocab</c> on the initial start (roadmap 0022.14); without the folder it does nothing.
/// The logic lives in <see cref="VocabSeedImporter"/> (shared with the maintainer page).
/// </summary>
public sealed class VocabSeedRunner : IRunner
{
    private readonly VocabSeedImporter _importer;

    public VocabSeedRunner(VocabSeedImporter importer)
    {
        _importer = importer;
    }

    public async Task Run(CancellationToken token = default)
    {
        await _importer.RunAsync(_importer.CreateLayout(), token);
    }
}
