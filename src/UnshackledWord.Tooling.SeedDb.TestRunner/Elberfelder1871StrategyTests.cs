using Microsoft.Extensions.DependencyInjection;
using UnshackledWord.Tooling.SeedDb.Services.ElberfelderParser;
using UnshackledWord.Tooling.SeedDb.TestRunner.Extensions;

namespace UnshackledWord.Tooling.SeedDb.TestRunner;

public class Elberfelder1871StrategyTests
{
    private readonly IServiceProvider _serviceProvider;

    public Elberfelder1871StrategyTests()
    {
        _serviceProvider = DependencyHelper.RegisterDependencies();
    }

    [Fact]
    public async Task Test_Elberfelder1871Strategy()
    {
        var strategy = _serviceProvider.GetRequiredService<Elberfelder1871Strategy>();
        var filePath = "../../../../../temp/SeedData/Elb/elberfelder1871.txt";
        await strategy.SaveToDatabase(filePath, TestContext.Current.CancellationToken);

        Assert.NotEmpty(strategy.Elberfelder1871Verses);
    }
}
