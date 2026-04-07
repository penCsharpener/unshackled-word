using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.AiMappingImport;

public sealed class AiMappingImportRunner : IRunner
{
    private readonly AiMappingImportRunnerRepository _repository;

    public AiMappingImportRunner(AiMappingImportRunnerRepository repository)
    {
        _repository = repository;
    }

    public async Task Run(CancellationToken token = default)
    {
        var backups = await _repository.ReadAllBackupsAsync(token);
    }
}
