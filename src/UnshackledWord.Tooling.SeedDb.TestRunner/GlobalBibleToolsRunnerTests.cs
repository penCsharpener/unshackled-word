using Microsoft.Extensions.DependencyInjection;
using UnshackledWord.Tooling.SeedDb.Services.GlobalBibleTools;
using UnshackledWord.Tooling.SeedDb.TestRunner.Extensions;

namespace UnshackledWord.Tooling.SeedDb.TestRunner;

public class GlobalBibleToolsRunnerTests
{
    private readonly IServiceProvider _serviceProvider;

    public GlobalBibleToolsRunnerTests()
    {
        _serviceProvider = DependencyHelper.RegisterDependencies();
    }

    [Fact]
    public async Task Test_GbtRunner()
    {
        var strategy = _serviceProvider.GetRequiredService<GbtRunner>();
        await strategy.Run(CancellationToken.None);
    }
}
