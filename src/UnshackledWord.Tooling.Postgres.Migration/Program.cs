using Serilog;
using UnshackledWord.Tooling.Postgres.Migration.Extensions;

namespace UnshackledWord.Tooling.Postgres.Migration;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddSerilog((sp, loggerConfig) => loggerConfig.ReadFrom.Configuration(builder.Configuration));
        builder.Services.RegisterServices(builder.Configuration);
        builder.Configuration.AddEnvironmentVariables("UNSHACKLEDWORD_");
        AddLocalSecrets(builder.Configuration);

        var host = builder.Build();
        host.Run();
    }

    static partial void AddLocalSecrets(ConfigurationManager builder);
}
