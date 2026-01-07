using Serilog;
using UnshackledWord.Tooling.SeedDb.Extensions;

namespace UnshackledWord.Tooling.SeedDb;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddSeedDbServices(builder.Configuration);
        builder.Services.AddHostedService<Worker>();
        builder.Services.AddSerilog((sp, loggerConfig) => loggerConfig.ReadFrom.Configuration(builder.Configuration));
        builder.Configuration.AddEnvironmentVariables("UNSHACKLEDWORD_");
        AddLocalSecrets(builder.Configuration);

        var host = builder.Build();
        host.Run();
    }

    static partial void AddLocalSecrets(ConfigurationManager builder);
}
