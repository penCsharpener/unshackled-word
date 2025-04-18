using Serilog;

namespace UnshackledWord.Tooling.SeedDb;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddHostedService<Worker>();
        builder.Services.AddSerilog((sp, loggerConfig) => loggerConfig.ReadFrom.Configuration(builder.Configuration));

        var host = builder.Build();
        host.Run();
    }
}
