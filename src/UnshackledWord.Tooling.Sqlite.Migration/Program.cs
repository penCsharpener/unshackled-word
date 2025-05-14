using Serilog;
using UnshackledWord.Tooling.Sqlite.Migration.Extensions;

namespace UnshackledWord.Tooling.Sqlite.Migration;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddSerilog((sp, loggerConfig) => loggerConfig.ReadFrom.Configuration(builder.Configuration));
        builder.Services.RegisterServices(builder.Configuration);

        var host = builder.Build();
        host.Run();
    }
}
