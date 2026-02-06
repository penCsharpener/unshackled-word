using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UnshackledWord.Infrastructure.Extensions;
using UnshackledWord.Persistence.Postgres.Extensions;
using UnshackledWord.Tooling.SeedDb.Services.StepBible;

namespace UnshackledWord.Tooling.SeedDb.TestRunner.Configuration;

public partial class TestConfiguration
{
    public static IServiceProvider GetServiceProvider()
    {
        var configurationBuilder = new ConfigurationBuilder();
        AddLocalSecrets(configurationBuilder);
        var config = configurationBuilder.Build();

        var services = new ServiceCollection();
        services.AddStepRunner();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddInfrastructureServices();
        services.AddPostgresPersistence();

        return services.BuildServiceProvider();
    }

    static partial void AddLocalSecrets(ConfigurationBuilder builder);
}
