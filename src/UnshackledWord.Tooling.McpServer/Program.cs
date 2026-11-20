using Serilog;
using UnshackledWord.Infrastructure.Extensions;
using UnshackledWord.Infrastructure.Observability;
using UnshackledWord.Infrastructure.Repositories;
using UnshackledWord.Persistence.Postgres.Extensions;
using UnshackledWord.Tooling.McpServer.Auth;

namespace UnshackledWord.Tooling.McpServer;

public partial class Program
{
    private const string ServiceName = "unshackledword-mcp";

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Configuration.AddEnvironmentVariables("UNSHACKLEDWORD_");
        AddLocalSecrets(builder.Configuration);

        builder.Services.AddSerilog((_, logger) => logger.ReadFrom.Configuration(builder.Configuration).WriteToTelemetry(builder.Configuration, ServiceName));
        builder.Services.AddPostgresPersistence();
        builder.Services.AddInfrastructureServices();
        builder.Services.AddRepositories();
        builder.AddUnshackledTelemetry(ServiceName);
        builder.Services.AddUnshackledHealthChecks();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IMcpCaller, McpCaller>();
        builder.Services.AddMcpServer()
            .WithHttpTransport(options => options.Stateless = true)
            .WithToolsFromAssembly();

        var app = builder.Build();
        app.MapMcp("/mcp");
        app.MapUnshackledHealth("/health");
        app.Run();
    }

    static partial void AddLocalSecrets(ConfigurationManager builder);
}
