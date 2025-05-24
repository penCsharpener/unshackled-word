using Microsoft.Extensions.DependencyInjection;
using UnshackledWord.Tooling.SeedDb.Services.CsvImports;
using UnshackledWord.Tooling.SeedDb.TestRunner.Extensions;

namespace UnshackledWord.Tooling.SeedDb.TestRunner;

public class CsvRunnerTests
{
    private readonly IServiceProvider _serviceProvider;

    public CsvRunnerTests()
    {
        _serviceProvider = DependencyHelper.RegisterDependencies();
    }

    [Fact]
    public async Task Test1()
    {
        var runner = _serviceProvider.GetRequiredService<CsvRunner>();
        await runner.Run(TestContext.Current.CancellationToken);
    }
}
