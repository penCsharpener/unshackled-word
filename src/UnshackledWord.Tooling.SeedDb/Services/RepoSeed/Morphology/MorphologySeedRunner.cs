using UnshackledWord.Infrastructure.Seed.Morphology;
using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.RepoSeed.Morphology;

/// <summary>Imports the repo's morphology seed on a fresh install (roadmap 0010.4); the logic lives in <see cref="MorphologySeedImporter"/>.</summary>
public sealed class MorphologySeedRunner : IRunner
{
    private readonly MorphologySeedImporter _importer;

    public MorphologySeedRunner(MorphologySeedImporter importer)
    {
        _importer = importer;
    }

    public async Task Run(CancellationToken token = default)
    {
        await _importer.Run(token);
    }
}
