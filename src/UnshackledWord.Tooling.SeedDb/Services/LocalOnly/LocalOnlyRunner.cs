using UnshackledWord.Tooling.SeedDb.Services.Abstractions;
using UnshackledWord.Tooling.SeedDb.Services.LocalOnly.Repositories;

namespace UnshackledWord.Tooling.SeedDb.Services.LocalOnly;

public class LocalOnlyRunner : IRunner
{
    private readonly CopyBackMappingRepository _repo;

    public LocalOnlyRunner(CopyBackMappingRepository repo)
    {
        _repo = repo;
    }

    public async Task Run(CancellationToken token = default)
    {
        await _repo.CopyOverGreekMappingAsync(token);
        await _repo.CopyOverHebrewMappingAsync(token);
    }
}
