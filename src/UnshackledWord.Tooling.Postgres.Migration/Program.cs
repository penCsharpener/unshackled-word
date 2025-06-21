using Serilog;
using UnshackledWord.Tooling.Postgres.Migration.Extensions;

namespace UnshackledWord.Tooling.Postgres.Migration;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddSerilog((sp, loggerConfig) => loggerConfig.ReadFrom.Configuration(builder.Configuration));
        builder.Services.RegisterServices(builder.Configuration);
        builder.Configuration.AddEnvironmentVariables("UNSHACKLEDWORD_");

        var host = builder.Build();
        host.Run();
    }
}
