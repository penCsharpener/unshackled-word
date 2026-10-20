using Serilog;
using UnshackledWord.Infrastructure.Extensions;
using UnshackledWord.Infrastructure.Repositories;
using UnshackledWord.Persistence.Postgres.Extensions;

namespace UnshackledWord.Tooling.McpServer;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Configuration.AddEnvironmentVariables("UNSHACKLEDWORD_");
        AddLocalSecrets(builder.Configuration);

        builder.Services.AddSerilog((_, logger) => logger.ReadFrom.Configuration(builder.Configuration));
        builder.Services.AddPostgresPersistence();
        builder.Services.AddInfrastructureServices();
        builder.Services.AddRepositories();
        builder.Services.AddMcpServer()
            .WithHttpTransport(options => options.Stateless = true)
            .WithToolsFromAssembly();

        var app = builder.Build();
        app.MapMcp("/mcp");
        app.Run();
    }

    static partial void AddLocalSecrets(ConfigurationManager builder);
}
