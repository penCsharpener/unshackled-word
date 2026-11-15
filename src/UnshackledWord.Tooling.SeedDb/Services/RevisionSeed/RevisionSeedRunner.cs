using UnshackledWord.Infrastructure.Seed.Revision;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.RevisionSeed;

/// <summary>
/// Seeds the revision variants from <c>assets/seed/revision</c> (roadmap 0011.12), after the Elb1871 words, the morphology and the
/// mappings exist. Into an empty <c>RevisionVerses</c> it imports; on a populated one it is the second rolling seed (after mappings):
/// variants nobody changed since the last import follow the repo's newer seed, the others are kept and reported. The logic lives in
/// <see cref="RevisionSeedImporter"/> (shared with the maintainer page).
/// </summary>
public sealed class RevisionSeedRunner : IRunner
{
    private readonly RevisionSeedImporter _importer;

    public RevisionSeedRunner(RevisionSeedImporter importer)
    {
        _importer = importer;
    }

    public async Task Run(CancellationToken token = default)
    {
        await _importer.RunAsync(_importer.CreateLayout(), token);
    }
}
