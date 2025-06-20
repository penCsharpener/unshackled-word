using Microsoft.Extensions.DependencyInjection;
using UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;
using UnshackledWord.Tooling.SeedDb.TestRunner.Extensions;

namespace UnshackledWord.Tooling.SeedDb.TestRunner;

public class ElberfelderRunnerTests
{
    private readonly IServiceProvider _serviceProvider;

    public ElberfelderRunnerTests()
    {
        _serviceProvider = DependencyHelper.RegisterDependencies();
    }

    [Fact]
    public async Task Test_ElberfelderRunner()
    {
        var runner = _serviceProvider.GetRequiredService<ElbRunner>();
        await runner.Run(TestContext.Current.CancellationToken);
    }
}
