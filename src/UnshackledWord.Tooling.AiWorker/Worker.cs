using UnshackledWord.Tooling.AiWorker.Mapping.Greek;
using UnshackledWord.Tooling.AiWorker.Mapping.Hebrew;

namespace UnshackledWord.Tooling.AiWorker;

public class Worker : BackgroundService
{
    private readonly GreekMappingService _gkMapping;
    private readonly HebrewMappingService _hebMapping;

    public Worker(GreekMappingService gkMapping, HebrewMappingService hebMapping)
    {
        _gkMapping = gkMapping;
        _hebMapping = hebMapping;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // await _gkMapping.RunAsync(stoppingToken);
        await _hebMapping.RunAsync(stoppingToken);
    }
}
