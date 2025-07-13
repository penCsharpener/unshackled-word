using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UnshackledWord.Tooling.SeedDb.Extensions;

namespace UnshackledWord.Tooling.SeedDb.TestRunner.Extensions;

public static class DependencyHelper
{
    public static IServiceProvider RegisterDependencies()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json",  optional: true, reloadOnChange: true)
            .AddUserSecrets<AssemblyMarker>()
            .Build();

        var serviceProvider = new ServiceCollection()
            .AddSeedDbServices(configuration)
            .BuildServiceProvider();

        return serviceProvider;
    }
}

public record AssemblyMarker;
