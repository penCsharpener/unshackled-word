namespace UnshackledWord.Tooling.AiWorker;

public class Worker : BackgroundService
{
    private readonly GreekMappingService _service;

    public Worker(GreekMappingService service)
    {
        _service = service;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _service.RunAsync(stoppingToken);
    }
}
