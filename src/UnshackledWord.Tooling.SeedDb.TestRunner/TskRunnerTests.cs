using Microsoft.Extensions.DependencyInjection;
using UnshackledWord.Tooling.SeedDb.Services.Tsk;
using UnshackledWord.Tooling.SeedDb.TestRunner.Extensions;

namespace UnshackledWord.Tooling.SeedDb.TestRunner;

public class TskRunnerTests
{
    private readonly IServiceProvider _serviceProvider;

    public TskRunnerTests()
    {
        _serviceProvider = DependencyHelper.RegisterDependencies();
    }

    [Fact]
    public async Task Test_TskRunner()
    {
        var runner = _serviceProvider.GetRequiredService<TskRunner>();
        await runner.Run(TestContext.Current.CancellationToken);
    }
}
