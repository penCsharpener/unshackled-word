using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UnshackledWord.Tooling.SeedDb.Extensions;
using UnshackledWord.Tooling.Sqlite.Migration.Extensions;

namespace UnshackledWord.Tooling.SeedDb.TestRunner.Extensions;

public static class DependencyHelper
{
    public static IServiceProvider RegisterDependencies()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<AssemblyMarker>()
            .Build();

        var serviceProvider = new ServiceCollection()
            .RegisterServices(configuration)
            .RegisterCsvServices()
            .BuildServiceProvider();

        return serviceProvider;
    }
}

public record AssemblyMarker;
