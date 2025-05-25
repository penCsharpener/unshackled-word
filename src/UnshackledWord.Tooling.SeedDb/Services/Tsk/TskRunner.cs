using UnshackledWord.Tooling.SeedDb.Services.Abstractions;

namespace UnshackledWord.Tooling.SeedDb.Services.Tsk;

public sealed class TskRunner : IRunner
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TskRunner> _logger;

    public TskRunner(IServiceScopeFactory scopeFactory, ILogger<TskRunner> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task Run(CancellationToken token = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<TskTextReader>();

        await runner.ReadAsync(token);
    }
}
